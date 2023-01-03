using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace TerrainGenerator {
    public class AdaptiveContour {
        Dictionary<Vector3, Point> pointDensityData;
        readonly Func<float3, float> densityFunction;

        public AdaptiveContour(Func<float3, float> densityFunction) {
            pointDensityData = new Dictionary<Vector3, Point>();
            this.densityFunction = densityFunction;
        }

        public void PopulateDensityData(Vector3Int size) {
            foreach (Vector3 coordinates in Volume(size)) {
                float density = densityFunction(coordinates);
                pointDensityData[coordinates] = new Point(coordinates, density);
            }
        }

        public List<Vector3> RunContouring() {
            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            Dictionary<Vector3, int> vertexIndices = new Dictionary<Vector3, int>();
            List<Quad> faces = new List<Quad>();

            foreach (Point point in pointDensityData.Values) {
                Point[,,] octet = GetNeighbouringOctet(point);
                List<Vector3> transitions = CalculateTransitions(octet);

                if (transitions.Count != 0) {
                    List<Vector3> normals = CalculateNormals(transitions);
                    Vector3 vertex = ParticleDescent(point.coordinates, transitions, normals);
                    vertices.Add(vertex);
                    vertexIndices[vertex] = vertices.Count;
                }
            }
            //foreach (Vector3 vertex in vertices) {
            //    // Calculate quads

            //}
            return vertices;
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
                        if (pointDensityData.ContainsKey(vertex)) {
                            octet[dx, dy, dz] = pointDensityData[vertex];
                        } else {
                            octet[dx, dy, dz] = new Point(vertex, densityFunction(vertex));
                        }

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

                    force += -1f * normal * Vector3.Dot(normal, centerPoint - transition);
                }

                float damping = 1 - (float)i / maxIterations;
                centerPoint += force * damping / transitionsCount;

                if (force.sqrMagnitude * force.sqrMagnitude < threshold) {
                    break;
                }
            }

            return coordinates + centerPoint;
        }

        Vector3 ApproximateNormals(Vector3 coordinates) {
            float delta = 0.0001f;
            float pointDensity = densityFunction(coordinates);
            float nx = (pointDensity - densityFunction(coordinates + new Vector3(delta, 0, 0))) / delta;
            float ny = (pointDensity - densityFunction(coordinates + new Vector3(0, delta, 0))) / delta;
            float nz = (pointDensity - densityFunction(coordinates + new Vector3(0, 0, delta))) / delta;
            return new Vector3(nx, ny, nz).normalized;
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

    struct Quad {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
        public Vector3 d;

        public Vector3 this[int i] {
            get {
                return i switch {
                    0 => a,
                    1 => b,
                    2 => c,
                    _ => d,
                };
            }
        }
    }
}