using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator {
    public class AdaptiveCountour {
        Dictionary<int, Point> pointDensityData;

        AdaptiveCountour(Dictionary<int, Point> pointDensityData) {
            this.pointDensityData = pointDensityData;
        }

        Mesh RunContouring() {
            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            Dictionary<Vector3, int> vertexIndices = new Dictionary<Vector3, int>();
            List<Quad> faces = new List<Quad>();
            foreach (Point point in pointDensityData.Values) {
                Vector3 vertex = FindBestVertex(point);
                vertices.Add(vertex);
                vertexIndices[vertex] = vertices.Count;
            }
            foreach (Vector3 vertex in vertices) {
                // Calculate quads
            }
            return mesh;
        }

        float Interpolate(float a, float b) {
            return a / (a - b);
        }

        Vector3 FindBestVertex(Point point) {
            int hash;
            Point[,,] octet = new Point[2,2,2];
            List<Vector3> transitions = new List<Vector3>();
            for (int dx = 0; dx < 2; ++dx) {
                for (int dy = 0; dy < 2; ++dy) {
                    for (int dz = 0; dz < 2; ++dz) {
                        hash = Point.CalculateHash(point.Coordinates.x + dx, point.Coordinates.y + dy, point.Coordinates.z + dz);
                        octet[dx, dy, dz] = pointDensityData[hash];
                    }
                }
            }
            // Calculate sign changes
            // Along x axis
            for (int dy = 0; dy < 2; ++dy) {
                for (int dz = 0; dz < 2; ++dz) {
                    Point a = octet[0, dy, dz];
                    Point b = octet[1, dy, dz];
                    if (a.Exists != b.Exists) {
                        float interpolateX = Interpolate(a.Density, b.Density);
                        transitions.Add(point.Coordinates + new Vector3(interpolateX, dy, dz));
                    }
                }
            }
            // Along y axis
            for (int dx = 0; dx < 2; ++dx) {
                for (int dz = 0; dz < 2; ++dz) {
                    Point a = octet[dx, 0, dz];
                    Point b = octet[dx, 1, dz];
                    if (a.Exists != b.Exists) {
                        float interpolateY = Interpolate(a.Density, b.Density);
                        transitions.Add(point.Coordinates + new Vector3(dx, interpolateY, dz));
                    }
                }
            }
            // Along z axis
            for (int dx = 0; dx < 2; ++dx) {
                for (int dy = 0; dy < 2; ++dy) {
                    Point a = octet[dx, dy, 0];
                    Point b = octet[dx, dy, 1];
                    if (a.Exists != b.Exists) {
                        float interpolateZ = Interpolate(a.Density, b.Density);
                        transitions.Add(point.Coordinates + new Vector3(dx, dy, interpolateZ));
                    }
                }
            }

            if (transitions.Count == 0) {
                return new Vector3();
            }

            foreach (Vector3 transition in transitions) {
                
            }
            return QuadraticError();
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