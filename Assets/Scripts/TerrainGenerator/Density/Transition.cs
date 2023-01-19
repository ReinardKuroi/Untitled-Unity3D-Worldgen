using System;
using System.Collections.Generic;
using UnityEngine;


namespace TerrainGenerator {
    public class Transition {
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