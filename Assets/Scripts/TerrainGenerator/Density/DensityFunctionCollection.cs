using UnityEngine;
using Unity.Mathematics;

namespace TerrainGenerator {
    public class DensityFunctionCollection {
        public static float SphereDensity(Vector3 x, float rad) {
            return rad - x.magnitude;
        }

        public static float Sigmoid(float x) {
            return 2 / (1 + Mathf.Exp(-x)) - 1;
        }

        public static float Perlin(Vector3 point, PerlinNoiseParameters parameters) {
            float amplitude = 1f;
            float cumulativeNoise = 0f;
            float cumulativeAmplitude = 0f;

            float frequency = parameters.frequency / 1000f;
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
}