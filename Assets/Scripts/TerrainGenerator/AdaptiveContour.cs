using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;


namespace TerrainGenerator {

    public class AdaptiveContour {
        readonly Func<Vector3, float> densityFunction;
        static readonly int3[] axisVectors = new int3[3] { new(0, 0, 1), new(0, 1, 0), new(1, 0, 0)};
        static readonly int3[,] cubicOffsets = new int3[3,6] {
            {
                // along z
                new(0, 0, 0),
                new(1, 0, 0),
                new(0, 1, 0),
                new(1, 0, 0),
                new(1, 1, 0),
                new(0, 1, 0)
            },
            {
                // along y
                new(0, 0, 0),
                new(0, 0, 1),
                new(1, 0, 0),
                new(0, 0, 1),
                new(1, 0, 1),
                new(1, 0, 0)
            },            {
                // along x
                new(0, 0, 0),
                new(0, 1, 0),
                new(0, 0, 1),
                new(0, 1, 0),
                new(0, 1, 1),
                new(0, 0, 1)
            }
        };
        readonly Dictionary<int3, GridPoint> pointDensityData = new();
        readonly Dictionary<int3, Octet> gridCells = new();
        readonly Dictionary<Transition, Vector3> transitionGradients = new();
        readonly List<int> faces = new();
        readonly List<Vector3> vertices = new();
        readonly Dictionary<int3, int> vertexIndices = new();
        int size;

        float DensityFunction(Vector3 x) {
            return densityFunction(x);
        }

        public AdaptiveContour(Func<Vector3, float> densityFunction, int size) {
            this.densityFunction = densityFunction;
            this.size = size;
        }

        void PopulateDensityData() {
            foreach (int3 gridCoordinates in Volume(size, includeEdges: true)) {
                float density = DensityFunction(new Vector3Int(gridCoordinates.x, gridCoordinates.y, gridCoordinates.z));
                pointDensityData[gridCoordinates] = new GridPoint(gridCoordinates, density);
            }
        }

        void GroupGirdPointsIntoOctets() {
            foreach (int3 gridCoordinates in Volume(size, includeEdges: false)) {
                Octet octet = new Octet(gridCoordinates, pointDensityData);
                octet.CalculateTransitions();
                if (octet.HasTransitions) {
                    CalculateTransitionGradients(octet.transitions);
                    octet.vertex = ParticleDescent(octet.transitions);
                }
                gridCells[gridCoordinates] = octet;
            }
        }

        public void RunContouring() {
            PopulateDensityData();
            GroupGirdPointsIntoOctets();
            GenerateVertices();
            GenerateFaces();
        }

        public void SetMesh(Mesh mesh) {
            mesh.Clear();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = faces.ToArray();
            mesh.RecalculateNormals();
        }

        void GenerateVertices() {
            foreach (int3 gridCoordinates in Volume(size, includeEdges: false)) {
                if (gridCells[gridCoordinates].HasTransitions) {
                    vertices.Add(gridCells[gridCoordinates].vertex);
                    vertexIndices[gridCoordinates] = vertices.Count - 1;
                }
            }
        }

        void GenerateFaces() {
            foreach (int3 coordinates in Volume(size, includeEdges: false)) {
                for (int axis = 0; axis < axisVectors.Length; ++axis) {
                    int3 offsetCoordinates = coordinates + axisVectors[axis];
                    if (offsetCoordinates.x == 0 || offsetCoordinates.y == 0 || offsetCoordinates.z == 0) {
                        continue;
                    }
                    bool inside = pointDensityData[coordinates].Exists;
                    bool outside = pointDensityData[offsetCoordinates].Exists;
                    if (inside != outside) {
                        int[] quad = GenerateQuad(coordinates, axis, vertexIndices);
                        if (outside) {
                            Array.Reverse(quad);
                        }
                        faces.AddRange(quad);
                    }
                }
            }
        }

        void CalculateTransitionGradients(IEnumerable<Transition> transitions) {
            foreach (Transition transition in transitions) {
                if (!transitionGradients.ContainsKey(transition)) {
                    transitionGradients[transition] = ApproximateNormals(transition.transitionPoint);
                }
            }
        }

        int[] GenerateQuad(int3 coordinates, int axis, Dictionary<int3, int> vertexIndices) {
            int[] quad = new int[6];

            for (int i = 0; i < quad.Length; ++i) {
                int3 index = coordinates - cubicOffsets[axis, i];
                quad[i] = vertexIndices[index];
            }

            return quad;
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
                    force += -1f * Vector3.Dot(transitionGradients[transition], centerPoint - transitionPoint)
                        * transitionGradients[transition];
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
            float delta = 0.00001f;
            float nx = DensityFunction(coordinates - new Vector3(delta, 0, 0));
            float ny = DensityFunction(coordinates - new Vector3(0, delta, 0));
            float nz = DensityFunction(coordinates - new Vector3(0, 0, delta));
            Vector3 gradient = new(nx, ny, nz);
            return Vector3.Normalize(-gradient);
        }

        IEnumerable<int3> Volume(int size, bool includeEdges = true) {
            if (includeEdges) {
                ++size;
            }
            for (int x = 0; x <= size; ++x) {
                for (int y = 0; y <= size; ++y) {
                    for (int z = 0; z <= size; ++z) {
                        yield return new int3(x, y, z);
                    }
                }
            }
        }

        class GridPoint {
            public readonly int3 coordinates;
            public readonly float density;
            public bool Exists { get { return density > 0; } }

            public GridPoint(int3 coordinates, float density) {
                this.coordinates = coordinates;
                this.density = density;
            }

            public override int GetHashCode() {
                return coordinates.GetHashCode();
            }
        }

        class Octet {
            public readonly int3 coordinates;
            public readonly GridPoint[] points;
            public Vector3 vertex;
            public List<Transition> transitions;
            public bool HasTransitions { get { return transitions.Count != 0; } }

            public Octet(int3 coordinates, Dictionary<int3, GridPoint> pointDensityData) {
                transitions = new();
                this.coordinates = coordinates;
                points = GetNeighbouringOctet(pointDensityData);
            }

            public static int IndexFromCoords(int x, int y, int z) {
                return x * 4 + y * 2 + z;
            }

            float Interpolate(float a, float b) {
                return a / (a - b);
            }

            GridPoint[] GetNeighbouringOctet(Dictionary<int3, GridPoint> pointDensityData) {
                GridPoint[] octet = new GridPoint[8];
                for (int dx = 0; dx < 2; ++dx) {
                    for (int dy = 0; dy < 2; ++dy) {
                        for (int dz = 0; dz < 2; ++dz) {
                            int3 offset = new(dx, dy, dz);
                            octet[Octet.IndexFromCoords(dx, dy, dz)] = pointDensityData[coordinates + offset];
                        }
                    }
                }
                return octet;
            }

            IEnumerable<Transition> CalculateTransitionsAlongAxis(int axis) {
                for (int i = 0; i < 4; ++i) {
                    int octetIndexA = (i << (axis + 1)) % 7;
                    int octetIndexB = octetIndexA + (1 << axis);
                    GridPoint a = points[octetIndexA];
                    GridPoint b = points[octetIndexB];
                    if (a.Exists != b.Exists) {
                        float interpolate = Interpolate(a.density, b.density);
                        Vector3 interpolatedTransition = new Vector3 (axisVectors[axis].x, axisVectors[axis].y, axisVectors[axis].z) * interpolate;
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

        class Transition {
            public readonly GridPoint from;
            public readonly GridPoint to;
            public readonly Vector3 transitionPoint;

            public Transition(GridPoint from, GridPoint to, Vector3 transitionPoint) {
                this.from = from;
                this.to = to;
                this.transitionPoint = transitionPoint;
            }

            public override bool Equals(object obj) {
                return obj is Transition transition &&
                       EqualityComparer<GridPoint>.Default.Equals(from, transition.from) &&
                       EqualityComparer<GridPoint>.Default.Equals(to, transition.to);
            }

            public override int GetHashCode() {
                return HashCode.Combine(from, to);
            }
        }
    }
}