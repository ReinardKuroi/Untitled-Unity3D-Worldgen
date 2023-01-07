using UnityEngine;


namespace TerrainGenerator {
    public interface IMeshGenerator {
        public void Run();
        public void SetMesh(Mesh mesh);
    }
}