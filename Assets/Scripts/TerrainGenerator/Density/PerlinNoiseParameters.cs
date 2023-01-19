using System;
using UnityEngine;

namespace TerrainGenerator {
    [System.Serializable]
    public struct PerlinNoiseParameters {
        [Range(0, 1000)]
        public int frequency;
        [Range(0, 10)]
        public float persistence;
        [Range(0, 10)]
        public float lacunarity;
        [Range(1, 10)]
        public int octaves;
    }
}