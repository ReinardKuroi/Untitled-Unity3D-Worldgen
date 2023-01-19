using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace TerrainGenerator {
    public class HermiteData : DensityData {
        readonly List<Transition> transitions = new();

        public List<Transition> Transitions { get { return transitions; } }

        readonly Dictionary<Transition, Vector3> transitionGradients = new();
        public Dictionary<Transition, Vector3> TransitionGradients { get { return transitionGradients; } }

        public HermiteData(int size) : base(size) { }

        public void AddTransition(Transition transition) {
            if (!transitions.Contains(transition))
                transitions.Add(transition);
        }

        public void SetTransitionGradient(Transition transition, Vector3 gradient) {
            transitionGradients.Add(transition, gradient);
        }
    }
}
