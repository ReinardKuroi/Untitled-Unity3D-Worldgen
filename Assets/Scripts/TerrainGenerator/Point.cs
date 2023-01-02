using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator {
    public class Point {
        public Vector3Int Coordinates { get; private set; }
        public float Density { get; private set; }
        public GameObject GameObject { get; set; }
        public int Hash { get; private set; }
        private readonly string prefabName = "Prefabs/Point (black)";

        public Point(Vector3Int coordinates, float density) {
            this.Coordinates = coordinates;
            this.Density = density;
            this.Hash = CalculateHash(coordinates.x, coordinates.y, coordinates.z);
        }

        public GameObject LoadPrefab() {
            return Resources.Load<GameObject>(prefabName);
        }

        public static int CalculateHash(int x, int y, int z) {
            return (x * 73856093 ^ y * 19349663 ^ z * 83492791) % System.Int32.MaxValue;
        }
    }
}
