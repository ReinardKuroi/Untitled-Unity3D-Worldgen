using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace TerrainGenerator {
    public class MarchingCubes : CPUMeshGenerator<DensityData> {
        public override void Free() {
            throw new System.NotImplementedException();
        }

        public override void CreateMesh() {
            GenerateFaces();
        }

        class MarchingOctet : Octet {
            public MarchingOctet(int3 coordinates, Dictionary<int3, GridPoint> pointDensityData) : base(coordinates, pointDensityData) {
            }

            public byte CalculateID() {
                byte id = 0;
                foreach (GridPoint point in points) {
                    if (point.Exists) {
                        id += 1;
                    }
                    id <<= 1;
                }
                return id;
            }
        }
    }
}
