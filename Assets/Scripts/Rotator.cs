using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotator : MonoBehaviour
{
    [Range(0f, 30f)]
    public float speed = 15f;
    void FixedUpdate()
    {
        transform.RotateAround(transform.position, Vector3.up, Time.deltaTime * speed);
    }
}
