using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator { 

    [ExecuteInEditMode]
    public class TerrainGenerator : MonoBehaviour {
        public double level = 0.5;
        public Vector3 size = new Vector3(16, 16, 16);

        bool settingsUpdated;
        Queue<Point> points = new Queue<Point>();

        void Awake() {
            print("Awake");
            DestroyMesh();
        }

        void Update() {
            if (settingsUpdated) {
                DestroyMesh();
                GenerateMesh();
                settingsUpdated = false;
            }
        }

        private void OnValidate() {
            settingsUpdated = true;
        }

        void DestroyMesh() {
            while (points.Count > 0) {
                DestroyImmediate(points.Dequeue().gameObject, false);
            }
            Point[] oldPoints = FindObjectsOfType<Point>();
            for (int i = 0; i < oldPoints.Length; ++i) {
                DestroyImmediate(oldPoints[i].gameObject, false);
            }
        }

        void InstantiatePoint(int x, int y, int z) {
            Point point;
            Vector3Int coordinates;
            bool isWhite;

            isWhite = Random.Range(0f, 1f) > 0.5f;
            coordinates = new Vector3Int(x, y, z);
            point = Point.CreatePoint(coordinates, isWhite);
            point.transform.parent = transform;
            points.Enqueue(point);
        }

        void GenerateMesh() {
            for (int x = 0; x < size.x; ++x) {
                for (int y = 0; y < size.y; ++y) {
                    for (int z = 0; z < size.z; ++z) {
                        InstantiatePoint(x, y, z);
                    }
                }
            }
        }
    }
}