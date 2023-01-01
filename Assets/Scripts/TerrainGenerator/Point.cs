using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Point : MonoBehaviour {
    public Vector3Int coordinates;

    public static Point CreatePoint(Vector3Int coordinates, bool isWhite = true) {
        GameObject prefab;
        GameObject pointGameObject;
        Point point;
        string resourcePath;

        if (isWhite) {
            resourcePath = "Prefabs/Point (white)";
        } else {
            resourcePath = "Prefabs/Point (black)";
        }

        prefab = Resources.Load<GameObject>(resourcePath);
        pointGameObject = Instantiate(prefab, coordinates, Quaternion.identity);
        point = pointGameObject.AddComponent<Point>();
        point.coordinates = coordinates;
        point.gameObject.name = $"Point ({coordinates.x} {coordinates.y} {coordinates.z})";
        return point;
    }
}
