using UnityEngine;
using UnityEngine.InputSystem;

public class NoclipController : MonoBehaviour {
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float baseSpeed = 10f;
    [SerializeField] private float sprintMultiplier = 2f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 90f;

    private float rotationX; // lewo/prawo (Player)
    private float rotationY; // góra/dół (Camera)

    private void Start() {
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update() {
        HandleLook();
        HandleMovement();
    }

    private void HandleLook() {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        // poziom → obrót całego obiektu
        rotationX += mouseDelta.x * mouseSensitivity * Time.deltaTime;
        transform.rotation = Quaternion.Euler(0f, rotationX, 0f);

        // pion → tylko kamera
        rotationY -= mouseDelta.y * mouseSensitivity * Time.deltaTime;
        rotationY = Mathf.Clamp(rotationY, -maxLookAngle, maxLookAngle);

        cameraTransform.localRotation = Quaternion.Euler(rotationY, 0f, 0f);
    }

    private void HandleMovement() {
        var keyboard = Keyboard.current;

        float x = 0f;
        float z = 0f;
        float y = 0f;

        if (keyboard.aKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed) x += 1f;
        if (keyboard.wKey.isPressed) z += 1f;
        if (keyboard.sKey.isPressed) z -= 1f;

        if (keyboard.spaceKey.isPressed) y += 1f;
        if (keyboard.leftCtrlKey.isPressed) y -= 1f;

        bool isSprinting = keyboard.leftShiftKey.isPressed;
        float speed = isSprinting ? baseSpeed * sprintMultiplier : baseSpeed;

        Vector3 move = transform.right * x
                     + transform.forward * z
                     + transform.up * y;

        transform.position += move * speed * Time.deltaTime;
    }
}