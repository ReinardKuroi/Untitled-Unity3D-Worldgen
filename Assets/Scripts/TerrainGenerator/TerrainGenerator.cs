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
        public Vector3Int size = new Vector3Int(16, 16, 16);

        bool settingsUpdated;
        Dictionary<int, Point> points = new Dictionary<int, Point>();
        Queue<Point> pointsToCreate = new Queue<Point>();
        Queue<Point> pointsToDestroy = new Queue<Point>();
        GameObject pointHolder;
        const string pointHolderName = "Point Holder";
        delegate float SamplePoint(float3 coordinates);
        SamplePoint sampleFunction = noise.snoise;

        void Update() {
            if (settingsUpdated) {
                GenerateMesh();
                settingsUpdated = false;
            }
            CreatePointQueue();
            EmptyDestroyQueue();
        }

        private void OnValidate() {
            settingsUpdated = true;
        }

        void EmptyDestroyQueue() {
            while (pointsToDestroy.Count > 0) {
                Point point = pointsToDestroy.Dequeue();
                DestroyImmediate(point.GameObject, false);
            }
        }

        void InstantiatePoint(Vector3Int coordinates) {
            Point point;
            float sampleNoise;

            sampleNoise = sampleFunction((Vector3)coordinates * scale) - level;
            point = new Point(coordinates, sampleNoise);
            pointsToCreate.Enqueue(point);
        }

        void CreatePointQueue() { 
            while (pointsToCreate.Count > 0) {
                Point point = pointsToCreate.Dequeue();
                int hash = point.Hash;
                if (point.Exists) {
                    if (!points.ContainsKey(hash)) {
                        points[hash] = point;
                        point.GameObject = Instantiate<GameObject>(point.LoadPrefab(), point.Coordinates, Quaternion.identity);
                        point.GameObject.name = $"Point ({point.Coordinates.x} {point.Coordinates.y} {point.Coordinates.z}) #{point.Hash}";
                        point.GameObject.transform.parent = pointHolder.transform;
                    } else {
                        point = points[hash];
                    }
                    point.GameObject.transform.localScale.Set(point.Density, point.Density, point.Density);
                } else {
                    if (points.ContainsKey(hash)) {
                        pointsToDestroy.Enqueue(points[hash]);
                        points.Remove(hash);
                    }
                }
            }
        }

        IEnumerable<Vector3Int> Volume() {
            for (int x = 0; x < size.x; ++x) {
                for (int y = 0; y < size.y; ++y) {
                    for (int z = 0; z < size.z; ++z) {
                        yield return new Vector3Int(x, y, z);
                    }
                }
            }
        }

        void GenerateMesh() {
            CreatePointHolder();
            foreach (Vector3Int coordinates in Volume()) { InstantiatePoint(coordinates); }
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