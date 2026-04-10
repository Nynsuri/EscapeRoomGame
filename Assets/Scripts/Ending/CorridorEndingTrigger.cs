using UnityEngine;

/// <summary>
/// CorridorEndingTrigger — Unity 6000.3.9f1
///
/// Place this on a trigger zone in the corridor to load the ending scene
/// when the player walks through it.
///
/// Follows the same pattern as PortalSideTrigger.
///
/// ─── Hierarchy ───────────────────────────────────────────────────
///   CorridorRoot  (empty, static)
///   ├── ... (corridor meshes)
///   └── EndingTriggerZone       ← this script here
///         BoxCollider            (IsTrigger = true)
///         Rigidbody              (IsKinematic = true, UseGravity = false)
///         CorridorEndingTrigger
/// ─────────────────────────────────────────────────────────────────
///
/// SETUP:
///   1. Create an empty GameObject inside the corridor where you want
///      the ending to fire (e.g. at the far end of the corridor).
///   2. Add a BoxCollider — tick IsTrigger. Size it to span the corridor width.
///   3. Add a Rigidbody — tick IsKinematic, untick UseGravity.
///   4. Add this script.
///   5. Set Ending Scene Name in the Inspector (default: "EndingScene").
///   6. Optionally tick Require All Collectibles to only trigger the
///      ending when CollectibleManager.IsComplete is true — otherwise
///      the player just can't pass until they have all relics.
/// </summary>
public class CorridorEndingTrigger : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Name of the ending scene to load")]
    public string endingSceneName = "EndingScene";

    [Header("Collectible Gate (optional)")]
    [Tooltip("If true, the ending only triggers when all collectibles are placed.\n" +
             "If false, the ending always triggers — CollectibleManager decides good/bad ending.")]
    public bool requireAllCollectibles = false;

    [Header("Blocked Message")]
    [Tooltip("Show an on-screen message when the player is blocked by missing collectibles")]
    public bool showBlockedMessage = true;
    public string blockedMessage = "The relics must be placed before you can leave.";
    public float messageDuration = 3f;

    // ── State ─────────────────────────────────────────────────────
    private bool _triggered = false;
    private float _messageTimer = 0f;
    private GUIStyle _messageStyle;

    // ─────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_triggered) return;

        // If a collectible gate is active, check whether all are placed
        if (requireAllCollectibles)
        {
            bool complete = CollectibleManager.Instance != null
                         && CollectibleManager.Instance.IsComplete;

            if (!complete)
            {
                if (showBlockedMessage)
                    _messageTimer = messageDuration;

                Debug.Log("[CorridorEndingTrigger] Blocked — not all collectibles placed.");
                return;
            }
        }

        _triggered = true;
        Debug.Log("[CorridorEndingTrigger] Player reached the end — loading ending.");
        EndingTrigger.LoadEnding(endingSceneName);
    }

    // ── Blocked message GUI ───────────────────────────────────────

    private void Update()
    {
        if (_messageTimer > 0f)
            _messageTimer -= Time.deltaTime;
    }

    private void OnGUI()
    {
        if (_messageTimer <= 0f || !showBlockedMessage) return;

        InitStyle();

        const float w = 600f, h = 44f;
        float x = (Screen.width  - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f + 80f;

        GUI.color = Color.black;
        GUI.Label(new Rect(x + 2, y + 2, w, h), blockedMessage, _messageStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(x,     y,     w, h), blockedMessage, _messageStyle);
    }

    private void InitStyle()
    {
        if (_messageStyle != null) return;
        _messageStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(1f, 0.4f, 0.3f) }
        };
    }

    // ── Gizmo — shows the trigger zone in the editor ──────────────

    private void OnDrawGizmosSelected()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color  = new Color(1f, 0.3f, 0.1f, 0.35f);
        Gizmos.DrawCube(col.center, col.size);
        Gizmos.color  = new Color(1f, 0.3f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(col.center, col.size);
    }
}
