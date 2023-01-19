using Unity.Mathematics;

namespace TerrainGenerator {
    public class SpatialTools {
        public static readonly int3[] axisVectors = new int3[3] { new(0, 0, 1), new(0, 1, 0), new(1, 0, 0) };
        public static readonly int3[,] cubicOffsets = new int3[3, 6] {
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
    }
}