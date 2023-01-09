using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator {

    [ExecuteInEditMode]
    public class TerrainGenerator : MonoBehaviour {
        public bool updateInEditMode = false;
        public bool updateInPlayMode = false;

        [Header("Map dimensions")]
        public int mapSize = 1;
        [Header("Map seed")]
        public int inputMapSeed = 0;
        private int mapSeed;
        [SerializeField]
        Vector3 mapOffset = new();
        [Header("Chunk parameters")]
        [Range(-100, 100)]
        public int seaLevel = 50;
        [Range(0, 100)]
        public int mountainLevel = 10;
        public PerlinNoiseParameters noiseParameters = new();
        public int chunkSize = 16;
        public Material material;
        public bool generateColliders;

        const string chunkRootName = "Chunk Root";
        GameObject chunkRoot;
        bool settingsUpdated;
        [Header("Dynamic Update Settings")]
        public bool DynamicGeneration = false;
        public int chunkRenderDistance = 3;
        public Transform viewPoint;

        readonly ChunkPool chunkPool = new();
        Dictionary<Vector3Int, Chunk> existingChunks;
        readonly Stack<Chunk> chunksToCreate = new();
        readonly Stack<Chunk> chunksCompleted = new();

        readonly ThreadDispatcher threadDispatcher = ThreadDispatcher.Instance;

        private void OnEnable() {
            Set();
            ResetChunks();
        }

        private void OnDestroy() {
            ResetChunks();
            chunkPool.Flush();
        }

        private void Set() {
            SetMapSeed();
            SetMapOffset();
            CreateChunkRoot();
            SetChunkRootTransform();
        }

        void Update() {
            if (settingsUpdated || Application.isPlaying && DynamicGeneration) {
                if (!Application.isPlaying) {
                    Set();
                }
                if (Application.isPlaying && updateInPlayMode || (!Application.isPlaying && updateInEditMode)) {
                    GenerateMesh();
                }
                settingsUpdated = false;
            }
        }

        void LateUpdate() {
            CreateChunkWorkers();
            threadDispatcher.UpdateThreads();
            SetCompletedChunkMesh();
        }

        private void OnDrawGizmos() {
            if (!Application.isPlaying) {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
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
            if (DynamicGeneration && Application.isPlaying) {
                UpdateDynamicMesh();
            } else {
                UpdateStaticMesh();
            }
        }

        void UpdateDynamicMesh() {
            DestroyChunksOutOfLoadRange();

            Vector3Int currentChunk = Vector3Int.FloorToInt(viewPoint.position / chunkSize);

            foreach (Vector3Int chunkPosition in NearbyChunkCoordinates(currentChunk, chunkRenderDistance)) {
                if (!existingChunks.ContainsKey(chunkPosition)) {
                    existingChunks[chunkPosition] = InitChunk(chunkPosition);
                }
                existingChunks[chunkPosition].Enable();
            }

            foreach (Vector3Int chunkPosition in existingChunks.Keys) {
                if ((chunkPosition + Vector3.one * 0.5f - currentChunk).magnitude > chunkRenderDistance) {
                    existingChunks[chunkPosition].Disable();
                }
            }
        }

        private void DestroyChunksOutOfLoadRange() {
            List<Chunk> chunksToDestroy = new();
            foreach (Vector3Int chunkPosition in existingChunks.Keys) {
                if ((existingChunks[chunkPosition].Center - viewPoint.position).magnitude > chunkRenderDistance * chunkSize * 2) {
                    chunksToDestroy.Add(existingChunks[chunkPosition]);
                }
            }
            foreach (Chunk chunk in chunksToDestroy) {
                DestroyDeadChunk(chunk);
            }
        }

        void UpdateStaticMesh () {
            ResetChunks();
            foreach (Vector3Int chunkPosition in NearbyChunkCoordinates(new Vector3Int(0, 0, 0), mapSize + 1)) {
                existingChunks[chunkPosition] = InitChunk(chunkPosition);
            }
        }

        private Chunk InitChunk(Vector3Int chunkPosition) {
            Chunk chunk = chunkPool.Fetch();
            chunk.Init(chunkPosition, chunkSize, chunkRoot.transform);
            chunk.SetupMesh(material, generateColliders);
            Vector3 chunkOffset = chunk.Coordinates * chunk.Size;
            chunk.chunkGenerator = new AdaptiveContour((x) => DensityFunction(x + chunkOffset), chunk.Size);
            EnqueueChunkToCreate(chunk);
            return chunk;
        }

        private void ResetChunks() {
            threadDispatcher.Flush();

            existingChunks = new Dictionary<Vector3Int, Chunk>();
            while (chunksToCreate.TryPop(out Chunk deadchunk)) {
                DestroyDeadChunk(deadchunk);
            }
            while (chunksCompleted.TryPop(out Chunk deadchunk)) {
                DestroyDeadChunk(deadchunk);
            }
            foreach (Chunk deadChunk in FindObjectsOfType<Chunk>()) {
                DestroyDeadChunk(deadChunk);
            }
        }

        private void EnqueueChunkToCreate(Chunk chunk) {
            Debug.Log($"Enqueued chunk birth: {chunk.name}", chunk.gameObject);
            chunksToCreate.Push(chunk);
        }

        private void CreateChunkWorkers() {
            while (chunksToCreate.TryPop(out Chunk chunk)) {
                chunk.workerId = threadDispatcher.EnqueueThread(chunk.chunkGenerator.Run, () => chunksCompleted.Push(chunk), workerName: $"{chunk.Coordinates}");
            }
        }

        private void SetCompletedChunkMesh() {
            while (chunksCompleted.TryPop(out Chunk chunk)) {
                chunk.workerId = 0;
                chunk.SetMesh();
            }
        }

        private void DestroyDeadChunk(Chunk chunk) {
            if (chunk) {
                Debug.Log($"Destroyed chunk {chunk.name}");
                existingChunks.Remove(chunk.Coordinates);
                threadDispatcher.TryKill(chunk.workerId);
                chunkPool.Store(chunk);
            }
        }

        IEnumerable<Vector3Int> NearbyChunkCoordinates(Vector3Int currentChunk, int chunkRenderDistance) {
            List<(Vector3Int, float distance)> coordinateList = new();
            for (int dx = -chunkRenderDistance; dx <= chunkRenderDistance; ++dx) {
                for (int dy = -chunkRenderDistance; dy <= chunkRenderDistance; ++dy) {
                    for (int dz = -chunkRenderDistance; dz <= chunkRenderDistance; ++dz) {
                        Vector3Int offset = new(dx, dy, dz);
                        float distance = (offset + Vector3.one * 0.5f).magnitude;
                        if (distance <= chunkRenderDistance) {
                            coordinateList.Add((currentChunk + offset, distance));
                        }
                    }
                }
            }
            coordinateList.Sort((x, y) => y.distance.CompareTo(x.distance));
            foreach ((Vector3Int coordinate, float) entry in coordinateList) {
                yield return entry.coordinate;
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

        void SetChunkRootTransform() {
            chunkRoot.transform.SetPositionAndRotation(transform.position, transform.rotation);
        }

        float DensityFunction(Vector3 x) {
            float radius = chunkSize * mapSize;
            return DensityFunctionCollection.Sigmoid(
                DensityFunctionCollection.SphereDensity(x, radius)
                + DensityFunctionCollection.Perlin(x + mapOffset, noiseParameters) * mountainLevel * 0.1f
                ) - seaLevel * 0.01f;
        }
    }
}