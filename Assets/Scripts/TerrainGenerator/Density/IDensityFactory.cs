using System;
using UnityEngine;
using Unity.Mathematics;

namespace TerrainGenerator {

    public interface IDensityFactory<T> where T : IDensityData {
        T DensityData { get; }

        void PopulateDensityData();
    }

    public class DensityGenerator : IDensityFactory<DensityData> {
        protected readonly DensityData densityData;
        protected readonly Func<Vector3, float> densityFunction;
        public DensityData DensityData { get { return densityData; } }

        public void PopulateDensityData() {
            foreach (int3 gridCoordinates in DensityData.Points(includeEdges: true)) {
                float density = densityFunction(new Vector3Int(gridCoordinates.x, gridCoordinates.y, gridCoordinates.z));
                DensityData.SetPointDensity(gridCoordinates, new GridPoint(gridCoordinates, density));
            }
        }
    }
}
