using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;


namespace TerrainGenerator {

    public class AdaptiveContour {
        readonly Func<Vector3, float> densityFunction;
        readonly Vector3[,] cubicOffsets = new Vector3[3,6] {
            {
                // along x
                new(0, 0, 0),
                new(0, 1, 0),
                new(0, 0, 1),
                new(0, 1, 0),
                new(0, 1, 1),
                new(0, 0, 1)
            },
            {
                // along y
                new(0, 0, 0),
                new(0, 0, 1),
                new(1, 0, 0),
                new(0, 0, 1),
                new(1, 0, 1),
                new(1, 0, 0)
            },
            {
                // along z
                new(0, 0, 0),
                new(1, 0, 0),
                new(0, 1, 0),
                new(1, 0, 0),
                new(1, 1, 0),
                new(0, 1, 0)
            }
        };
        readonly Dictionary<Vector3Int, GridPoint> pointDensityData = new();
        readonly Dictionary<Vector3Int, Octet> octets = new();
        readonly Dictionary<(GridPoint, GridPoint),Vector3> transitionGradients = new();
        readonly List<int> faces = new();
        readonly List<Vector3> vertices = new();
        readonly Dictionary<Vector3, int> vertexIndices = new ();
        Vector3Int size;

        float DensityFunction(Vector3 x) {
            return densityFunction(x);
        }

        public AdaptiveContour(Func<Vector3, float> densityFunction, Vector3Int size) {
            this.densityFunction = densityFunction;
            this.size = size;
        }

        void PopulateDensityData() {
            foreach (Vector3Int gridCoordinates in Volume(size, includeEdges: true)) {
                float density = DensityFunction(gridCoordinates);
                pointDensityData[gridCoordinates] = new GridPoint(gridCoordinates, density);
            }
        }

        void GroupGirdPointsIntoOctets() {
            foreach (Vector3Int gridCoordinates in Volume(size, includeEdges: false)) {
                octets[gridCoordinates] = new Octet(gridCoordinates, GetNeighbouringOctet(pointDensityData[gridCoordinates]));
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
            foreach (Vector3Int gridCoordinates in Volume(size, includeEdges: false)) {
                Octet octet = octets[gridCoordinates];
                List<Transition> transitions = CalculateTransitions(octet.vertices);

                if (transitions.Count != 0) {
                    CalculateTransitionGradients(transitions);
                    Vector3 vertex = ParticleDescent(transitions);
                    vertices.Add(vertex);
                    vertexIndices[gridCoordinates] = vertices.Count - 1;
                }
            }
        }

        void GenerateFaces() {
            Vector3Int[] offsets = new Vector3Int[3] { Vector3Int.right, Vector3Int.up, Vector3Int.forward };

            foreach (Vector3Int coordinates in Volume(size, includeEdges: false)) {
                for (int axis = 0; axis < offsets.Length; ++axis) {
                    Vector3Int offsetCoordinates = coordinates + offsets[axis];
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

        int[] GenerateQuad(Vector3 coordinates, int axis, Dictionary<Vector3, int> vertexIndices) {
            int[] quad = new int[6];

            for (int i = 0; i < quad.Length; ++i) {
                Vector3 index = coordinates - cubicOffsets[axis, i];
                quad[i] = vertexIndices[index];
            }

            return quad;
        }

        float Interpolate(float a, float b) {
            return a / (a - b);
        }

        GridPoint[,,] GetNeighbouringOctet(GridPoint point) {
            GridPoint[,,] octet = new GridPoint[2, 2, 2];
            for (int dx = 0; dx < 2; ++dx) {
                for (int dy = 0; dy < 2; ++dy) {
                    for (int dz = 0; dz < 2; ++dz) {
                        Vector3Int vertex = point.coordinates + new Vector3Int(dx, dy, dz);
                        octet[dx, dy, dz] = pointDensityData[vertex];
                    }
                }
            }
            return octet;
        }

        List<Transition> CalculateTransitions(GridPoint[,,] octet) {
            List<Transition> transitions = new();
            GridPoint point = octet[0, 0, 0];
            // Along x axis
            for (int dy = 0; dy < 2; ++dy) {
                for (int dz = 0; dz < 2; ++dz) {
                    GridPoint a = octet[0, dy, dz];
                    GridPoint b = octet[1, dy, dz];
                    if (a.Exists != b.Exists) {
                        float interpolateX = Interpolate(a.density, b.density);
                        transitions.Add(new (a, b, point.coordinates + new Vector3(interpolateX, dy, dz)));
                    }
                }
            }
            // Along y axis
            for (int dx = 0; dx < 2; ++dx) {
                for (int dz = 0; dz < 2; ++dz) {
                    GridPoint a = octet[dx, 0, dz];
                    GridPoint b = octet[dx, 1, dz];
                    if (a.Exists != b.Exists) {
                        float interpolateY = Interpolate(a.density, b.density);
                        transitions.Add(new (a, b, point.coordinates + new Vector3(dx, interpolateY, dz)));
                    }
                }
            }
            // Along z axis
            for (int dx = 0; dx < 2; ++dx) {
                for (int dy = 0; dy < 2; ++dy) {
                    GridPoint a = octet[dx, dy, 0];
                    GridPoint b = octet[dx, dy, 1];
                    if (a.Exists != b.Exists) {
                        float interpolateZ = Interpolate(a.density, b.density);
                        transitions.Add(new (a, b, point.coordinates + new Vector3(dx, dy, interpolateZ)));
                    }
                }
            }
            return transitions;
        }

        void CalculateTransitionGradients(List<Transition> transitions) {
            foreach (Transition transition in transitions) {
                (GridPoint, GridPoint) transitionDirection = (transition.from, transition.to);
                if (!transitionGradients.ContainsKey(transitionDirection)) {
                    transitionGradients[transitionDirection] = ApproximateNormals(transition.transitionPoint);
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
                    Vector3 gradient = transitionGradients[(transition.from, transition.to)];

                    force += -1f * Vector3.Dot(gradient, centerPoint - transitionPoint) * gradient;
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
        IEnumerable<Vector3Int> Volume(Vector3Int size, bool includeEdges = true) {
            size += includeEdges ? Vector3Int.one : Vector3Int.zero;
            for (int x = 0; x <= size.x; ++x) {
                for (int y = 0; y <= size.y; ++y) {
                    for (int z = 0; z <= size.z; ++z) {
                        yield return new Vector3Int(x, y, z);
                    }
                }
            }
        }
    }

    struct GridPoint {
        public readonly Vector3Int coordinates;
        public readonly float density;
        public bool Exists { get { return density > 0; } }

        public GridPoint(Vector3Int coordinates, float density) {
            this.coordinates = coordinates;
            this.density = density;
        }

        public override string ToString() {
            return $"{coordinates}";
        }
    }

    struct Octet {
        public readonly Vector3Int coordinates;
        public readonly GridPoint[,,] vertices;

        public Octet(Vector3Int coordinates, GridPoint[,,] vertices) {
            this.coordinates = coordinates;
            this.vertices = vertices;
        }


    }

    struct Vertex {
        public readonly Vector3Int gridCoordinates;
        public readonly List<Transition> transitions;
    }

    struct Transition {
        public readonly GridPoint from;
        public readonly GridPoint to;
        public readonly Vector3 transitionPoint;

        public Transition(GridPoint from, GridPoint to, Vector3 transitionPoint) {
            this.from = from;
            this.to = to;
            this.transitionPoint = transitionPoint;
        }

        public override string ToString() {
            return $"{from} -> {to}: {transitionPoint}";
        }
    }
}