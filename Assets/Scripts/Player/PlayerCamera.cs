using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    // ----- Camera -----
    [SerializeField] private Transform cameraTransform;
    private PlayerMovement movement;

    [Header("Mouse look")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    private float lookAngle;

    [Header("Forced look")]
    [SerializeField] private float lookAtSmooth = 4f;
    private float lookAtTimer;
    private Vector3 lookAtPoint;
    public bool IsLookingAt => lookAtTimer > 0f;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        if (cameraTransform == null)
        {
            cameraTransform = GetComponentInChildren<Camera>().transform;
        }
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
        if (movement != null && !movement.enabled)
        {
            return;
        }

        HandleLook();
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
}
