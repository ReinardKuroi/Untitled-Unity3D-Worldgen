using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
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
        bool settingsUpdated;
        [Header("Dynamic Update Settings")]
        public bool DynamicGeneration = false;
        public float renderDistance = 50f;
        public Transform viewPoint;

        readonly Dictionary<Vector3Int, Chunk> existingChunks = new();
        readonly Stack<Chunk> chunksToCreate = new();
        readonly Stack<Chunk> chunksInProgress = new();
        readonly Stack<Chunk> chunksCompleted = new();
        readonly Stack<Chunk> chunksToDestroy = new();

        readonly ThreadDispatcher threadDispatcher = new();

        private void Awake() {
            UpdateMesh();
        }

        void Update() {
            if (settingsUpdated || Application.isPlaying && DynamicGeneration) {
                UpdateMesh();
                settingsUpdated = false;
            }
            DestroyDeadChunks();
            CreateChunkWorkers();
            threadDispatcher.UpdateThreads();
            SetCompletedChunkMesh();
        }

        void UpdateMesh() {
            if (Application.isPlaying && updateInPlayMode || (!Application.isPlaying && updateInEditMode)) {
                SetMapSeed();
                SetMapOffset();
                CreateChunkRoot();
                SetChunkRootTransform();
                GenerateMesh();
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
                            chunk.chunkGenerator = new AdaptiveContour(SampleFunction, chunkSize);
                            chunksToCreate.Push(chunk);
                            existingChunks[chunkPosition] = chunk;
                        }
                    }
                }
            }

            foreach (Vector3Int chunkPosition in existingChunks.Keys) {
                if ((existingChunks[chunkPosition].Center - viewPoint.position).magnitude > renderDistance) {
                    EnqueueChunkToDestroy(existingChunks[chunkPosition]);
                }
            }
        }

        void GenerateStaticMesh () {
            foreach (Chunk deadChunk in new List<Chunk>(FindObjectsOfType<Chunk>())) {
                deadChunk.Destroy();
            }

            threadDispatcher.Flush();

            foreach (Vector3Int chunkPosition in IterateOverChunkGrid()) {
                Chunk chunk = InitChunk(chunkPosition);

                Vector3 chunkOffset = chunk.Coordinates * chunk.Size;
                float radius = chunkSize * Mathf.Sqrt(3) * (mapEnd - mapStart).magnitude * 0.5f / Mathf.PI;
                float SampleFunction(Vector3 x) =>
                    SphereDensity(x, chunkOffset, radius)
                    + 0.5f * Perlin(x + chunkOffset + mapOffset, noiseParameters)
                    - seaLevel * 0.01f;
                chunk.chunkGenerator = new AdaptiveContour(SampleFunction, chunkSize);
                EnqueueChunkToCreate(chunk);
            }
        }

        private void EnqueueChunkToCreate(Chunk chunk) {
            chunksToCreate.Push(chunk);
        }

        private void EnqueueChunkToDestroy(Chunk chunk) {
            chunksToDestroy.Push(chunk);
        }

        private void CreateChunkWorkers() {
            while (chunksToCreate.TryPop(out Chunk chunk)) {
                Action invoke = chunk.chunkGenerator.Run;
                Action callback = () => chunksCompleted.Push(chunk);
                threadDispatcher.EnqueueThread(invoke, callback, workerName: $"{chunk.Coordinates}");
            }
        }

        private void SetCompletedChunkMesh() {
            while (chunksCompleted.TryPop(out Chunk chunk)) {
                chunk.SetMesh();
                chunk.DisableIfEmpty();
            }
        }

        private void DestroyDeadChunks() {
            while (chunksToDestroy.TryPop(out Chunk chunk)) {
                chunk.Disable();
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
            GameObject chunkObject = new();
            chunk = chunkObject.GetComponent<Chunk>();
            if (chunk == null) {
                chunk = chunkObject.AddComponent<Chunk>();
            }
            chunk.Setup(material);
            chunk.Coordinates = coordinates;
            chunk.Size = chunkSize;
            chunk.name = $"Chunk ({chunk.Coordinates})";
            chunk.transform.parent = chunkRoot.transform;
            chunk.transform.localPosition = chunk.Coordinates * chunk.Size;
            chunk.transform.localRotation = Quaternion.identity;
            chunk.Center = chunk.transform.position + chunk.Size * Mathf.Sqrt(3) * Vector3.one / 2;
            return chunk;
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