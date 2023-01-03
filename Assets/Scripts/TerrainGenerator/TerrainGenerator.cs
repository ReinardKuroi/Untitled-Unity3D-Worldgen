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
                GameObject point = Instantiate<GameObject>(pointPrefab);
                point.name = name;
                point.transform.parent = pointHolder.transform;
                point.transform.SetLocalPositionAndRotation(coordinates, Quaternion.identity);
            }
        }

        void GenerateMesh() {
            CreatePointHolder();
            EnqueueDestroyPoints();
            AdaptiveContour generator = new AdaptiveContour(SampleFunction, size);
            generator.PopulateDensityData();
            Mesh mesh = generator.RunContouring();
            pointHolder.transform.position = size * chunkOffset;
            pointHolder.GetComponent<MeshFilter>().mesh = mesh;
        }

        float SampleFunction(Vector3 x) {
            return (sampleFunction((x + chunkOffset * size) * scale) + 1) / 2 - level;
        }

        void CreatePointHolder() {
            if (pointHolder == null) {
                if (GameObject.Find(pointHolderName)) {
                    pointHolder = GameObject.Find(pointHolderName);
                } else {
                    pointHolder = new GameObject(pointHolderName);
                    pointHolder.AddComponent<MeshFilter>();
                    pointHolder.AddComponent<MeshRenderer>();
                    pointHolder.GetComponent<MeshRenderer>().material = Resources.Load<Material>("Prefabs/White");
                }
            }
        }
    }
}