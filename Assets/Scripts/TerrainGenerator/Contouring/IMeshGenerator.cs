using UnityEngine;


namespace TerrainGenerator {
    public interface IMeshGenerator {
        public IDensityData DensityData { get; set; }
        public void CreateMesh();
        public void SetMesh(Mesh mesh);
        void Free();
    }
}