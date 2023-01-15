using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace TerrainGenerator {
    public class DensityData : IDensityData {
        protected readonly Dictionary<int3, GridPoint> pointDensityData = new();
        protected readonly int size;

        public DensityData(int size) {
            this.size = size;
        }

        public Dictionary<int3, GridPoint> PointDensityData { get { return pointDensityData; } }
        public int Size { get { return size; } }

        public void SetPointDensity(int3 grid, GridPoint point) {
            pointDensityData[grid] = point;
        }

        public IEnumerable<int3> Points(bool includeEdges = false) {
            int _size = size;
            if (includeEdges) {
                _size++;
            }
            for (int x = 0; x <= _size; ++x) {
                for (int y = 0; y <= _size; ++y) {
                    for (int z = 0; z <= _size; ++z) {
                        yield return new int3(x, y, z);
                    }
                }
            }
        }
    }
}
