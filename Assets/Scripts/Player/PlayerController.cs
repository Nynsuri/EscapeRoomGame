using UnityEngine;

/// <summary>
/// PlayerController — Unity 6000.3.9f1
/// Handles WASD movement, mouse-look camera, adjustable camera height, and adjustable capsule collider.
/// Attach to a GameObject that has a CharacterController component.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  MOVEMENT
    // ─────────────────────────────────────────────
    [Header("Movement")]
    [Tooltip("Walking speed in units/s")]
    public float moveSpeed = 5f;
    [Tooltip("Multiplier applied while holding Shift")]
    public float sprintMultiplier = 2f;
    [Tooltip("Gravity applied every frame")]
    public float gravity = -9.81f;

    // ─────────────────────────────────────────────
    //  CAMERA
    // ─────────────────────────────────────────────
    [Header("Camera")]
    [Tooltip("Drag the Camera (child of this object) here")]
    public Transform cameraTransform;
    [Tooltip("Mouse sensitivity")]
    public float mouseSensitivity = 100f;
    [Tooltip("Local Y position of the camera (camera height)")]
    [Range(0f, 3f)]
    public float cameraHeight = 1.7f;

    // ─────────────────────────────────────────────
    //  COLLIDER
    // ─────────────────────────────────────────────
    [Header("Collider")]
    [Tooltip("Height of the CapsuleCollider")]
    [Range(0.5f, 3f)]
    public float colliderHeight = 1.8f;
    [Tooltip("Radius of the CapsuleCollider")]
    [Range(0.1f, 1f)]
    public float colliderRadius = 0.4f;
    [Tooltip("Center offset of the CapsuleCollider")]
    public Vector3 colliderCenter = new Vector3(0f, 0.9f, 0f);

    // ─────────────────────────────────────────────
    //  JUMP
    // ─────────────────────────────────────────────
    [Header("Jump")]
    [Tooltip("Key to jump")]
    public KeyCode jumpKey = KeyCode.Space;
    [Tooltip("How high the player jumps (units)")]
    public float jumpHeight = 1.2f;

    // ─────────────────────────────────────────────
    //  CROUCH
    // ─────────────────────────────────────────────
    [Header("Crouch")]
    [Tooltip("Hold or toggle crouch")]
    public KeyCode crouchKey = KeyCode.LeftControl;
    [Tooltip("Movement speed while crouching")]
    public float crouchSpeed = 2f;
    [Tooltip("Collider height while crouching")]
    public float crouchColliderHeight = 1.0f;
    [Tooltip("Collider center while crouching")]
    public Vector3 crouchColliderCenter = new Vector3(0f, 0.5f, 0f);
    [Tooltip("Camera height while crouching")]
    public float crouchCameraHeight = 0.8f;
    [Tooltip("How fast the camera/collider lerp between stand and crouch")]
    public float crouchTransitionSpeed = 10f;

    // ─────────────────────────────────────────────
    //  PRIVATE
    // ─────────────────────────────────────────────
    private CharacterController _cc;
    private Vector3 _velocity;
    private float _xRotation;

    private bool _isCrouching = false;
    private float _standColliderHeight;
    private Vector3 _standColliderCenter;
    private float _standCameraHeight;

    // ─────────────────────────────────────────────

    void Awake()
    {
        _cc = GetComponent<CharacterController>();

        // Lock & hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Auto-find camera if not assigned
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                cameraTransform = cam.transform;
            else
                Debug.LogError("[PlayerController] No camera found! Create a Camera as a child of the Player and assign it to Camera Transform.");
        }

        // CRITICAL: re-parent camera to this player if it isn't already a child
        if (cameraTransform != null && cameraTransform.parent != transform)
        {
            cameraTransform.SetParent(transform, false);
            Debug.Log("[PlayerController] Camera was not a child of Player — re-parented automatically.");
        }

        // Save standing defaults so crouch can restore them
        _standColliderHeight = colliderHeight;
        _standColliderCenter = colliderCenter;
        _standCameraHeight   = cameraHeight;

        ApplyColliderSettings();
        ApplyCameraHeight();
    }

    void Update()
    {
        HandleMovement();
        HandleJump();
        HandleCrouch();
        HandleMouseLook();
        ApplyCameraHeight();
        ApplyColliderSettings();
    }

    // ─── Movement ─────────────────────────────────

    void HandleMovement()
    {
        if (_cc.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical   = Input.GetAxis("Vertical");

        Vector3 direction = transform.right * horizontal + transform.forward * vertical;

        float speed;
        if (_isCrouching)
            speed = crouchSpeed;
        else
            speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);

        _cc.Move(direction * speed * Time.deltaTime);

        // Gravity
        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }

    // ─── Jump ─────────────────────────────────────

    void HandleJump()
    {
        // Can only jump when grounded and not crouching
        if (_cc.isGrounded && !_isCrouching && Input.GetKeyDown(jumpKey))
        {
            // v = sqrt(h * -2 * g)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    // ─── Crouch ───────────────────────────────────

    void HandleCrouch()
    {
        // Toggle on key press (only allowed while grounded)
        if (Input.GetKeyDown(crouchKey) && _cc.isGrounded)
        {
            if (_isCrouching)
                TryStandUp();
            else
                StartCrouch();
        }

        // Smoothly lerp collider and camera toward target values
        float targetColliderH  = _isCrouching ? crouchColliderHeight  : _standColliderHeight;
        Vector3 targetCenter   = _isCrouching ? crouchColliderCenter  : _standColliderCenter;
        float targetCameraH    = _isCrouching ? crouchCameraHeight    : _standCameraHeight;

        float t = crouchTransitionSpeed * Time.deltaTime;
        colliderHeight  = Mathf.Lerp(colliderHeight,  targetColliderH, t);
        colliderCenter  = Vector3.Lerp(colliderCenter, targetCenter,    t);
        cameraHeight    = Mathf.Lerp(cameraHeight,    targetCameraH,   t);
    }

    void StartCrouch()
    {
        _isCrouching = true;
    }

    void TryStandUp()
    {
        // Cast upward to check if there is headroom to stand
        float standHeadroom = _standColliderHeight - crouchColliderHeight;
        Vector3 origin = transform.position + Vector3.up * crouchColliderHeight;

        if (Physics.Raycast(origin, Vector3.up, standHeadroom))
        {
            Debug.Log("[PlayerController] Can't stand up — ceiling above!");
            return;
        }

        _isCrouching = false;
    }

    // ─── Mouse Look ───────────────────────────────

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);

        // Rotate camera up/down
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

        // Rotate player body left/right
        transform.Rotate(Vector3.up * mouseX);
    }

    // ─── Helpers ──────────────────────────────────

    void ApplyCameraHeight()
    {
        if (cameraTransform == null) return;
        Vector3 pos = cameraTransform.localPosition;
        pos.y = cameraHeight;
        cameraTransform.localPosition = pos;
    }

    void ApplyColliderSettings()
    {
        _cc.height = colliderHeight;
        _cc.radius = colliderRadius;
        _cc.center = colliderCenter;
    }
}
