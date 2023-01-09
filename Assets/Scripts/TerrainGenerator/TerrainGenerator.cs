using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator {

    [ExecuteInEditMode]
    public class TerrainGenerator : MonoBehaviour {
        [Header("Chunk viewer")]
        public Transform viewPoint;
        [Header("Chunk material")]
        public Material material;
        [Header("Terrain")]
        public TerrainGenerationOptions generationOptions;

        GameObject chunkRoot;
        const string chunkRootName = "Chunk Root";

        bool settingsUpdated;
        Vector3 mapOffset = new();
        private int mapSeed;

        readonly ChunkPool chunkPool = new();
        Dictionary<Vector3Int, Chunk> existingChunks;
        readonly Stack<Chunk> chunksToCreate = new();
        readonly Stack<Chunk> chunksCompleted = new();

        readonly ThreadDispatcher threadDispatcher = ThreadDispatcher.Instance;

        private void OnEnable() {
            Application.targetFrameRate = 60;
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
            if (settingsUpdated || Application.isPlaying && generationOptions.DynamicGeneration) {
                if (!Application.isPlaying) {
                    Set();
                }
                if (Application.isPlaying && generationOptions.updateInPlayMode || (!Application.isPlaying && generationOptions.updateInEditMode)) {
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
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
#endif
        }

        private void OnValidate() {
            settingsUpdated = true;
        }

        void SetMapSeed() {
            if (generationOptions.inputMapSeed == 0) {
                System.Random random = new();
                mapSeed = random.Next();
            } else {
                mapSeed = generationOptions.inputMapSeed;
            }
        }

        void SetMapOffset() {
            System.Random seededMapCoords = new(mapSeed);
            mapOffset = new Vector3Int(seededMapCoords.Next() % (2 << 16), seededMapCoords.Next() % (2 << 16), seededMapCoords.Next() % (2 << 16));
        }

        void GenerateMesh() {
            if (generationOptions.DynamicGeneration && Application.isPlaying) {
                UpdateDynamicMesh();
            } else {
                UpdateStaticMesh();
            }
        }

        void UpdateDynamicMesh() {
            DestroyChunksOutOfLoadRange();

            Vector3Int currentChunk = Vector3Int.FloorToInt(viewPoint.position / generationOptions.chunkSize);

            foreach (Vector3Int chunkPosition in NearbyChunkCoordinates(currentChunk, generationOptions.chunkRenderDistance)) {
                if (!existingChunks.ContainsKey(chunkPosition)) {
                    existingChunks[chunkPosition] = InitChunk(chunkPosition);
                }
                existingChunks[chunkPosition].Enable();
            }

            foreach (Vector3Int chunkPosition in existingChunks.Keys) {
                if ((chunkPosition + Vector3.one * 0.5f - currentChunk).magnitude > generationOptions.chunkRenderDistance) {
                    existingChunks[chunkPosition].Disable();
                }
            }
        }

        private void DestroyChunksOutOfLoadRange() {
            List<Chunk> chunksToDestroy = new();
            foreach (Vector3Int chunkPosition in existingChunks.Keys) {
                if ((existingChunks[chunkPosition].Center - viewPoint.position).magnitude > generationOptions.chunkRenderDistance * generationOptions.chunkSize * 2) {
                    chunksToDestroy.Add(existingChunks[chunkPosition]);
                }
            }
            foreach (Chunk chunk in chunksToDestroy) {
                DestroyDeadChunk(chunk);
            }
        }

        void UpdateStaticMesh () {
            ResetChunks();
            foreach (Vector3Int chunkPosition in NearbyChunkCoordinates(new Vector3Int(0, 0, 0), generationOptions.mapSize + 1)) {
                existingChunks[chunkPosition] = InitChunk(chunkPosition);
            }
        }

        private Chunk InitChunk(Vector3Int chunkPosition) {
            Chunk chunk = chunkPool.Fetch();
            chunk.Init(chunkPosition, generationOptions.chunkSize, chunkRoot.transform);
            chunk.SetupMesh(material, generationOptions.generateColliders);
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
            float radius = generationOptions.chunkSize * generationOptions.mapSize;
            return DensityFunctionCollection.Sigmoid(DensityFunctionCollection.SphereDensity(x, radius) * generationOptions.seaLevel * 0.01f
                + DensityFunctionCollection.Perlin(x + mapOffset, generationOptions.noiseParameters) * generationOptions.mountainLevel * 0.1f)
                + generationOptions.offsetLevel * 0.01f;
        }
    }
}