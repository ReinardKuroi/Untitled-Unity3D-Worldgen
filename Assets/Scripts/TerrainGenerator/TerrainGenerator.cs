using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace TerrainGenerator { 

    [ExecuteInEditMode]
    public class TerrainGenerator : MonoBehaviour {
        public bool updateInEditMode = true;
        [Range(0.0f, 1.0f)]
        public float level = 0.5f;
        [Range(0.01f, 0.2f)]
        public float scale = 0.1f;
        public Vector3Int size = new Vector3Int(16, 16, 16);
        public Vector3Int chunkOffset = new Vector3Int();

        Queue<Vector3> pointsToCreate = new Queue<Vector3>();
        Queue<GameObject> points = new Queue<GameObject>();
        Queue<GameObject> pointsToDestroy = new Queue<GameObject>();
        bool settingsUpdated;
        GameObject pointHolder;
        const string pointHolderName = "Point Holder";
        const string prefabName = "Prefabs/Point (black)";
        Func<float3, float> sampleFunction = noise.snoise;

        void Update() {
            if (settingsUpdated) {
                UpdateMesh();
                settingsUpdated = false;
            }

        }

        void UpdateMesh() {
            if (Application.isPlaying || (!Application.isPlaying && updateInEditMode)) {
                GenerateMesh();
                RunDestroyQueue();
                CreatePointQueue();
            }
        }

        private void OnValidate() {
            settingsUpdated = true;
        }

        void RunDestroyQueue() {
            while (pointsToDestroy.Count > 0) {
                GameObject point = pointsToDestroy.Dequeue();
                DestroyImmediate(point, false);
            }
        }

        void EnqueueCreatePoint(Vector3 coordinates) {
            pointsToCreate.Enqueue(coordinates);
        }

        void EnqueueDestroyPoints() {
            for (int i = 0; i < pointHolder.transform.childCount; ++i) {
                GameObject point = pointHolder.transform.GetChild(i).gameObject;
                pointsToDestroy.Enqueue(point);
            }
        }

        GameObject LoadPrefab() {
            return Resources.Load<GameObject>(prefabName);
        }

        void CreatePointQueue() {
            GameObject pointPrefab = LoadPrefab();
            while (pointsToCreate.Count > 0) {
                Vector3 coordinates = pointsToCreate.Dequeue();
                string name = $"Point ({coordinates.x} {coordinates.y} {coordinates.z})";
                GameObject point = Instantiate<GameObject>(pointPrefab, coordinates, Quaternion.identity);
                point.name = name;
                point.transform.parent = pointHolder.transform;
            }
        }

        void GenerateMesh() {
            CreatePointHolder();
            EnqueueDestroyPoints();
            AdaptiveContour generator = new AdaptiveContour(x => (sampleFunction(x * scale) + 1) / 2 - level);
            generator.PopulateDensityData(size, chunkOffset * size);
            foreach (Vector3 point in generator.RunContouring()) {
                EnqueueCreatePoint(point);
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