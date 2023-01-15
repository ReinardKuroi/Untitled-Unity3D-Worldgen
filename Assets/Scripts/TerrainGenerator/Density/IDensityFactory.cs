using System;
using UnityEngine;
using Unity.Mathematics;

namespace TerrainGenerator {

    public interface IDensityFactory {
        IDensityData DensityData { get; }

        void PopulateDensityData();
    }

    public class DensityGenerator : IDensityFactory {
        protected readonly IDensityData densityData;
        protected readonly Func<Vector3, float> densityFunction;
        public IDensityData DensityData { get { return densityData; } }

        public void PopulateDensityData() {
            foreach (int3 gridCoordinates in DensityData.Points(includeEdges: true)) {
                float density = densityFunction(new Vector3Int(gridCoordinates.x, gridCoordinates.y, gridCoordinates.z));
                DensityData.SetPointDensity(gridCoordinates, new GridPoint(gridCoordinates, density));
            }
        }
    }
}
