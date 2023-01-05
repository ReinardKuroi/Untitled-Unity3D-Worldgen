using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace TerrainGenerator { 

    [ExecuteInEditMode]
    public class TerrainGenerator : MonoBehaviour {
        public bool updateInEditMode = false;
        public bool updateInPlayMode = false;

        [Header("Map dimensions")]
        public Vector3Int mapStart = new Vector3Int();
        public Vector3Int mapEnd = new Vector3Int();
        [Header("Map seed")]
        public int mapSeed = 0;
        Vector3 mapOffset = new();
        [Header("Chunk parameters")]
        [SerializeField]
        [Range(0f, 1f)]
        public float seaLevel = 0.5f;
        public PerlinNoiseParameters noiseParameters = new();
        public Vector3Int size = new Vector3Int(16, 16, 16);

        const string chunkRootName = "Chunk Root";
        GameObject chunkRoot;
        Queue<Chunk> deadChunks = new Queue<Chunk>();
        bool settingsUpdated;
        private bool DynamicGeneration = false;

        private void Awake() {
            UpdateMesh();
        }

        void Update() {
            if (settingsUpdated) {
                UpdateMesh();
                settingsUpdated = false;
            }
        }

        void UpdateMesh() {
            if (Application.isPlaying && updateInPlayMode || (!Application.isPlaying && updateInEditMode)) {
                SetMapSeed();
                SetMapOffset();
                GenerateMesh();
            }
        }

        private void OnValidate() {
            settingsUpdated = true;
        }

        void SetMapSeed() {
            if (mapSeed == 0) {
                System.Random random = new();
                mapSeed = random.Next();
            }
        }

        void SetMapOffset() {
            System.Random seededMapCoords = new(mapSeed);
            mapOffset = new Vector3Int(seededMapCoords.Next() % 2 << 16, seededMapCoords.Next() % 2 << 16, seededMapCoords.Next() % 2 << 16);
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
                        chunk.transform.parent = chunkRoot.transform;
                        chunk.transform.localPosition = chunk.Coordinates * chunk.Size;
                        chunk.transform.localRotation = Quaternion.identity;
                        //float SampleFunction(Vector3 x) => PerlinOffset(x, chunk.Coordinates * chunk.Size);
                        //float SampleFunction(Vector3 x) => Perlin2D(x, chunk.Coordinates * chunk.Size);
                        /*float SampleFunction(Vector3 x) => PerlinOffset(x, chunk.Coordinates * chunk.Size)
                            * (SphereDensity(x, chunk.Coordinates * chunk.Size, 42)
                            + SphereDensity(x, chunk.Coordinates * chunk.Size, 35));*/
                        float SampleFunction(Vector3 x) => SphereDensity(x, chunk.Coordinates * chunk.Size, size.magnitude * (mapEnd - mapStart).magnitude * 0.5f / Mathf.PI)
                            + Perlin(x, chunk.Coordinates * chunk.Size, noiseParameters);
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
                    Rotator rotator = chunkRoot.AddComponent<Rotator>();
                    rotator.speed = size.magnitude * 3f / 60f;
                }
            }
        }

        float SphereDensity(Vector3 x, Vector3 offset, float rad) {
            x += offset;
            return 1/(1 + Mathf.Exp(x.magnitude - rad)) - 0.5f;
        }

        float Perlin(Vector3 x, Vector3 offset, PerlinNoiseParameters parameters) {
            float cumulativeNoise = 0;
            float cumulativeAmplitude = 0;

            float frequency = parameters.frequency;
            float persistence = parameters.persistence;
            float lacunarity = parameters.lacunarity;
            float amplitude = parameters.amplitude;
            int octaves = parameters.octaves;

            x = x + offset + mapOffset;

            for (int i = 0; i < octaves; ++i) {
                cumulativeNoise = noise.snoise(x * frequency) * amplitude;
                cumulativeAmplitude += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return cumulativeNoise * 0.5f / cumulativeAmplitude - seaLevel;
        }
    }

    [System.Serializable]
    public struct PerlinNoiseParameters {
        public float frequency;
        public float persistence;
        public float lacunarity;
        public float amplitude;
        public int octaves;
    }
}