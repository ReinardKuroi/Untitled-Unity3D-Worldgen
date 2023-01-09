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
        [SerializeField]
        public Vector3 Center { get { return Size * (Coordinates + Vector3.one * 0.5f); } }

        public bool Empty { get { return mesh.vertexCount == 0 || mesh.triangles.Length == 0; } }

        public IMeshGenerator chunkGenerator;
        public int workerId;

        [HideInInspector]
        public Mesh mesh;
        Mesh collisionMesh;
        MeshCollider meshCollider;
        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        bool generateCollider;

        public static Chunk Create() {
            Chunk chunk;
            GameObject chunkObject = new();
            chunk = chunkObject.GetComponent<Chunk>();
            if (chunk == null) {
                chunk = chunkObject.AddComponent<Chunk>();
            }
            return chunk;
        }

        public void Destroy() {
            DestroyImmediate(gameObject, false);
        }

        public void Init(Vector3Int coordinates, int chunkSize, Transform chunkRoot) {
            Coordinates = coordinates;
            Size = chunkSize;
            name = $"Chunk ({Coordinates})";
            transform.parent = chunkRoot;
            transform.localPosition = Coordinates * Size;
            transform.localRotation = Quaternion.identity;
        }

        public void SetupMesh(Material material, bool generateCollider) {
            this.generateCollider = generateCollider;
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
                    meshCollider.cookingOptions = MeshColliderCookingOptions.None;
                    meshCollider.cookingOptions = MeshColliderCookingOptions.UseFastMidphase;
                    meshCollider.sharedMesh = mesh;
                }
                ForceUpdateCollider();
            }
        }

        private void ForceUpdateCollider() {
            meshCollider.enabled = false;
            meshCollider.enabled = generateCollider;
        }

        public void Enable() {
            if (gameObject && !gameObject.activeSelf) {
                gameObject.SetActive(true);
            }
        }

        public void Disable() {
            if (gameObject && gameObject.activeSelf) {
                gameObject.SetActive(false);
            }
        }

        public void SetMesh() {
            chunkGenerator.SetMesh(mesh);
            if (!Empty && generateCollider) {
                ForceUpdateCollider();
            }
        }

        public void ResetMesh() {
            if (mesh != null) {
                mesh.Clear();
            }
        }
    }
}
