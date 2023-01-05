using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotator : MonoBehaviour
{
    float speed = 15f;
    void FixedUpdate()
    {
        transform.RotateAround(transform.position, Vector3.up, Time.deltaTime * speed);
    }
}
