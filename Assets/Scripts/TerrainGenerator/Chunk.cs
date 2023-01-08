using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator {
    public class Chunk : MonoBehaviour {
        [field: SerializeField]
        public Vector3Int Coordinates { get; set; }
        [field: SerializeField]
        public int Size { get; set; }
        [field: SerializeField]
        public Vector3 Center { get; set; }
        public bool Empty { get { return mesh.vertexCount == 0 || mesh.triangles.Length == 0; } }

        public IMeshGenerator chunkGenerator;

        [HideInInspector]
        public Mesh mesh;
        MeshCollider meshCollider;
        MeshFilter meshFilter;
        MeshRenderer meshRenderer;

        public void Setup(Material material, bool generateCollider) {
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

        public void Enable() {
            if (!gameObject.activeSelf) {
                gameObject.SetActive(true);
            }
        }

        public void Disable() {
            if (gameObject.activeSelf) {
                gameObject.SetActive(false);
            }
        }

        public static Chunk Disable(Chunk chunk) {
            if (!Application.isPlaying) {
                if (chunk.gameObject) {
                    chunk.Destroy();
                }
                return null;
            } else {
                chunk.gameObject.SetActive(false);
                return chunk;
            }
        }

        internal void SetMesh() {
            chunkGenerator.SetMesh(mesh);
        }

        internal void Destroy() {
            DestroyImmediate(gameObject, false);
        }
    }
}
