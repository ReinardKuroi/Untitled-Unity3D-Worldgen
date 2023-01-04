using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace TerrainGenerator { 

    [ExecuteInEditMode]
    public class TerrainGenerator : MonoBehaviour {
        public bool updateInEditMode = true;
        [Header("Map dimensions")]
        public Vector3Int mapStart = new Vector3Int();
        public Vector3Int mapEnd = new Vector3Int();
        [Header("Chunk parameters")]
        [Range(0.0f, 1.0f)]
        public float level = 0.5f;
        [Range(0.01f, 0.2f)]
        public float scale = 0.1f;
        public Vector3Int size = new Vector3Int(16, 16, 16);

        const string chunkRootName = "Chunk Root";
        GameObject chunkRoot;
        Queue<Chunk> deadChunks = new Queue<Chunk>();
        Queue<Chunk> aliveChunks = new Queue<Chunk>();
        bool settingsUpdated;
        private bool DynamicGeneration = false;

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

        void GenerateMesh() {
            if (DynamicGeneration) {
                throw new NotImplementedException("Dynamic render not implemented!");
            }

            CreateChunkRoot();

            foreach (Chunk deadChunk in new List<Chunk>(FindObjectsOfType<Chunk>())) {
                deadChunk.Disable();
                if (Application.isPlaying) {
                    deadChunks.Enqueue(deadChunk);
                }
            }

            Material material = Resources.Load<Material>("Prefabs/White");

            for (int vectorX = mapStart.x; vectorX < mapEnd.x; ++vectorX) {
                for (int vectorY = mapStart.y; vectorY < mapEnd.y; ++vectorY) {
                    for (int vectorZ = mapStart.z; vectorZ < mapEnd.z; ++vectorZ) {
                        Chunk chunk;
                        if (deadChunks.Count > 0) {
                            chunk = deadChunks.Dequeue();
                            chunk.gameObject.SetActive(true);
                        } else {
                            GameObject chunkObject = new GameObject();
                            chunk = chunkObject.GetComponent<Chunk>();
                            if (chunk == null) {
                                chunk = chunkObject.AddComponent<Chunk>();
                            }
                            chunk.Setup(material);
                        }
                        chunk.Coordinates = new Vector3Int(vectorX, vectorY, vectorZ);
                        chunk.Size = size;
                        chunk.name = $"Chunk ({chunk.Coordinates})";
                        chunk.transform.position = chunk.Coordinates * chunk.Size;
                        chunk.transform.parent = chunkRoot.transform;
                        //float SampleFunction(Vector3 x) => PerlinOffset(x, chunk.Coordinates * chunk.Size);
                        float SampleFunction(Vector3 x) => Perlin2D(x, chunk.Coordinates * chunk.Size);
                        AdaptiveContour generator = new AdaptiveContour(SampleFunction, size);
                        chunk.mesh = generator.RunContouring(chunk.mesh);
                    }
                }
            }
        }

        void CreateChunkRoot() {
            if (chunkRoot == null) {
                if (GameObject.Find(chunkRootName)) {
                    chunkRoot = GameObject.Find(chunkRootName);
                } else {
                    chunkRoot = new GameObject(chunkRootName);
                }
            }
        }

        float PerlinOffset(Vector3 x, Vector3 offset) {
            x += offset;
            return (noise.snoise(x * scale) + 1) / 2 - level;
        }

        float Perlin2D(Vector3 x, Vector3 offset) {
            x += offset;
            return (noise.snoise(new float2(x.x, x.z) * scale) - ((x.y / size.y * size.y / 1.5f) - size.y * 0.2f) + 1) / 2 - level;
        }
    }
}