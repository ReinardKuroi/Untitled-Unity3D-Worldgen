using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public float smoothDamp = 50f;
    GameObject cameraTarget;

    void Start()
    {
        cameraTarget = GameObject.Find("Player View");
    }

    void LateUpdate()
    {
        transform.position = Vector3.Slerp(transform.position, cameraTarget.transform.position, smoothDamp * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, cameraTarget.transform.rotation, smoothDamp * Time.deltaTime);
    }
}
