using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace TerrainGenerator { 

    [ExecuteInEditMode]
    public class TerrainGenerator : MonoBehaviour {
        [Range(0.0f, 1.0f)]
        public float level = 0.5f;
        [Range(0.05f, 0.2f)]
        public float scale = 0.1f;
        public Vector3 size = new Vector3(16, 16, 16);

        bool settingsUpdated;
        Queue<Point> points = new Queue<Point>();
        GameObject pointHolder;
        const string pointHolderName = "Point Holder";

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
                oldPoints[i].gameObject.SetActive(true);
                DestroyImmediate(oldPoints[i].gameObject, false);
            }
        }

        void InstantiatePoint(int x, int y, int z) {
            Point point;
            Vector3Int coordinates;
            float sampleNoise;

            coordinates = new Vector3Int(x, y, z);
            sampleNoise = noise.snoise(new float3(x, y, z) * scale);
            if (sampleNoise > level) {
                point = Point.CreatePoint(coordinates);
                point.transform.parent = pointHolder.transform;
                point.transform.localScale *= sampleNoise * math.sqrt(2);
                points.Enqueue(point);
            }
        }

        void GenerateMesh() {
            CreatePointHolder();
            for (int x = 0; x < size.x; ++x) {
                for (int y = 0; y < size.y; ++y) {
                    for (int z = 0; z < size.z; ++z) {
                        InstantiatePoint(x, y, z);
                    }
                }
            }
        }

        void CreatePointHolder() {
            if (pointHolder == null) {
                if (GameObject.Find(pointHolderName)) {
                    pointHolder = GameObject.Find(pointHolderName);
                } else {
                    pointHolder = new GameObject(pointHolderName);
                }
            }
        }
    }
}