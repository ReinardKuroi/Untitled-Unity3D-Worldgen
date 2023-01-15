using Unity.Mathematics;

namespace TerrainGenerator {
    public class GridPoint {
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
}