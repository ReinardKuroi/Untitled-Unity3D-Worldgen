using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;


namespace TerrainGenerator {

    public class AdaptiveContour : CPUMeshGenerator<HermiteData> {

        void GenerateVertices() {
            foreach (int3 gridCoordinates in densityData.Points(includeEdges: false)) {
                AdaptiveOctet octet = new(gridCoordinates, densityData);
                if (octet.HasTransitions) {
                    octet.vertex = ParticleDescent(octet.transitions);
                    GenerateVertex(octet);
                }
            }
        }

        public override void CreateMesh() {
            GenerateVertices();
            GenerateFaces();
        }

        private Vector3 ParticleDescent(List<Transition> transitions, float threshold = 0.00001f) {
            const int maxIterations = 50;
            int transitionsCount = transitions.Count;
            Vector3 centerPoint = new();
            
            foreach (Transition transition in transitions) {
                centerPoint += transition.transitionPoint;
            }
            centerPoint /= transitionsCount;

            for (int i = 0; i < maxIterations; ++i) {
                Vector3 force = new();

                for (int j = 0; j < transitionsCount; ++j) {
                    Transition transition = transitions[j];
                    Vector3 transitionPoint = transition.transitionPoint;
                    force += -1f * Vector3.Dot(densityData.TransitionGradients[transition], centerPoint - transitionPoint)
                        * densityData.TransitionGradients[transition];
                }

                float damping = 1 - (float)i / maxIterations;
                centerPoint += force * damping / transitionsCount;

                if (force.sqrMagnitude * force.sqrMagnitude < threshold) {
                    break;
                }
            }

            return centerPoint;
        }

        public override void Free() {
            throw new NotImplementedException();
        }

        class AdaptiveOctet : Octet {
            public readonly List<Transition> transitions = new();

            public bool HasTransitions { get { return transitions.Count != 0; } }

            public AdaptiveOctet(int3 coordinates, HermiteData densityData) : base(coordinates, densityData) {
                foreach (Transition transition in densityData.Transitions)
                    if (Any(transition.from == ))
                transitions = ;
            }
        }
    }
}