using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;


namespace TerrainGenerator {

    public class AdaptiveContour : CPUMeshGenerator<HermiteData> {
        public AdaptiveContour(HermiteData densityData) : base(densityData) { }

        void GenerateVertices() {
            foreach (int3 gridCoordinates in densityData.Points(includeEdges: false)) {
                AdaptiveOctet octet = new(gridCoordinates, densityData.PointDensityData);
                octet.CalculateTransitions();
                if (octet.HasTransitions) {
                    CalculateTransitionGradients(octet.transitions);
                    octet.vertex = ParticleDescent(octet.transitions);
                    GenerateVertex(octet);
                }
            }
        }

        public override void Run() {
            GenerateVertices();
            GenerateFaces();
        }

        void CalculateTransitionGradients(IEnumerable<Transition> transitions) {
            foreach (Transition transition in transitions) {
                if (!densityData.TransitionGradients.ContainsKey(transition)) {
                    densityData.TransitionGradients[transition] = ApproximateNormals(transition.transitionPoint);
                }
            }
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

        Vector3 ApproximateNormals(Vector3 coordinates) {
            //float delta = 0.00001f;
            //float nx = DensityFunction(coordinates - new Vector3(delta, 0, 0));
            //float ny = DensityFunction(coordinates - new Vector3(0, delta, 0));
            //float nz = DensityFunction(coordinates - new Vector3(0, 0, delta));
            //Vector3 gradient = new(nx, ny, nz);
            //return Vector3.Normalize(-gradient);
            return new();
        }

        public override void Free() {
            throw new NotImplementedException();
        }

        class AdaptiveOctet : Octet {
            public List<Transition> transitions;
            public bool HasTransitions { get { return transitions.Count != 0; } }

            public AdaptiveOctet(int3 coordinates, Dictionary<int3, GridPoint> pointDensityData) : base(coordinates, pointDensityData) {
                transitions = new();
            }

            IEnumerable<Transition> CalculateTransitionsAlongAxis(int axis) {
                for (int i = 0; i < 4; ++i) {
                    int octetIndexA = (i << (axis + 1)) % 7;
                    int octetIndexB = octetIndexA + (1 << axis);
                    GridPoint a = points[octetIndexA];
                    GridPoint b = points[octetIndexB];
                    if (a.Exists != b.Exists) {
                        float interpolate = Interpolate(a.density, b.density);
                        Vector3 interpolatedTransition = new Vector3(axisVectors[axis].x, axisVectors[axis].y, axisVectors[axis].z) * interpolate;
                        yield return new(a, b, new Vector3(a.coordinates.x, a.coordinates.y, a.coordinates.z) + interpolatedTransition);
                    }
                }
            }

            public void CalculateTransitions() {
                for (int axis = 0; axis < axisVectors.Length; ++axis) {
                    foreach (Transition transition in CalculateTransitionsAlongAxis(axis)) {
                        transitions.Add(transition);
                    }
                }
            }
        }
    }
}