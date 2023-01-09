using System.Collections.Generic;

namespace TerrainGenerator {
    public class ChunkPool {
        readonly Stack<Chunk> chunks = new();

        public Chunk Fetch() {
            while (chunks.TryPop(out Chunk chunk)) {
                if (chunk.gameObject) {
                    chunk.Enable();
                    return chunk;
                }
            }
            return Chunk.Create();
        }

        public void Store(Chunk chunk) {
            chunks.Push(chunk);
            chunk.Disable();
            chunk.ResetMesh();
            chunk.name = $"Chunk (stored) #{chunks.Count}";
        }

        public void Flush() {
            while (chunks.TryPop(out Chunk chunk)) {
                chunk.Destroy();
            }
        }
    }
}