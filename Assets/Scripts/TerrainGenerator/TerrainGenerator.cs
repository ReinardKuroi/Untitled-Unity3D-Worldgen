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

        Dictionary<Vector3Int, Chunk> existingChunks;
        readonly Stack<Chunk> chunksToCreate = new();
        readonly Stack<Chunk> chunksCompleted = new();
        readonly Stack<Chunk> chunksToDestroy = new();

        readonly ThreadDispatcher threadDispatcher = ThreadDispatcher.Instance;

        private void OnEnable() {
            Reset();
            UpdateMesh();
        }

        private void Reset() {
            ResetChunks();
            SetMapSeed();
            SetMapOffset();
            CreateChunkRoot();
            SetChunkRootTransform();
        }

        void Update() {
            if (settingsUpdated || Application.isPlaying && DynamicGeneration) {
                if (!Application.isPlaying) {
                    Reset();
                }
                UpdateMesh();
                settingsUpdated = false;
            }
            CreateChunkWorkers();
            threadDispatcher.UpdateThreads();
            SetCompletedChunkMesh();
            DestroyDeadChunks();
        }

        private void OnDrawGizmos() {
            if (!Application.isPlaying) {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
        }

        void UpdateMesh() {
            if (Application.isPlaying && updateInPlayMode || (!Application.isPlaying && updateInEditMode)) {
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
            if (DynamicGeneration && Application.isPlaying) {
                GenerateDynamicMesh();
            } else {
                GenerateStaticMesh();
            }
        }

        void GenerateDynamicMesh() {
            Vector3Int currentChunk = Vector3Int.FloorToInt(viewPoint.position / chunkSize);

            foreach (Vector3Int chunkPosition in NearbyChunkCoordinates(currentChunk, chunkRenderDistance)) {
                if (!existingChunks.ContainsKey(chunkPosition)) {
                    Chunk chunk = InitChunk(chunkPosition);
                    Vector3 chunkOffset = chunk.Coordinates * chunk.Size;
                    chunk.chunkGenerator = new AdaptiveContour(x => DensityFunction(x + chunkOffset), chunkSize);
                    existingChunks[chunkPosition] = chunk;
                    EnqueueChunkToCreate(chunk);
                }
                existingChunks[chunkPosition].Enable();
            }

            foreach (Vector3Int chunkPosition in existingChunks.Keys) {
                if ((existingChunks[chunkPosition].Center - viewPoint.position).magnitude > (chunkRenderDistance) * chunkSize) {
                    EnqueueChunkToDestroy(existingChunks[chunkPosition]);
                }
            }
        }

        void GenerateStaticMesh () {
            ResetChunks();

            foreach (Vector3Int chunkPosition in NearbyChunkCoordinates(new Vector3Int(0, 0, 0), mapSize + 1)) {
                Chunk chunk = InitChunk(chunkPosition);

                Vector3 chunkOffset = chunk.Coordinates * chunk.Size;
                float SampleFunction(Vector3 x) => DensityFunction(x + chunkOffset);
                chunk.chunkGenerator = new AdaptiveContour(SampleFunction, chunkSize);
                EnqueueChunkToCreate(chunk);
            }
        }

        private void ResetChunks() {
            existingChunks = new Dictionary<Vector3Int, Chunk>();
            while (chunksToCreate.TryPop(out Chunk deadchunk)) {
                EnqueueChunkToDestroy(deadchunk);
            }
            while (chunksCompleted.TryPop(out Chunk deadchunk)) {
                EnqueueChunkToDestroy(deadchunk);
            }
            foreach (Chunk deadChunk in new List<Chunk>(FindObjectsOfType<Chunk>())) {
                EnqueueChunkToDestroy(deadChunk);
            }
            threadDispatcher.Flush();
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
                if (chunk.Empty) {
                    EnqueueChunkToDestroy(chunk);
                }
            }
        }

        private void DestroyDeadChunks() {
            while (chunksToDestroy.TryPop(out Chunk chunk)) {
                if (chunk) {
                    Chunk.Disable(chunk);
                }
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

        Chunk InitChunk(Vector3Int coordinates) {
            Chunk chunk;
            GameObject chunkObject = new();
            chunk = chunkObject.GetComponent<Chunk>();
            if (chunk == null) {
                chunk = chunkObject.AddComponent<Chunk>();
            }
            chunk.Setup(material, generateColliders);
            chunk.Coordinates = coordinates;
            chunk.Size = chunkSize;
            chunk.name = $"Chunk ({chunk.Coordinates})";
            chunk.transform.parent = chunkRoot.transform;
            chunk.transform.localPosition = chunk.Coordinates * chunk.Size;
            chunk.transform.localRotation = Quaternion.identity;
            chunk.Center = chunk.transform.position + chunk.Size * Vector3.one / 2;
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

        float DensityFunction(Vector3 x) {
            float radius = chunkSize * mapSize;
            return DensityFunctionCollection.Sigmoid(
                DensityFunctionCollection.SphereDensity(x, radius)
                + DensityFunctionCollection.Perlin(x + mapOffset, noiseParameters) * mountainLevel * 0.1f
                ) - seaLevel * 0.01f;
        }
    }
}