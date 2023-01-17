using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace TerrainGenerator {
    public class HermiteDensityFactory {
        protected readonly HermiteData densityData;
        protected readonly Func<Vector3, float> densityFunction;
        public HermiteData DensityData { get { return densityData; } }

        protected readonly int size;
        protected readonly Vector3 offset;

        public HermiteDensityFactory(Func<Vector3, float> densityFunction, int size, Vector3 offset) {
            this.densityFunction = densityFunction;
            this.size = size;
            this.offset = offset;
            this.densityData = new HermiteData(size);
        }

        public void GenerateData() {
            CalculateDensity();
            CalculateTransitions();
            CalculateTransitionGradients();
        }

        private void CalculateDensity() {
            foreach (int3 gridCoordinates in densityData.Points(includeEdges: true)) {
                float density = DensityFunction(new Vector3Int(gridCoordinates.x, gridCoordinates.y, gridCoordinates.z));
                densityData.SetPointDensity(gridCoordinates, new GridPoint(gridCoordinates, density));
            }
        }

        void CalculateTransitions() {
            for (int axis = 0; axis < axisVectors.Length; ++axis) {
                foreach (Transition transition in CalculateTransitionsAlongAxis(axis)) {
                    densityData.AddTransition(transition);
                }
            }
        }

        IEnumerable<Transition> CalculateTransitionsAlongAxis(int axis) {
            for (int i = 0; i < 4; ++i) {
                int octetIndexA = (i << (axis + 1)) % 7;
                int octetIndexB = octetIndexA + (1 << axis);
                GridPoint a = points[octetIndexA];
                GridPoint b = points[octetIndexB];
                if (a.Exists != b.Exists) {
                    float interpolate = Interpolate(a.density, b.density);
                    Vector3 interpolatedTransition = new Vector3(SpatialTools.axisVectors[axis].x, SpatialTools.axisVectors[axis].y, SpatialTools.axisVectors[axis].z) * interpolate;
                    yield return new(a, b, new Vector3(a.coordinates.x, a.coordinates.y, a.coordinates.z) + interpolatedTransition);
                }
            }
        }
        void CalculateTransitionGradients() {
            foreach (Transition transition in densityData.Transitions) {
                if (!densityData.TransitionGradients.ContainsKey(transition)) {
                    densityData.TransitionGradients[transition] = ApproximateNormals(transition.transitionPoint);
                }
            }
        }
        Vector3 ApproximateNormals(Vector3 coordinates) {
            float delta = 0.00001f;
            float nx = DensityFunction(coordinates - new Vector3(delta, 0, 0));
            float ny = DensityFunction(coordinates - new Vector3(0, delta, 0));
            float nz = DensityFunction(coordinates - new Vector3(0, 0, delta));
            Vector3 gradient = new(nx, ny, nz);
            return Vector3.Normalize(-gradient);
        }

        protected float DensityFunction(Vector3 point) {
            return densityFunction(point + offset);
        }
    }
}
