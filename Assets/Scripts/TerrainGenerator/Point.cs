using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator {
    public class Point {
        public Vector3Int Coordinates { get; private set; }
        public float Level { get; private set; }
        public GameObject GameObject { get; set; }
        public int Hash { get { return (Coordinates.x * 73856093 ^ Coordinates.y * 19349663 ^ Coordinates.z * 83492791) % System.Int32.MaxValue; } }
        private readonly string prefabName = "Prefabs/Point (black)";

        public Point(Vector3Int coordinates, float level) {
            this.Coordinates = coordinates;
            this.Level = level;
        }

        public GameObject LoadPrefab() {
            return Resources.Load<GameObject>(prefabName);
        }
    }
}
