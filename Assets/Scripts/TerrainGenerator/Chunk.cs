using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator {
    public class Chunk : MonoBehaviour {
        public Vector3Int Coordinates { get; set; }
        public int Size { get; set; }
        public Mesh mesh;
        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        public MeshCollider meshCollider;
        readonly bool generateCollider = true;

        public static int CalculateHash(int x, int y, int z) {
            return (x * 73856093 ^ y * 19349663 ^ z * 83492791) % int.MaxValue;
        }

        public void Setup(Material material) {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();

            if (meshFilter == null) {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (meshRenderer == null) {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            if (meshCollider == null && generateCollider) {
                meshCollider = gameObject.AddComponent<MeshCollider>();
            } else if (meshCollider && !generateCollider) {
                DestroyImmediate(meshCollider);
            }

            meshRenderer.material = material;

            mesh = meshFilter.sharedMesh;
            if (mesh == null) {
                mesh = new Mesh {
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
                };
                meshFilter.sharedMesh = mesh;
            }
            

            if (generateCollider) {
                if (meshCollider.sharedMesh == null) {
                    meshCollider.sharedMesh = mesh;
                }
                gameObject.isStatic = false;
                meshCollider.enabled = false;
                meshCollider.enabled = true;
            }
        }

        public Chunk Disable() {
            if (!Application.isPlaying) {
                DestroyImmediate(gameObject, false);
                return null;
            } else {
                mesh.Clear();
                gameObject.SetActive(false);
                return this;
            }
        }
    }
}
