using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator {
    public class Chunk : MonoBehaviour {
        public Vector3Int Coordinates { get { return coordinates;  } set { coordinates = value; center = value + Vector3.one * 0.5f; } }
        private Vector3Int coordinates;
        public int Size { get; set; }
        public Vector3 Center { get { return center; } }
        private Vector3 center;

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
            if (gameObject) {
                DestroyImmediate(gameObject, false);
            }
        }

        public void Init(Vector3Int coordinates, int chunkSize, Transform chunkRoot) {
            Coordinates = coordinates;
            Size = chunkSize;
            name = Coordinates.ToString();
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
        }

        private void ForceUpdateCollider() {
            meshCollider.enabled = false;
            meshCollider.enabled = true;
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
                meshCollider.sharedMesh = mesh;
                ForceUpdateCollider();
            }
        }

        public void ResetMesh() {
            if (mesh != null) {
                mesh.Clear();
            }
        }

        public static int PositionalHash(Vector3Int coordinates) {
            int hashCode = (coordinates.x * 73856093 + coordinates.y * 19349669 + coordinates.z * 83492791) % int.MaxValue;
            return hashCode;
        }
    }
}
