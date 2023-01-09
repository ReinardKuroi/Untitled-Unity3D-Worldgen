using System;
using UnityEngine;

namespace TerrainGenerator {
    [Serializable]
    public struct TerrainGenerationOptions {
        [Header("Update settings")]
        public bool updateInEditMode;
        public bool updateInPlayMode;
        [Header("Dynamic Update Settings")]
        public bool DynamicGeneration;
        public int chunkRenderDistance;
        [Header("Map dimensions")]
        public int mapSize;
        [Header("Map seed")]
        public int inputMapSeed;
        [Header("Chunk parameters")]
        [Range(-100, 100)]
        public int seaLevel;
        [Range(0, 100)]
        public int mountainLevel;
        public PerlinNoiseParameters noiseParameters;
        public int chunkSize;
        public bool generateColliders;
    }
}