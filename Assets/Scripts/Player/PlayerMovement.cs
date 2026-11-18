using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float sneakSpeed = 1.5f;
    [SerializeField] private float sprintSpeed = 6f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Mouse look")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private Transform cameraTransform;

    [Header("Camera height")]
    [SerializeField] private float standHeight = 0.6f;
    [SerializeField] private float crouchHeight = 0.1f;
    [SerializeField] private float hideHeight = -0.2f;
    [SerializeField] private float cameraSmooth = 8f;

    [Header("Field of view")]
    [SerializeField] private float normalFov = 60f;
    [SerializeField] private float sprintFov = 70f;

    [Header("Head bob")]
    [SerializeField] private float bobAmount = 0.05f;
    [SerializeField] private float bobFrequency = 3f;

    [Header("Hiding")]
    [SerializeField] private bool isHidden;
    [SerializeField] private int hidingSpotsNearby ;

    public bool IsHidden => isHidden;

    private CharacterController controller;
    private Camera playerCamera;
    private float fallSpeed;
    private float lookAngle;
    private float cameraHeight;
    private float bobTimer;

    void Awake()
    { 
        controller = GetComponent<CharacterController>();
        playerCamera = cameraTransform.GetComponent<Camera>();
        cameraHeight = standHeight;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    { 
        if (PauseMenu.IsPaused)
        {
            return;
        }

        HandleLook();
        HandleHiding();
        HandleCameraHeight();
        HandleFieldOfView();

        if (isHidden)
        {
            return;
        }

        HandleMovement();
    }

    void HandleLook()
    {
        float mouseX = Mouse.current.delta.ReadValue().x;
        transform.Rotate(0f, mouseX * mouseSensitivity, 0f);

        float mouseY = Mouse.current.delta.ReadValue().y;
        lookAngle -= mouseY * mouseSensitivity;
        lookAngle = Mathf.Clamp(lookAngle, -80f, 80f);
        cameraTransform.localRotation = Quaternion.Euler(lookAngle, 0f, 0f);
    }

    void HandleHiding()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame && hidingSpotsNearby > 0)
        {
            isHidden = !isHidden;
        }
    }

    void HandleCameraHeight()
    {
        float targetHeight;
        if (isHidden)
        {
            targetHeight = hideHeight;
        }
        else if (Keyboard.current.leftCtrlKey.isPressed)
        {
            targetHeight = crouchHeight;
        }
        else
        {
            targetHeight = standHeight;
        }
        cameraHeight = Mathf.Lerp(cameraHeight, targetHeight, cameraSmooth * Time.deltaTime);

        Vector3 flatVelocity = controller.velocity;
        flatVelocity.y = 0f;
        float moveSpeed = flatVelocity.magnitude;
        if (isHidden)
        {
            moveSpeed = 0f;
        }
        bobTimer += moveSpeed * bobFrequency * Time.deltaTime;
        float bobOffset = Mathf.Sin(bobTimer) * bobAmount * (moveSpeed / walkSpeed);

        cameraTransform.localPosition = new Vector3(0f, cameraHeight + bobOffset, 0f);
    }

    void HandleFieldOfView()
    {
        float targetFov;
        if (Keyboard.current.leftShiftKey.isPressed && !isHidden && !Keyboard.current.leftCtrlKey.isPressed)
        {
            targetFov = sprintFov;
        }
        else
        {
            targetFov = normalFov;
        }
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, cameraSmooth * Time.deltaTime);
    }

    void HandleMovement()
    {
        float currentSpeed;
        if (Keyboard.current.leftCtrlKey.isPressed)
        {
            currentSpeed = sneakSpeed;
        }
        else if (Keyboard.current.leftShiftKey.isPressed)
        {
            currentSpeed = sprintSpeed;
        }
        else
        {
            currentSpeed = walkSpeed;
        }

        Vector3 direction = Vector3.zero;
        if (Keyboard.current.wKey.isPressed)
        {
            direction += transform.forward;
        }
        if (Keyboard.current.aKey.isPressed)
        {
            direction -= transform.right;
        }
        if (Keyboard.current.dKey.isPressed)
        {
            direction += transform.right;
        }
        if (Keyboard.current.sKey.isPressed)
        {
            direction -= transform.forward;
        }
        direction = direction.normalized;

        if (controller.isGrounded)
        {
            fallSpeed = -2f;
        }
        fallSpeed += gravity * Time.deltaTime;

        Vector3 velocity = direction * currentSpeed;
        velocity.y = fallSpeed;
        controller.Move(velocity * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("HidingSpot"))
        {
            hidingSpotsNearby += 1;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("HidingSpot"))
        {
            hidingSpotsNearby -= 1;
        }
    }
}
