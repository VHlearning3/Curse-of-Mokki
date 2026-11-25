using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    // ----- Movement -----
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float sneakSpeed = 1.5f;
    [SerializeField] private float sprintSpeed = 6f;
    [SerializeField] private float gravity = -9.81f;
    private CharacterController controller;
    private float fallSpeed;

    [Header("Crouch")]
    [SerializeField] private float standColliderHeight = 2f;
    [SerializeField] private float crouchColliderHeight = 1.2f;
    [SerializeField] private bool isCrouching;

    // ----- Camera -----
    [Header("Mouse look")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private Transform cameraTransform;
    private float lookAngle;

    [Header("Camera height")]
    [SerializeField] private float standHeight = 0.6f;
    [SerializeField] private float crouchHeight = 0.1f;
    [SerializeField] private float hideHeight = -0.2f;
    [SerializeField] private float cameraSmooth = 8f;
    private float cameraHeight;

    [Header("Field of view")]
    [SerializeField] private float normalFov = 50f;
    [SerializeField] private float sprintFov = 60f;
    private Camera playerCamera;

    [Header("Head bob")]
    [SerializeField] private float bobAmount = 0.05f;
    [SerializeField] private float bobFrequency = 3f;
    private float bobTimer;

    [Header("Forced look")]
    [SerializeField] private float lookAtSmooth = 4f;
    [SerializeField] private bool freezeMovementWhileLooking = true;
    private float lookAtTimer;
    private Vector3 lookAtPoint;
    public bool IsLookingAt => lookAtTimer > 0f;

    // ----- Hiding -----
    [Header("Hiding")]
    [SerializeField] private bool isHidden;
    [SerializeField] private int hidingSpotsNearby;
    private PlayerMadness madness;
    public bool IsHidden => isHidden;

    // ----- Sounds -----
    [Header("Footsteps")]
    [SerializeField] private float stepDistance = 2f;
    [SerializeField] private float sneakStepVolume = 0.4f;
    [SerializeField] private float stepPitchVariation = 0.12f;
    [SerializeField] private float stepVolumeVariation = 0.25f;
    [SerializeField] private float stepRhythmVariation = 0.15f;
    private float stepProgress;

    void Awake()
    { 
        controller = GetComponent<CharacterController>();
        madness = GetComponent<PlayerMadness>();
        playerCamera = cameraTransform.GetComponent<Camera>();
        cameraHeight = standHeight;
        Cursor.lockState = CursorLockMode.Locked;
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

        HandleLook();
        HandleHiding();
        HandleCrouch();
        HandleCameraHeight();
        HandleFieldOfView();
        HandleMovement();
        HandleFootsteps();
        
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

    void HandleHiding()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame && hidingSpotsNearby > 0)
        {
            isHidden = !isHidden;
        }

        if (madness != null)
        {
            if (isHidden)
            {
                madness.BeginDrain(this, 1f);
            }
            else
            {
                madness.EndDrain(this);
            }
        }
    }

    void HandleCameraHeight()
    {
        float targetHeight;
        if (isHidden)
        {
            targetHeight = hideHeight;
        }
        else if (isCrouching)
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
        if (Keyboard.current.leftShiftKey.isPressed && !isHidden && !isCrouching)
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
        
        if (isHidden)
        {
            return;
        }
        float currentSpeed;
        if (isCrouching)
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

        if (lookAtTimer > 0f && freezeMovementWhileLooking)
        {
            direction = Vector3.zero;
        }

        if (controller.isGrounded)
        {
            fallSpeed = -2f;
        }
        fallSpeed += gravity * Time.deltaTime;

        Vector3 velocity = direction * currentSpeed;
        velocity.y = fallSpeed;
        controller.Move(velocity * Time.deltaTime);
    }
    void HandleFootsteps()
    {
        if (isHidden)
        {
            return;
        }
        Vector3 flatVelocity = controller.velocity;
        flatVelocity.y = 0f;
        float moveSpeed = flatVelocity.magnitude;
        stepProgress += moveSpeed * Time.deltaTime;

        if (stepProgress > stepDistance)
        {
            stepProgress = Random.Range(0f, stepRhythmVariation) * stepDistance;
            float stepVolume;
            if (isCrouching)
            {
                stepVolume = sneakStepVolume;
            }
            else
            {
                stepVolume = 1f;
            }

            string surface = GetGroundSurface();
            string stepSound = "SFX_Walk" + surface;
            bool isRunning = moveSpeed > (walkSpeed + sprintSpeed) / 2f;
            if (isRunning && AudioManager.Instance.HasClip("SFX_Run" + surface))
            {
                stepSound = "SFX_Run" + surface;
            }
            stepVolume *= Random.Range(1f - stepVolumeVariation, 1f);
            AudioManager.Instance.PlaySFX(stepSound, stepVolume, stepPitchVariation);
        }
    }

    string GetGroundSurface()
    {
        RaycastHit hit;
        bool groundBelow = Physics.Raycast(transform.position, Vector3.down, out hit, standColliderHeight / 2f + 0.5f, ~0, QueryTriggerInteraction.Ignore);
        if (groundBelow && hit.collider.CompareTag("Floor"))
        {
            return "Floor";
        }
        if (groundBelow && hit.collider.CompareTag("Cave"))
        {
            return "Cave";
        }
        return "Snow";
    }

    void HandleCrouch()
    {
        bool ceilingAbove = Physics.Raycast(transform.position, Vector3.up, standColliderHeight / 2f + 0.1f, ~0, QueryTriggerInteraction.Ignore);
        if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed)
        {
            isCrouching = true;
        }
        else if (!ceilingAbove)
        {
           isCrouching = false;
        }
        float targetColliderHeight;
        if (isCrouching) 
        {
            targetColliderHeight = crouchColliderHeight;
        }
        else
        {
            targetColliderHeight = standColliderHeight;
        }
        controller.height = Mathf.Lerp(controller.height, targetColliderHeight, cameraSmooth * Time.deltaTime);
        controller.center = new Vector3 (0f, (controller.height - standColliderHeight) / 2f, 0f);
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
