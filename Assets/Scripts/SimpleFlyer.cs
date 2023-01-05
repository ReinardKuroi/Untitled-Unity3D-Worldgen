using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class SimpleFlyer : MonoBehaviour
{
    [Range(1f, 10f)]
    public float sensitivity = 5f;

    [Range(0.1f, 3f)]
    public float speed = 1f;
    private Vector2 rotation = new();
    Rigidbody rigidbody;

    ParticleSystem engineTrail;

    RectTransform velocityReticle;
    RectTransform headingReticle;
    Text speedReadout;

    void Start() {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        rigidbody = GetComponent<Rigidbody>();
        rotation = transform.rotation.eulerAngles;

        engineTrail = GameObject.Find("Engine Trail").GetComponent<ParticleSystem>();

        velocityReticle = GameObject.Find("Velocity Reticle").GetComponent<RectTransform>();
        headingReticle = GameObject.Find("Heading Reticle").GetComponent<RectTransform>();
        speedReadout = GameObject.Find("Speed Readout").GetComponent<Text>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
        Vector2 mouseInput = new Vector2(Input.GetAxisRaw("Mouse X"), -1f * Input.GetAxisRaw("Mouse Y")) * sensitivity * Time.deltaTime * 360;
        rotation += mouseInput;
        rotation.y = Mathf.Clamp(rotation.y, -89.998f, 89.998f);
        transform.eulerAngles = new Vector3(rotation.y, rotation.x, 0f);

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        Vector3 direction = transform.right * input.x + transform.forward * input.y;
        rigidbody.AddForce(direction * Time.deltaTime * speed * 100, ForceMode.Acceleration);

        engineTrail.gameObject.transform.rotation = Quaternion.LookRotation(-transform.forward.normalized - direction.normalized);

        if (input.magnitude > 0 && !engineTrail.isPlaying) {
            engineTrail.Play();
        } else if (input.magnitude == 0 && engineTrail.isPlaying) {
            engineTrail.Stop();
        }
    }

    void LateUpdate() {
        bool headingTowards = (Vector3.Dot(rigidbody.velocity, transform.forward) >= 0);
        Vector3 velocityHeading = Quaternion.LookRotation(rigidbody.velocity).eulerAngles;
        if (rigidbody.velocity.magnitude > 0.01f && headingTowards) {
            velocityReticle.gameObject.SetActive(true);
        } else {
            velocityReticle.gameObject.SetActive(false);
        }
        velocityReticle.position = RectTransformUtility.WorldToScreenPoint(Camera.main, transform.position + rigidbody.velocity.normalized * 3);
        headingReticle.position = RectTransformUtility.WorldToScreenPoint(Camera.main, transform.position + transform.forward.normalized * 3);
        speedReadout.text = $"SPD: {(headingTowards ? rigidbody.velocity.magnitude : -rigidbody.velocity.magnitude),6:0.00} MS\n" +
            $"HDX: {velocityHeading.x,6:0.00} DG\n" +
            $"HDY {velocityHeading.y,6:0.00} DG\n" +
            $"HDZ {velocityHeading.z,6:0.00} DG";
    }
}
