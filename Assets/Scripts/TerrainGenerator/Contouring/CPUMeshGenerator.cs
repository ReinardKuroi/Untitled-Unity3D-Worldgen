using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace TerrainGenerator {
    public abstract class CPUMeshGenerator<T> where T : DensityData {
        protected T densityData;
        public T DensityData { get { return densityData; } set { densityData = value; } }
        protected readonly List<int> faces = new();
        protected readonly List<Vector3> vertices = new();
        protected readonly Dictionary<int3, int> vertexIndices = new();

        public abstract void Free();

        public abstract void CreateMesh();

        public void SetMesh(Mesh mesh) {
            mesh.Clear();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = faces.ToArray();
            mesh.RecalculateNormals();
            mesh.Optimize();
        }

        protected void GenerateFaceAlongAxis(int3 coordinates, int axis) {
            int3 offsetCoordinates = coordinates + SpatialTools.axisVectors[axis];
            if (offsetCoordinates.x == 0 || offsetCoordinates.y == 0 || offsetCoordinates.z == 0) {
                return;
            }
            bool inside = densityData.PointDensityData[coordinates].Exists;
            bool outside = densityData.PointDensityData[offsetCoordinates].Exists;
            if (inside != outside) {
                int[] quad = GenerateQuad(coordinates, axis);
                if (outside) {
                    Array.Reverse(quad);
                }
                faces.AddRange(quad);
            }
        }

        protected void GenerateFaces() {
            foreach (int3 coordinates in densityData.Points(includeEdges: false)) {
                GenerateFacesAtGridCoordinates(coordinates);
            }
        }

        protected void GenerateFacesAtGridCoordinates(int3 coordinates) {
            for (int axis = 0; axis < SpatialTools.axisVectors.Length; ++axis) {
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
                int3 index = coordinates - SpatialTools.cubicOffsets[axis, i];
                quad[i] = vertexIndices[index];
            }

            return quad;
        }

        protected class Octet {
            public readonly int3 coordinates;
            public readonly GridPoint[] points;
            public Vector3 vertex;

            public Octet(int3 coordinates, T densityData) {
                this.coordinates = coordinates;
                points = GetNeighbouringOctet(densityData.PointDensityData);
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