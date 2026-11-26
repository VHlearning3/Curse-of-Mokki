using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    // ----- Camera -----
    [SerializeField] private Transform cameraTransform;
    private Camera playerCamera;
    private PlayerMovement movement;

    [Header("Mouse look")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    private float lookAngle;

    [Header("Forced look")]
    [SerializeField] private float lookAtSmooth = 4f;
    private float lookAtTimer;
    private Vector3 lookAtPoint;
    public bool IsLookingAt => lookAtTimer > 0f;

    [Header("Camera height")]
    [SerializeField] private float standHeight = 0.6f;
    [SerializeField] private float crouchHeight = 0.1f;
    [SerializeField] private float hideHeight = -0.2f;
    [SerializeField] private float cameraSmooth = 8f;
    private float cameraHeight;

    [Header("Head bob")]
    [SerializeField] private float bobAmount = 0.05f;
    [SerializeField] private float bobFrequency = 3f;
    private float bobTimer;

    [Header("Field of view")]
    [SerializeField] private float normalFov = 50f;
    [SerializeField] private float sprintFov = 60f;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        if (cameraTransform == null)
        {
            cameraTransform = GetComponentInChildren<Camera>().transform;
        }
        playerCamera = cameraTransform.GetComponent<Camera>();
        cameraHeight = standHeight;
    }

    void Start()
    {
        foreach (SanityLostZone zone in FindObjectsByType<SanityLostZone>(FindObjectsSortMode.None))
        {
            zone.OnTriggered += OnSanityLostZoneTriggered;
        }
    }

    void OnSanityLostZoneTriggered(SanityLostZone zone)
    {
        if (zone.FocusPoint != null)
        {
            LookAt(zone.FocusPoint.position, zone.Duration);
        }
    }

    void Update()
    {
        if (MenuUtility.AnyMenuOpen)
        {
            return;
        }
        if (!movement.enabled || !movement.ControlEnabled)
        {
            return;
        }

        HandleLook();
        HandleCameraHeight();
        HandleFieldOfView();
    }

    void HandleLook()
    {
        if (lookAtTimer > 0f)
        {
            HandleForcedLook();
            return;
        }
        float mouseX = Mouse.current.delta.ReadValue().x;
        transform.Rotate(0f, mouseX * mouseSensitivity, 0f);

        float mouseY = Mouse.current.delta.ReadValue().y;
        lookAngle -= mouseY * mouseSensitivity;
        lookAngle = Mathf.Clamp(lookAngle, -80f, 80f);
        cameraTransform.localRotation = Quaternion.Euler(lookAngle, 0f, 0f);
    }

    public void LookAt(Vector3 point, float seconds)
    {
        lookAtPoint = point;
        lookAtTimer = seconds;
    }

    void HandleForcedLook()
    {
        lookAtTimer -= Time.deltaTime;

        Vector3 toPoint = lookAtPoint - cameraTransform.position;
        Vector3 flatToPoint = new Vector3(toPoint.x, 0f, toPoint.z);

        float targetYaw = Mathf.Atan2(toPoint.x, toPoint.z) * Mathf.Rad2Deg;
        float targetPitch = -Mathf.Atan2(toPoint.y, flatToPoint.magnitude) * Mathf.Rad2Deg;
        targetPitch = Mathf.Clamp(targetPitch, -80f, 80f);

        float yaw = Mathf.LerpAngle(transform.eulerAngles.y, targetYaw, lookAtSmooth * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        lookAngle = Mathf.Lerp(lookAngle, targetPitch, lookAtSmooth * Time.deltaTime);
        cameraTransform.localRotation = Quaternion.Euler(lookAngle, 0f, 0f);
    }

    void HandleCameraHeight()
    {
        float targetHeight;
        if (movement.IsHidden)
        {
            targetHeight = hideHeight;
        }
        else if (movement.IsCrouching)
        {
            targetHeight = crouchHeight;
        }
        else
        {
            targetHeight = standHeight;
        }
        cameraHeight = Mathf.Lerp(cameraHeight, targetHeight, cameraSmooth * Time.deltaTime);

        float moveSpeed = movement.GetGroundSpeed();
        if (movement.IsHidden)
        {
            moveSpeed = 0f;
        }
        bobTimer += moveSpeed * bobFrequency * Time.deltaTime;
        float bobOffset = Mathf.Sin(bobTimer) * bobAmount * (moveSpeed / movement.WalkSpeed);

        cameraTransform.localPosition = new Vector3(0f, cameraHeight + bobOffset, 0f);
    }

    void HandleFieldOfView()
    {
        float targetFov;
        if (movement.IsSprinting)
        {
            targetFov = sprintFov;
        }
        else
        {
            targetFov = normalFov;
        }
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, cameraSmooth * Time.deltaTime);
    }
}
