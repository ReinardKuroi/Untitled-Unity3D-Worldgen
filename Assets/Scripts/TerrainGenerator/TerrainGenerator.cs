using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Threading;
using UnityEngine.UI;

namespace TerrainGenerator { 

    [ExecuteInEditMode]
    public class TerrainGenerator : MonoBehaviour {
        public bool updateInEditMode = false;
        public bool updateInPlayMode = false;

        [Header("Map dimensions")]
        public Vector3Int mapStart = new();
        public Vector3Int mapEnd = new();
        [Header("Map seed")]
        public int inputMapSeed = 0;
        private int mapSeed;
        [SerializeField]
        Vector3 mapOffset = new();
        [Header("Chunk parameters")]
        [Range(0, 100)]
        public int seaLevel = 50;
        public PerlinNoiseParameters noiseParameters = new();
        public int chunkSize = 16;
        public Material material;

        const string chunkRootName = "Chunk Root";
        GameObject chunkRoot;
        readonly Queue<Chunk> deadChunks = new();
        Dictionary<Vector3Int, Chunk> existingChunks = new();
        bool settingsUpdated;
        [Header("Dynamic Update Settings")]
        public bool DynamicGeneration = false;
        public float renderDistance = 50f;
        public Transform viewPoint;

        private const int MAX_THREADS = 8;

        private void Awake() {
            UpdateMesh();
        }

        void Update() {
            if (settingsUpdated || Application.isPlaying && DynamicGeneration) {
                UpdateMesh();
                settingsUpdated = false;
            }
        }

        void UpdateMesh() {
            if (Application.isPlaying && updateInPlayMode || (!Application.isPlaying && updateInEditMode)) {
                SetMapSeed();
                SetMapOffset();
                CreateChunkRoot();
                GenerateMesh();
                SetChunkRootTransform();
            }
        }

        private void OnValidate() {
            settingsUpdated = true;
        }

        void SetMapSeed() {
            if (inputMapSeed == 0) {
                System.Random random = new();
                mapSeed = random.Next();
            } else {
                mapSeed = inputMapSeed;
            }
        }

        void SetMapOffset() {
            System.Random seededMapCoords = new(mapSeed);
            mapOffset = new Vector3Int(seededMapCoords.Next() % (2 << 16), seededMapCoords.Next() % (2 << 16), seededMapCoords.Next() % (2 << 16));
        }

        void GenerateMesh() {
            if (DynamicGeneration) {
                GenerateDynamicMesh();
            } else {
                GenerateStaticMesh();
            }
        }

        void GenerateDynamicMesh() {
            int chunkRenderDistance = Mathf.RoundToInt(renderDistance / chunkSize);
            Vector3Int currentChunk = Vector3Int.FloorToInt(viewPoint.position / chunkSize);

            Text debug = GameObject.Find("Debug").GetComponent<Text>();
            debug.text = $"View Point: {viewPoint.position}\n" +
                $"Current Chunk: {currentChunk}\n" +
                $"Chunk Render Distance: {chunkRenderDistance}";

            Dictionary<Chunk, AdaptiveContour> chunkGenerators = new();

            for (int dx = -chunkRenderDistance; dx <= chunkRenderDistance; ++dx) {
                for (int dy = -chunkRenderDistance; dy <= chunkRenderDistance; ++dy) {
                    for (int dz = -chunkRenderDistance; dz <= chunkRenderDistance; ++dz) {
                        Vector3Int chunkPosition = currentChunk + new Vector3Int(dx, dy, dz);
                        if (existingChunks.ContainsKey(chunkPosition)) {
                            existingChunks[chunkPosition].gameObject.SetActive(true);
                        } else {
                            Chunk chunk = InitChunk(chunkPosition);
                            Vector3 chunkOffset = chunk.Coordinates * chunk.Size;
                            float radius = chunkSize * Mathf.Sqrt(3) * (mapEnd - mapStart).magnitude * 0.5f / Mathf.PI;
                            float SampleFunction(Vector3 x) =>
                                SphereDensity(x, chunkOffset, radius)
                                + 0.5f * Perlin(x + chunkOffset + mapOffset, noiseParameters)
                                - seaLevel * 0.01f;
                            chunkGenerators[chunk] = new AdaptiveContour(SampleFunction, chunkSize);
                            existingChunks[chunkPosition] = chunk;
                        }
                    }
                }
            }

            Stack<Thread> contouringThreads = new();
            Stack<Thread> activeContouringThreads = new();

            foreach (Chunk chunk in chunkGenerators.Keys) {
                Thread thread = new(() => {
                    try {
                        chunkGenerators[chunk].RunContouring();
                    }
                    catch (Exception exc) {
                        Debug.LogError(exc);
                    }
                });
                contouringThreads.Push(thread);
                thread.Name = $"{chunk.name}";
            }


            while (contouringThreads.Count > 0) {
                while (activeContouringThreads.Count < MAX_THREADS) {
                    if (contouringThreads.TryPop(out Thread thread)) {
                        thread.Start();
                        Debug.Log($"Thread {thread.Name} started");
                        activeContouringThreads.Push(thread);
                    } else {
                        break;
                    }
                }

                while (activeContouringThreads.Count > 0) {
                    Thread thread = activeContouringThreads.Pop();
                    thread.Join();
                    Debug.Log($"Thread {thread.Name} finished");
                }
            }

            foreach (Chunk chunk in chunkGenerators.Keys) {
                chunkGenerators[chunk].SetMesh(chunk.mesh);
                if (chunk.mesh.vertexCount == 0 || chunk.mesh.triangles.Length == 0) {
                    chunk.gameObject.SetActive(false);
                }
            }

            foreach (Vector3Int chunkPosition in existingChunks.Keys) {
                if ((existingChunks[chunkPosition].Center - viewPoint.position).magnitude > renderDistance) {
                    existingChunks[chunkPosition].gameObject.SetActive(false);
                }
            }
        }

        void GenerateStaticMesh () {
            foreach (Chunk deadChunk in new List<Chunk>(FindObjectsOfType<Chunk>())) {
                if (deadChunk.Disable()) {
                    deadChunks.Enqueue(deadChunk);
                }
            }

            Dictionary<Chunk, AdaptiveContour> chunkGenerators = new();


            foreach (Vector3Int chunkPosition in IterateOverChunkGrid()) {
                Chunk chunk = InitChunk(chunkPosition);

                Vector3 chunkOffset = chunk.Coordinates * chunk.Size;
                float radius = chunkSize * Mathf.Sqrt(3) * (mapEnd - mapStart).magnitude * 0.5f / Mathf.PI;
                float SampleFunction(Vector3 x) =>
                    SphereDensity(x, chunkOffset, radius)
                    + 0.5f * Perlin(x + chunkOffset + mapOffset, noiseParameters)
                    - seaLevel * 0.01f;
                chunkGenerators[chunk] = new AdaptiveContour(SampleFunction, chunkSize);
            }

            Stack<Thread> contouringThreads = new();
            Stack<Thread> activeContouringThreads = new();

            foreach (Chunk chunk in chunkGenerators.Keys) {
                Thread thread = new(() =>
                {
                    try {
                        chunkGenerators[chunk].RunContouring();
                    } catch (Exception exc) {
                        Debug.LogError(exc);
                    }
                });
                contouringThreads.Push(thread);
                thread.Name = $"{chunk.name}";
            }


            while (contouringThreads.Count > 0) {
                while (activeContouringThreads.Count < MAX_THREADS) {
                    if (contouringThreads.TryPop(out Thread thread)) {
                        thread.Start();
                        Debug.Log($"Thread {thread.Name} started");
                        activeContouringThreads.Push(thread);
                    } else {
                        break;
                    }
                }

                while (activeContouringThreads.Count > 0) {
                    Thread thread = activeContouringThreads.Pop();
                    thread.Join();
                    Debug.Log($"Thread {thread.Name} finished");
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
                GameObject chunkObject = new();
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
            chunk.Center = chunk.transform.position + chunk.Size * Mathf.Sqrt(3) * Vector3.one / 2;
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
                    //Rotator rotator = chunkRoot.AddComponent<Rotator>();
                    //rotator.speed = chunkSize * 3f / 60f;
                }
            }
        }

        void SetChunkRootTransform() {
            chunkRoot.transform.SetPositionAndRotation(transform.position, transform.rotation);
        }

        float SphereDensity(Vector3 x, Vector3 offset, float rad) {
            x += offset;
            return 1/(1 + Mathf.Exp(x.magnitude - rad)) - 0.5f;
        }

        float Perlin(Vector3 point, PerlinNoiseParameters parameters) {
            float amplitude = 1f;
            float cumulativeNoise = 0f;
            float cumulativeAmplitude = 0f;

            float frequency = parameters.frequency / 100f;
            float persistence = parameters.persistence;
            float lacunarity = parameters.lacunarity;
            int octaves = parameters.octaves;

            for (int i = 0; i < octaves; ++i) {
                cumulativeNoise = noise.snoise(point * frequency) * amplitude;
                cumulativeAmplitude += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return cumulativeNoise / cumulativeAmplitude;
        }
    }

    [System.Serializable]
    public struct PerlinNoiseParameters {
        [Range(0, 100)]
        public float frequency;
        [Range(0, 10)]
        public float persistence;
        [Range(0, 10)]
        public float lacunarity;
        [Range(0, 10)]
        public int octaves;
    }
}