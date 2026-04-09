using UnityEngine;

/// <summary>
/// PortalBlocker — Unity 6000.3.9f1
///
/// An invisible opaque quad/mesh placed in front of a window or opening
/// that hides the room beyond when the player is on the corridor side.
/// Gets disabled (revealing the room) when the player enters via the portal,
/// re-enabled when the player leaves.
///
/// ─── Setup ───────────────────────────────────────────────────────
///  1. Create a Quad (or any mesh) sized to cover the window opening.
///  2. Assign a plain opaque material that matches the wall colour, OR
///     use the included "invisible" option (Camera.cullingMask trick below).
///  3. Attach this script to the blocker GameObject.
///  4. In PortalRing inspector, add this to the "Room Blockers" array.
///
/// The blocker is ON (hiding room) when player is on corridor side.
/// The blocker is OFF (room visible) when player is inside the room.
/// ─────────────────────────────────────────────────────────────────
/// </summary>
public class PortalBlocker : MonoBehaviour
{
    [Header("Blocker Settings")]
    [Tooltip("If true, only the MeshRenderer is toggled (GameObject stays active).\n" +
             "If false, the entire GameObject is enabled/disabled.")]
    public bool rendererOnlyMode = true;

    // ── State ─────────────────────────────────────────────────────
    private MeshRenderer _renderer;

    void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
    }

    /// <summary>Call with true when player is on corridor side (blocker visible).</summary>
    public void SetBlocking(bool blocking)
    {
        if (rendererOnlyMode && _renderer != null)
            _renderer.enabled = blocking;
        else
            gameObject.SetActive(blocking);
    }
}
