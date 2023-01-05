using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Threading;


namespace TerrainGenerator {

    public class AdaptiveContour {
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

        Dictionary<Vector3, Point> pointDensityData = new();
        Vector3Int size;
        List<int> faces = new();
        List<Vector3> vertices = new();
        Dictionary<Vector3, int> vertexIndices = new ();

        float DensityFunction(Vector3 x) {
            return densityFunction(x);
        }

        public AdaptiveContour(Func<Vector3, float> densityFunction, Vector3Int size) {
            this.pointDensityData = new Dictionary<Vector3, Point>();
            this.densityFunction = densityFunction;
            this.size = size;
        }

        void PopulateDensityData() {
            foreach (Vector3 coordinates in Volume(size)) {
                float density = DensityFunction(coordinates);
                pointDensityData[coordinates] = new Point(coordinates, density);
            }
        }

        public void RunContouring() {
            PopulateDensityData();
            GenerateVertices();
            GenerateFaces();
        }

        public void SetMesh(Mesh mesh) {
            mesh.Clear();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = faces.ToArray();
            mesh.RecalculateNormals();
        }

        void GenerateVertices() {
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
        }

        void GenerateFaces() {
            Vector3[] offsets = new Vector3[3] { Vector3.right, Vector3.up, Vector3.forward };

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
                            pointDensityData[vertex] = new Point(vertex, DensityFunction(vertex));
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
            float nx = DensityFunction(coordinates - new Vector3(delta, 0, 0)) - DensityFunction(coordinates + new Vector3(delta, 0, 0));
            float ny = DensityFunction(coordinates - new Vector3(0, delta, 0)) - DensityFunction(coordinates + new Vector3(0, delta, 0));
            float nz = DensityFunction(coordinates - new Vector3(0, 0, delta)) - DensityFunction(coordinates + new Vector3(0, 0, delta));
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