using UnityEngine;

/// <summary>
/// Player Controller for Unity 6 — works with this exact hierarchy:
///
///   Player                    (this script + CharacterController here)
///     ├── scene               (your GLTF model)
///     └── Main Camera         (child of Player)
///
/// SETUP:
/// 1. Attach this script to the Player GameObject.
/// 2. Make sure CharacterController is also on Player.
/// 3. Assign 'modelRoot'    → the "scene" child (your GLTF model).
/// 4. Assign 'playerCamera' → the Main Camera child.
/// 5. Done — no separate camera script needed.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Your GLTF model child — the 'scene' object")]
    public Transform modelRoot;
    [Tooltip("Main Camera — child of Player")]
    public Camera playerCamera;

    [Header("Movement")]
    public float walkSpeed   = 4f;
    public float runSpeed    = 8f;
    public float jumpHeight  = 1.5f;
    public float gravity     = -20f;

    [Header("Camera")]
    public float mouseSensitivity   = 2f;
    public float cameraDistance     = 5f;
    public float cameraHeightOffset = 1.6f;
    public float minPitch           = -20f;
    public float maxPitch           =  60f;

    [Header("Rotation")]
    public float rotationSmoothTime = 0.08f;

    // ── Private ───────────────────────────────────────────
    private CharacterController _cc;

    private float _yaw;
    private float _pitch = 15f;
    private float _rotVelocity;

    private Vector3 _velocity;
    private bool    _isGrounded;

    void Awake()
    {
        _cc  = GetComponent<CharacterController>();
        _yaw = transform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void Update()
    {
        GroundCheck();
        CameraLook();
        Move();
        Jump();
        ApplyGravity();
    }

    // ── Ground ────────────────────────────────────────────
    void GroundCheck()
    {
        _isGrounded = _cc.isGrounded;
        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;
    }

    // ── Camera (orbits player, mouse controlled) ──────────
    void CameraLook()
    {
        // Escape unlocks cursor, click re-locks
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        if (Cursor.lockState != CursorLockMode.Locked) return;

        _yaw   += Input.GetAxis("Mouse X") * mouseSensitivity;
        _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);

        if (playerCamera == null) return;

        // Orbit position around player pivot
        Vector3    pivot = transform.position + Vector3.up * cameraHeightOffset;
        Quaternion rot   = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3    desiredCamPos = pivot - rot * Vector3.forward * cameraDistance;

        // Pull camera in if geometry is in the way
        float actualDist = cameraDistance;
        if (Physics.SphereCast(pivot, 0.2f,
                               (desiredCamPos - pivot).normalized,
                               out RaycastHit hit, cameraDistance))
        {
            actualDist = Mathf.Max(0.5f, hit.distance - 0.1f);
        }

        Vector3 finalCamPos = pivot - rot * Vector3.forward * actualDist;
        playerCamera.transform.position = finalCamPos;
        playerCamera.transform.LookAt(pivot);
    }

    // ── Movement ──────────────────────────────────────────
    void Move()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        bool  running = Input.GetKey(KeyCode.LeftShift);

        Vector3 input = new Vector3(h, 0f, v).normalized;
        if (input.magnitude < 0.1f) return;

        float speed = running ? runSpeed : walkSpeed;

        // Angle relative to camera's horizontal yaw
        float targetAngle = Mathf.Atan2(input.x, input.z) * Mathf.Rad2Deg + _yaw;

        // Smoothly rotate the GLTF model to face movement direction
        if (modelRoot != null)
        {
            float smoothAngle = Mathf.SmoothDampAngle(
                modelRoot.eulerAngles.y, targetAngle,
                ref _rotVelocity, rotationSmoothTime);
            modelRoot.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
        }

        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        _cc.Move(moveDir * speed * Time.deltaTime);
    }

    // ── Jump ──────────────────────────────────────────────
    void Jump()
    {
        if (Input.GetButtonDown("Jump") && _isGrounded)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    // ── Gravity ───────────────────────────────────────────
    void ApplyGravity()
    {
        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * cameraHeightOffset, 0.15f);
    }
}
