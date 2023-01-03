using UnityEngine;

namespace TerrainGenerator {
    class Point {
        public readonly Vector3 coordinates;
        public readonly float density;
        public bool Exists { get { return density > 0; } }

        public Point(Vector3 coordinates, float density) {
            this.coordinates = coordinates;
            this.density = density;
        }
    }
}
