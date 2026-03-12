using UnityEngine;

/// <summary>
/// TorchItem.cs — Unity 6000.3.9f1
///
/// On pickup:
///   - Hides the world mesh/collider (base.OnPickup)
///   - Moves the torch mesh to the player's hand anchor (bottom-right of camera)
///   - Re-parents the Spotlight to the camera so it illuminates forward
///
/// On drop:
///   - Returns mesh and light back to the world object
///   - Re-enables mesh/collider (base.OnDrop)
/// </summary>
public class TorchItem : InventoryItem
{
    [Header("Torch Settings")]
    public KeyCode toggleKey = KeyCode.T;

    [Header("Light (auto-found in children if empty)")]
    [Tooltip("Assign a Spotlight child here. It will be moved to the camera on pickup.")]
    public Light torchLight;

    [Header("Held mesh position — tweak these in Play Mode")]
    [Tooltip("Where the torch appears relative to the camera (right, down, forward)")]
    public Vector3 heldMeshOffset = new Vector3(0.28f, -0.28f, 0.55f);
    public Vector3 heldMeshRotation = new Vector3(0f, 0f, 0f);
    public Vector3 heldMeshScale = new Vector3(1f, 1f, 1f);

    [Header("Light offset relative to camera")]
    public Vector3 heldLightOffset = new Vector3(0.28f, -0.20f, 0.85f);

    // ── Private state ─────────────────────────────────────────────
    private bool _isOn = false;
    private Camera _cam;

    // Original transform data for restoring on drop
    private Transform _originalLightParent;
    private Vector3 _originalLightLocalPos;
    private Quaternion _originalLightLocalRot;

    private Transform _originalMeshParent;
    private Vector3 _originalMeshLocalPos;
    private Quaternion _originalMeshLocalRot;
    private Vector3 _originalMeshLocalScale;

    // The visual mesh child (first child that has a Renderer)
    private Transform _meshTransform;

    // ── Awake ─────────────────────────────────────────────────────
    void Awake()
    {
        itemName = "Torch";
        description = "Press T to toggle flashlight.";

        // Auto-find light
        if (torchLight == null)
            torchLight = GetComponentInChildren<Light>();

        // Configure as a proper spotlight if not already
        if (torchLight != null)
        {
            torchLight.type = LightType.Spot;
            torchLight.spotAngle = 80f;           // wider circular beam
            torchLight.innerSpotAngle = 55f;      // soft falloff edge
            torchLight.range = 30f;
            torchLight.intensity = 5f;
            torchLight.color = new Color(1f, 0.95f, 0.85f); // warm white
            torchLight.enabled = false;

            _originalLightParent = torchLight.transform.parent;
            _originalLightLocalPos = torchLight.transform.localPosition;
            _originalLightLocalRot = torchLight.transform.localRotation;
        }

        // Find the mesh (first Renderer in children, skip the light object)
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            _meshTransform = renderers[0].transform;
            _originalMeshParent = _meshTransform.parent;
            _originalMeshLocalPos = _meshTransform.localPosition;
            _originalMeshLocalRot = _meshTransform.localRotation;
            _originalMeshLocalScale = _meshTransform.localScale;
        }
    }

    // ── Pickup / Drop ─────────────────────────────────────────────

    public override void OnPickup()
    {
        base.OnPickup(); // hides all renderers + colliders on world object
        torchLight.cullingMask = ~(1 << LayerMask.NameToLayer("HeldItem"));

        _cam = Camera.main ?? FindFirstObjectByType<Camera>();
        if (_cam == null) return;

        // ── Move MESH to camera (hand position) ──
        if (_meshTransform != null)
        {
            _meshTransform.GetComponent<Renderer>().enabled = true; // re-show just the mesh
            _meshTransform.SetParent(_cam.transform, false);
            _meshTransform.localPosition = heldMeshOffset;
            _meshTransform.localRotation = Quaternion.Euler(heldMeshRotation);
            _meshTransform.localScale = heldMeshScale;
        }

        // ── Move LIGHT to camera ──
        if (torchLight != null)
        {
            torchLight.transform.SetParent(_cam.transform, false);
            torchLight.transform.localPosition = heldLightOffset;
            torchLight.transform.localRotation = Quaternion.identity; // points forward
        }
    }

    public override void OnDrop(Vector3 worldPosition)
    {
        // Return mesh to original parent first, then base restores visibility
        if (_meshTransform != null)
        {
            _meshTransform.SetParent(_originalMeshParent, false);
            _meshTransform.localPosition = _originalMeshLocalPos;
            _meshTransform.localRotation = _originalMeshLocalRot;
            _meshTransform.localScale = _originalMeshLocalScale;
        }

        // Return light to torch
        if (torchLight != null)
        {
            torchLight.transform.SetParent(_originalLightParent, false);
            torchLight.transform.localPosition = _originalLightLocalPos;
            torchLight.transform.localRotation = _originalLightLocalRot;
            torchLight.cullingMask = ~0;
        }

        // Turn off light when dropped
        SetTorch(false);

        base.OnDrop(worldPosition); // re-enables renderers + colliders
    }

    // ── Hotkey ────────────────────────────────────────────────────

    public override void OnInventoryUpdate()
    {
        if (Input.GetKeyDown(toggleKey))
            SetTorch(!_isOn);
    }

    public void SetTorch(bool state)
    {
        _isOn = state;
        if (torchLight != null)
            torchLight.enabled = _isOn;
        Debug.Log($"[Torch] {(_isOn ? "ON" : "OFF")}");
    }

    public override void OnSelect() => Debug.Log("[Torch] Press T to toggle.");
    public override void OnDeselect() => Debug.Log("[Torch] Deselected.");
}