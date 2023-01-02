using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator {
    public class Point : MonoBehaviour {
        public Vector3Int coordinates;

        public static Point CreatePoint(Vector3Int coordinates) {
            GameObject prefab;
            GameObject pointGameObject;
            Point point;
            string resourcePath;

            resourcePath = "Prefabs/Point (black)";
            prefab = Resources.Load<GameObject>(resourcePath);
            pointGameObject = Instantiate(prefab, coordinates, Quaternion.identity);
            point = pointGameObject.AddComponent<Point>();
            point.coordinates = coordinates;
            point.gameObject.name = $"Point ({coordinates.x} {coordinates.y} {coordinates.z})";
            return point;
        }
    }
}
