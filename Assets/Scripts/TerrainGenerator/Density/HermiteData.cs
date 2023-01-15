using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace TerrainGenerator {
    public class HermiteData : DensityData {
        readonly Dictionary<Transition, Vector3> transitionGradients = new();

        public Dictionary<Transition, Vector3> TransitionGradients { get { return transitionGradients; } }
    }
}
