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
    bool holdPosition;

    ParticleSystem engineTrail;
    AudioSource engineSound;

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
        HandleMovement();
    }

    void FixedUpdate() {
        ApplyRigidbodyMovement();
    }
    void LateUpdate() {
        DrawEngineTrail();
        PlayEngineSound();
        GUIDrawReticles();
    }

    void InitDynamicObjects() {
        rBody = GetComponent<Rigidbody>();
        rotation = transform.rotation.eulerAngles;
        engineTrail = GameObject.Find("Engine Trail").GetComponent<ParticleSystem>();
        velocityReticle = GameObject.Find("Velocity Reticle").GetComponent<RectTransform>();
        headingReticle = GameObject.Find("Heading Reticle").GetComponent<RectTransform>();
        speedReadout = GameObject.Find("Speed Readout").GetComponent<Text>();
        engineSound = GameObject.Find("Engine").GetComponent<AudioSource>();
    }

    void LockCursor() {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void HandleInput() {
        mouseInput = new Vector2(Input.GetAxisRaw("Mouse X"), -1f * Input.GetAxisRaw("Mouse Y")) * sensitivity;
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

        if (Input.GetKey(KeyCode.F)) {
            holdPosition = true;
        } else {
            holdPosition = false;
        }

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

    void ApplyRigidbodyMovement() {
        rBody.AddForce(movementDirection * speed, ForceMode.Acceleration);
    }

    void HandleMovement() {
        rotation += mouseInput;
        rotation.y = Mathf.Clamp(rotation.y, -89.998f, 89.998f);
        transform.eulerAngles = Vector3.Lerp(transform.eulerAngles, new Vector3(rotation.y, rotation.x, 0f), Time.deltaTime * 600);

        if (holdPosition) {
            if (rBody.velocity.magnitude > 0.05f) {
                movementDirection = -rBody.velocity.normalized * Mathf.Sqrt(rBody.velocity.magnitude);
            } else {
                movementDirection = Vector3.zero;
                rBody.velocity = Vector3.zero;
                rBody.angularVelocity = Vector3.zero;
            }
        } else {
            movementDirection = transform.right * input.x + transform.forward * input.y;
        }
    }

    void DrawEngineTrail() {
        engineTrail.gameObject.transform.rotation = Quaternion.LookRotation(-transform.forward.normalized - movementDirection.normalized);

        if (movementDirection.magnitude > 0) {
            engineTrail.Play();
        } else if (engineTrail.isPlaying) {
            engineTrail.Stop();
        }
    }

    void PlayEngineSound() {
        engineSound.pitch = Mathf.Clamp(rBody.velocity.magnitude / 10f + 0.8f, 0.8f, 2.5f);
        if (movementDirection.magnitude > 0 && engineSound.volume == 0f) {
            engineSound.volume = 1f;
        } else if (movementDirection.magnitude == 0 && !(engineSound.volume == 0f)) {
            engineSound.volume = 0f;
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
