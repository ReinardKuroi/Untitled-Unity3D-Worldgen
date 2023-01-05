using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Threading;

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
        public Vector3Int chunkSize = new Vector3Int(16, 16, 16);
        public Material material;

        const string chunkRootName = "Chunk Root";
        GameObject chunkRoot;
        Queue<Chunk> deadChunks = new Queue<Chunk>();
        bool settingsUpdated;
        private bool DynamicGeneration = false;
        private const int MAX_THREADS = 8;

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
                if (deadChunk.Disable()) {
                    deadChunks.Enqueue(deadChunk);
                }
            }

            Dictionary<Chunk, AdaptiveContour> chunkGenerators = new();


            foreach (Vector3Int chunkPosition in IterateOverChunkGrid()) {
                Chunk chunk = InitChunk(chunkPosition);

                float SampleFunction(Vector3 x) =>
                    SphereDensity(x, chunk.Coordinates * chunk.Size, chunkSize.magnitude * (mapEnd - mapStart).magnitude * 0.5f / Mathf.PI)
                    + Perlin(x, chunk.Coordinates * chunk.Size, noiseParameters);
                chunkGenerators[chunk] = new AdaptiveContour(SampleFunction, chunkSize);
            }

            Stack<Thread> contouringThreads = new();
            Stack<Thread> activeContouringThreads = new();

            foreach (Chunk chunk in chunkGenerators.Keys) {
                Thread thread = new Thread(chunkGenerators[chunk].RunContouring);
                contouringThreads.Push(thread);
                thread.Name = $"{chunk.name}";
            }


            while (contouringThreads.Count > 0) {
                while (activeContouringThreads.Count < MAX_THREADS) {
                    if (contouringThreads.TryPop(out Thread thread)) {
                        thread.Start();
                        print($"Thread {thread.Name} started");
                        activeContouringThreads.Push(thread);
                    } else {
                        break;
                    }
                }

                while (activeContouringThreads.Count > 0) {
                    Thread thread = activeContouringThreads.Pop();
                    thread.Join();
                    print($"Thread {thread.Name} finished");
                }
            }

            foreach (Chunk chunk in chunkGenerators.Keys) {
                chunkGenerators[chunk].SetMesh(chunk.mesh);
                DisableChunkIfEmpty(chunk);
            }
        }

        IEnumerable<Vector3Int> IterateOverChunkGrid() {
            for (int x = mapStart.x; x < mapEnd.x; ++x) {
                for (int y = mapStart.y; y < mapEnd.y; ++y) {
                    for (int z = mapStart.z; z < mapEnd.z; ++z) {
                        yield return new Vector3Int(x, y, z);
                    }
                }
            }
        }

        Chunk InitChunk(Vector3Int coordinates) {
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
            chunk.Coordinates = coordinates;
            chunk.Size = chunkSize;
            chunk.name = $"Chunk ({chunk.Coordinates})";
            chunk.transform.parent = chunkRoot.transform;
            chunk.transform.localPosition = chunk.Coordinates * chunk.Size;
            chunk.transform.localRotation = Quaternion.identity;
            return chunk;
        }

        void DisableChunkIfEmpty(Chunk chunk) {
            if (chunk.mesh.vertexCount == 0 || chunk.mesh.triangles.Length == 0) {
                if (chunk.Disable()) {
                    deadChunks.Enqueue(chunk);
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
                    rotator.speed = chunkSize.magnitude * 3f / 60f;
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