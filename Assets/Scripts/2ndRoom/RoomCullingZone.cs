using UnityEngine;

/// <summary>
/// RoomCullingZone — Unity 6000.3.9f1
///
/// Attach to an empty GameObject with a large BoxCollider (IsTrigger = true)
/// that covers the entire room. When the player enters, the listed layers
/// are hidden from the camera. When the player leaves, they are restored.
///
/// Uses distance-based detection (same as PortalRing) so it works with
/// CharacterController without needing Rigidbody on triggers.
///
/// ─── Setup ───────────────────────────────────────────────────────
///  1. Create an empty GameObject sized to cover the room.
///  2. Attach this script.
///  3. Add the layer names you want to HIDE when inside this room
///     to the "Layers To Hide" array (e.g. "HiddenRooms").
///  4. Assign Player Transform or tag player as "Player".
///  5. The camera is found automatically (Camera.main).
/// ─────────────────────────────────────────────────────────────────
/// </summary>
public class RoomCullingZone : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("Leave empty to auto-find by 'Player' tag")]
    public Transform playerTransform;

    [Header("Camera")]
    [Tooltip("Leave empty to use Camera.main")]
    public Camera targetCamera;

    [Header("Layers to hide when player is INSIDE this room")]
    [Tooltip("Add the layer names you want hidden inside this room.\n" +
             "Example: 'HiddenRooms'")]
    public string[] layersToHide;

    [Header("Zone Bounds")]
    [Tooltip("Size of the detection box in world units (covers the room)")]
    public Vector3 zoneSize = new Vector3(10f, 4f, 10f);

    // ── Internals ────────────────────────────────────────────────
    private bool   _playerInside    = false;
    private int    _hideMask        = 0;      // combined mask of layers to hide
    private int    _originalCulling = 0;

    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (playerTransform == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null)
            _originalCulling = targetCamera.cullingMask;

        // Build the hide mask from layer names
        _hideMask = 0;
        foreach (var layerName in layersToHide)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer == -1)
                Debug.LogWarning($"[RoomCullingZone] Layer '{layerName}' not found! Create it in Tags & Layers.");
            else
                _hideMask |= (1 << layer);
        }
    }

    void Update()
    {
        if (playerTransform == null || targetCamera == null) return;

        bool inside = IsPlayerInside();

        if (inside && !_playerInside)
        {
            // Player entered — hide the layers
            _playerInside = true;
            targetCamera.cullingMask &= ~_hideMask;
            Debug.Log($"[RoomCullingZone] '{gameObject.name}' — hiding layers, culling mask: {targetCamera.cullingMask}");
        }
        else if (!inside && _playerInside)
        {
            // Player left — restore the layers
            _playerInside = false;
            targetCamera.cullingMask |= _hideMask;
            Debug.Log($"[RoomCullingZone] '{gameObject.name}' — restoring layers, culling mask: {targetCamera.cullingMask}");
        }
    }

    bool IsPlayerInside()
    {
        // Check if player is within the box bounds in local space
        Vector3 localPos = transform.InverseTransformPoint(playerTransform.position);
        Vector3 half     = zoneSize * 0.5f;
        return Mathf.Abs(localPos.x) <= half.x &&
               Mathf.Abs(localPos.y) <= half.y &&
               Mathf.Abs(localPos.z) <= half.z;
    }

    // ── Gizmo ─────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color  = _playerInside
            ? new Color(1f, 0.3f, 0.3f, 0.2f)
            : new Color(0.3f, 1f, 0.3f, 0.2f);
        Gizmos.DrawCube(Vector3.zero, zoneSize);
        Gizmos.color = _playerInside ? Color.red : Color.green;
        Gizmos.DrawWireCube(Vector3.zero, zoneSize);
    }
}
