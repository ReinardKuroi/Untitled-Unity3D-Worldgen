using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace TerrainGenerator {
    public interface IDensityData {
        int Size { get; }
        Dictionary<int3, GridPoint> PointDensityData { get; }

        void SetPointDensity(int3 grid, GridPoint point);

        IEnumerable<int3> Points(bool includeEdges);
    }
}