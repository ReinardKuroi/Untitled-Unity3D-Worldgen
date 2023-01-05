using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class SimpleFlyer : MonoBehaviour
{
    [Range(1f, 10f)]
    public float sensitivity = 5f;

    [Range(0.1f, 3f)]
    public float speed = 1f;

    Rigidbody rBody;
    Vector2 input;
    Vector2 mouseInput;
    Vector2 rotation = new();
    Vector3 movementDirection;

    ParticleSystem engineTrail;

    RectTransform velocityReticle;
    RectTransform headingReticle;
    Text speedReadout;

    void Start() {
        LockCursor();
        InitDynamicObjects();
    }

    void Update()
    {
        HandleInput();
        DrawEngineTrail();
    }

    void FixedUpdate() {
        HandleMovement();
    }
    void LateUpdate() {
        GUIDrawReticles();
    }

    void InitDynamicObjects() {
        rBody = GetComponent<Rigidbody>();
        rotation = transform.rotation.eulerAngles;
        engineTrail = GameObject.Find("Engine Trail").GetComponent<ParticleSystem>();
        velocityReticle = GameObject.Find("Velocity Reticle").GetComponent<RectTransform>();
        headingReticle = GameObject.Find("Heading Reticle").GetComponent<RectTransform>();
        speedReadout = GameObject.Find("Speed Readout").GetComponent<Text>();
    }

    void LockCursor() {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void HandleInput() {
        mouseInput = new Vector2(Input.GetAxisRaw("Mouse X"), -1f * Input.GetAxisRaw("Mouse Y")) * sensitivity;
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

        if (Input.GetKeyDown(KeyCode.Escape)) {
            Exit();
        }
    }

    private void Exit() {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void HandleMovement() {
        rotation += mouseInput;
        rotation.y = Mathf.Clamp(rotation.y, -89.998f, 89.998f);
        transform.eulerAngles = Vector3.Lerp(transform.eulerAngles, new Vector3(rotation.y, rotation.x, 0f), 10);

        movementDirection = transform.right * input.x + transform.forward * input.y;
        rBody.AddForce(movementDirection * speed, ForceMode.Acceleration);
    }

    void DrawEngineTrail() {
        engineTrail.gameObject.transform.rotation = Quaternion.LookRotation(-transform.forward.normalized - movementDirection.normalized);

        if (input.magnitude > 0 && !engineTrail.isPlaying) {
            engineTrail.Play();
        } else if (input.magnitude == 0 && engineTrail.isPlaying) {
            engineTrail.Stop();
        }
    }

    void GUIDrawReticles() {
        bool headingTowards = (Vector3.Dot(rBody.velocity, transform.forward) >= 0);
        Vector3 velocityHeading = Quaternion.LookRotation(rBody.velocity).eulerAngles;
        if (rBody.velocity.magnitude > 0.01f && headingTowards) {
            velocityReticle.gameObject.SetActive(true);
        } else {
            velocityReticle.gameObject.SetActive(false);
        }
        velocityReticle.position = Vector2.Lerp(velocityReticle.position, RectTransformUtility.WorldToScreenPoint(Camera.main, transform.position + rBody.velocity.normalized * 3), Time.deltaTime * 10);
        headingReticle.position = Vector2.Lerp(headingReticle.position, RectTransformUtility.WorldToScreenPoint(Camera.main, transform.position + transform.forward.normalized * 3), Time.deltaTime * 10);
        speedReadout.text = $"SPD: {(headingTowards ? rBody.velocity.magnitude : -rBody.velocity.magnitude),6:0.00} MS\n" +
            $"HDX: {velocityHeading.x,6:0.00} DG\n" +
            $"HDY {velocityHeading.y,6:0.00} DG\n" +
            $"HDZ {velocityHeading.z,6:0.00} DG";
    }
}
