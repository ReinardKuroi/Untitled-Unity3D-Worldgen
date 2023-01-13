using System;
using Unity.Mathematics;
using UnityEngine;

namespace TerrainGenerator {
    public class AdaptiveContourCompute : AdaptiveContour {
        public AdaptiveContourCompute(Func<Vector3, float> densityFunction, int size) : base(densityFunction, size) { }
        public new void PopulateDensityData() {
            foreach (int3 gridCoordinates in Volume(size, includeEdges: true)) {
                float density = DensityFunction(new Vector3Int(gridCoordinates.x, gridCoordinates.y, gridCoordinates.z));
                pointDensityData[gridCoordinates] = new GridPoint(gridCoordinates, density);
            }
        }
    }
}
