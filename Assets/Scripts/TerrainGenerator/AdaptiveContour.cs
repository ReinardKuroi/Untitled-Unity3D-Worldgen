using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace TerrainGenerator {

    public class AdaptiveContour {
        Dictionary<Vector3, Point> pointDensityData;
        readonly Func<Vector3, float> densityFunction;
        readonly Vector3[,] cubicOffsets = new Vector3[3,6] {
            {
                // along x
                new(0, 0, 0),
                new(0, 1, 0),
                new(0, 0, 1),
                new(0, 1, 0),
                new(0, 1, 1),
                new(0, 0, 1)
            },
            {
                // along y
                new(0, 0, 0),
                new(0, 0, 1),
                new(1, 0, 0),
                new(0, 0, 1),
                new(1, 0, 1),
                new(1, 0, 0)
            },
            {
                // along z
                new(0, 0, 0),
                new(1, 0, 0),
                new(0, 1, 0),
                new(1, 0, 0),
                new(1, 1, 0),
                new(0, 1, 0)
            }
        };
        Vector3Int size;

        public AdaptiveContour(Func<Vector3, float> densityFunction, Vector3Int size) {
            this.pointDensityData = new Dictionary<Vector3, Point>();
            this.densityFunction = densityFunction;
            this.size = size;
        }

        public void PopulateDensityData() {
            foreach (Vector3 coordinates in Volume(size)) {
                float density = densityFunction(coordinates);
                pointDensityData[coordinates] = new Point(coordinates, density);
            }
        }

        public Mesh RunContouring() {
            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            Dictionary<Vector3, int> vertexIndices = new Dictionary<Vector3, int>();
            List<int> faces;
            List<Vector3> normals;

            foreach (Vector3 coordinates in Volume(size)) {
                Point point = pointDensityData[coordinates];
                Point[,,] octet = GetNeighbouringOctet(point);
                List<Vector3> transitions = CalculateTransitions(octet);

                if (transitions.Count != 0) {
                    List<Vector3> gradients = CalculateNormals(transitions);
                    Vector3 vertex = ParticleDescent(coordinates, transitions, gradients);
                    vertices.Add(vertex);
                    vertexIndices[coordinates] = vertices.Count - 1;
                }
            }

            faces = GenerateFaces(vertexIndices);

            mesh.vertices = vertices.ToArray();
            mesh.triangles = faces.ToArray();
            mesh.RecalculateNormals();

            return mesh;
        }

        List<int> GenerateFaces(Dictionary<Vector3, int> vertexIndices) {
            Vector3[] offsets = new Vector3[3] { Vector3.right, Vector3.up, Vector3.forward };
            List<int> faces = new List<int>();

            foreach (Vector3 coordinates in Volume(size)) {
                for (int axis = 0; axis < offsets.Length; ++axis) {
                    Vector3 offsetCoordinates = coordinates + offsets[axis];
                    if (offsetCoordinates.x == 0 || offsetCoordinates.y == 0 || offsetCoordinates.z == 0) {
                        continue;
                    }
                    bool inside = pointDensityData[coordinates].Exists;
                    bool outside = pointDensityData[offsetCoordinates].Exists;
                    if (inside != outside) {
                        int[] quad = GenerateQuad(coordinates, axis, vertexIndices);
                        if (outside) {
                            Array.Reverse(quad);
                        }
                        faces.AddRange(quad);
                    }
                }
            }

            return faces;
        }

        int[] GenerateQuad(Vector3 coordinates, int axis, Dictionary<Vector3, int> vertexIndices) {
            int[] quad = new int[6];

            for (int i = 0; i < quad.Length; ++i) {
                Vector3 index = coordinates - cubicOffsets[axis, i];
                quad[i] = vertexIndices[index];
            }

            return quad;
        }

        float Interpolate(float a, float b) {
            return a / (a - b);
        }

        Point[,,] GetNeighbouringOctet(Point point) {
            Point[,,] octet = new Point[2, 2, 2];
            for (int dx = 0; dx < 2; ++dx) {
                for (int dy = 0; dy < 2; ++dy) {
                    for (int dz = 0; dz < 2; ++dz) {
                        Vector3 vertex = point.coordinates + new Vector3(dx, dy, dz);
                        if (!pointDensityData.ContainsKey(vertex)) {
                            pointDensityData[vertex] = new Point(vertex, densityFunction(vertex));
                        }
                        octet[dx, dy, dz] = pointDensityData[vertex];
                    }
                }
            }
            return octet;
        }

        List<Vector3> CalculateTransitions(Point[,,] octet) {
            List<Vector3> transitions = new List<Vector3>();
            Point point = octet[0, 0, 0];
            // Along x axis
            for (int dy = 0; dy < 2; ++dy) {
                for (int dz = 0; dz < 2; ++dz) {
                    Point a = octet[0, dy, dz];
                    Point b = octet[1, dy, dz];
                    if (a.Exists != b.Exists) {
                        float interpolateX = Interpolate(a.density, b.density);
                        transitions.Add(point.coordinates + new Vector3(interpolateX, dy, dz));
                    }
                }
            }
            // Along y axis
            for (int dx = 0; dx < 2; ++dx) {
                for (int dz = 0; dz < 2; ++dz) {
                    Point a = octet[dx, 0, dz];
                    Point b = octet[dx, 1, dz];
                    if (a.Exists != b.Exists) {
                        float interpolateY = Interpolate(a.density, b.density);
                        transitions.Add(point.coordinates + new Vector3(dx, interpolateY, dz));
                    }
                }
            }
            // Along z axis
            for (int dx = 0; dx < 2; ++dx) {
                for (int dy = 0; dy < 2; ++dy) {
                    Point a = octet[dx, dy, 0];
                    Point b = octet[dx, dy, 1];
                    if (a.Exists != b.Exists) {
                        float interpolateZ = Interpolate(a.density, b.density);
                        transitions.Add(point.coordinates + new Vector3(dx, dy, interpolateZ));
                    }
                }
            }
            return transitions;
        }

        List<Vector3> CalculateNormals(List<Vector3> transitions) {
            List<Vector3> normals = new List<Vector3>();
            foreach (Vector3 transition in transitions) {
                normals.Add(ApproximateNormals(transition));
            }
            return normals;
        }

        private Vector3 ParticleDescent(Vector3 coordinates, List<Vector3> transitions, List<Vector3> normals, float threshold = 0.00001f) {
            int maxIterations = 50;
            int transitionsCount = transitions.Count;
            Vector3 centerPoint = new Vector3();
            
            foreach (Vector3 transition in transitions) {
                centerPoint += transition;
            }
            centerPoint /= transitionsCount;

            for (int i = 0; i < maxIterations; ++i) {
                Vector3 force = new Vector3();

                for (int j = 0; j < transitionsCount; ++j) {
                    Vector3 transition = transitions[j];
                    Vector3 normal = normals[j];

                    force += normal * - 1f * Vector3.Dot(normal, centerPoint - transition);
                }

                float damping = 1 - (float)i / maxIterations;
                centerPoint += force * damping / transitionsCount;

                if (force.sqrMagnitude * force.sqrMagnitude < threshold) {
                    break;
                }
            }

            return centerPoint;
        }

        Vector3 ApproximateNormals(Vector3 coordinates) {
            float delta = 0.00001f;
            float nx = densityFunction(coordinates - new Vector3(delta, 0, 0)) - densityFunction(coordinates + new Vector3(delta, 0, 0));
            float ny = densityFunction(coordinates - new Vector3(0, delta, 0)) - densityFunction(coordinates + new Vector3(0, delta, 0));
            float nz = densityFunction(coordinates - new Vector3(0, 0, delta)) - densityFunction(coordinates + new Vector3(0, 0, delta));
            Vector3 gradient = new Vector3(nx, ny, nz);
            return Vector3.Normalize(-gradient);
        }
        IEnumerable<Vector3Int> Volume(Vector3Int size) {
            for (int x = 0; x < size.x + 1; ++x) {
                for (int y = 0; y < size.y + 1; ++y) {
                    for (int z = 0; z < size.z + 1; ++z) {
                        yield return new Vector3Int(x, y, z);
                    }
                }
            }
        }
    }
}