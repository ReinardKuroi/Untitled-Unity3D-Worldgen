using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace TerrainGenerator {
    public abstract class CPUMeshGenerator : IMeshGenerator {
        protected static readonly int3[] axisVectors = new int3[3] { new(0, 0, 1), new(0, 1, 0), new(1, 0, 0) };
        protected static readonly int3[,] cubicOffsets = new int3[3, 6] {
            {
                // along z
                new(0, 0, 0),
                new(1, 0, 0),
                new(0, 1, 0),
                new(1, 0, 0),
                new(1, 1, 0),
                new(0, 1, 0)
            },
            {
                // along y
                new(0, 0, 0),
                new(0, 0, 1),
                new(1, 0, 0),
                new(0, 0, 1),
                new(1, 0, 1),
                new(1, 0, 0)
            },            {
                // along x
                new(0, 0, 0),
                new(0, 1, 0),
                new(0, 0, 1),
                new(0, 1, 0),
                new(0, 1, 1),
                new(0, 0, 1)
            }
        };
        protected readonly Func<Vector3, float> densityFunction;
        protected readonly Dictionary<int3, GridPoint> pointDensityData = new();
        protected readonly List<int> faces = new();
        protected readonly List<Vector3> vertices = new();
        protected readonly Dictionary<int3, int> vertexIndices = new();
        protected readonly int size;

        public CPUMeshGenerator(Func<Vector3, float> densityFunction, int size) {
            this.densityFunction = densityFunction;
            this.size = size;
        }

        public abstract void Free();

        public abstract void Run();

        public void SetMesh(Mesh mesh) {
            mesh.Clear();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = faces.ToArray();
            mesh.RecalculateNormals();
            mesh.Optimize();
        }

        protected float DensityFunction(Vector3 x) {
            return densityFunction(x);
        }

        protected void GenerateFaceAlongAxis(int3 coordinates, int axis) {
            int3 offsetCoordinates = coordinates + axisVectors[axis];
            if (offsetCoordinates.x == 0 || offsetCoordinates.y == 0 || offsetCoordinates.z == 0) {
                return;
            }
            bool inside = pointDensityData[coordinates].Exists;
            bool outside = pointDensityData[offsetCoordinates].Exists;
            if (inside != outside) {
                int[] quad = GenerateQuad(coordinates, axis);
                if (outside) {
                    Array.Reverse(quad);
                }
                faces.AddRange(quad);
            }
        }

        protected void GenerateFaces() {
            foreach (int3 coordinates in Volume(size, includeEdges: false)) {
                GenerateFacesAtGridCoordinates(coordinates);
            }
        }

        protected void GenerateFacesAtGridCoordinates(int3 coordinates) {
            for (int axis = 0; axis < axisVectors.Length; ++axis) {
                GenerateFaceAlongAxis(coordinates, axis);
            }
        }

        protected void GenerateVertex(Octet gridCell) {
            vertices.Add(gridCell.vertex);
            vertexIndices[gridCell.coordinates] = vertices.Count - 1;
        }

        protected int[] GenerateQuad(int3 coordinates, int axis) {
            int[] quad = new int[6];

            for (int i = 0; i < quad.Length; ++i) {
                int3 index = coordinates - cubicOffsets[axis, i];
                quad[i] = vertexIndices[index];
            }

            return quad;
        }

        protected void PopulateDensityData() {
            foreach (int3 gridCoordinates in Volume(size, includeEdges: true)) {
                float density = DensityFunction(new Vector3Int(gridCoordinates.x, gridCoordinates.y, gridCoordinates.z));
                pointDensityData[gridCoordinates] = new GridPoint(gridCoordinates, density);
            }
        }

        protected IEnumerable<int3> Volume(int size, bool includeEdges = true) {
            if (includeEdges) {
                ++size;
            }
            for (int x = 0; x <= size; ++x) {
                for (int y = 0; y <= size; ++y) {
                    for (int z = 0; z <= size; ++z) {
                        yield return new int3(x, y, z);
                    }
                }
            }
        }
        protected class GridPoint {
            public readonly int3 coordinates;
            public readonly float density;
            public bool Exists { get { return density > 0; } }

            public GridPoint(int3 coordinates, float density) {
                this.coordinates = coordinates;
                this.density = density;
            }

            public override int GetHashCode() {
                return coordinates.GetHashCode();
            }
        }

        protected class Octet {
            public readonly int3 coordinates;
            public readonly GridPoint[] points;
            public Vector3 vertex;

            public Octet(int3 coordinates, Dictionary<int3, GridPoint> pointDensityData) {
                this.coordinates = coordinates;
                points = GetNeighbouringOctet(pointDensityData);
            }

            public static int IndexFromCoords(int x, int y, int z) {
                return x * 4 + y * 2 + z;
            }

            protected float Interpolate(float a, float b) {
                return a / (a - b);
            }

            private GridPoint[] GetNeighbouringOctet(Dictionary<int3, GridPoint> pointDensityData) {
                GridPoint[] octet = new GridPoint[8];
                for (int dx = 0; dx < 2; ++dx) {
                    for (int dy = 0; dy < 2; ++dy) {
                        for (int dz = 0; dz < 2; ++dz) {
                            int3 offset = new(dx, dy, dz);
                            octet[IndexFromCoords(dx, dy, dz)] = pointDensityData[coordinates + offset];
                        }
                    }
                }
                return octet;
            }
        }
    }
}