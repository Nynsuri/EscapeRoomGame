using UnityEngine;

/// <summary>
/// HologramMaterialToggle — Unity 6000.3.9f1
///
/// Attach to the hologram Quad. Swaps between two materials each
/// time the player interacts with it. Works with the existing
/// raycast interaction pattern.
///
/// ─── Setup ───────────────────────────────────────────────────────
///  1. Attach to the Quad GameObject.
///  2. Set Material A — the starting material (e.g. Galaxy).
///  3. Set Material B — the material to swap to (e.g. alternate hologram).
///  4. Assign Player Camera.
/// ─────────────────────────────────────────────────────────────────
/// </summary>
public class HologramMaterialToggle : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Drag the Quad GameObject here — the one whose material gets swapped")]
    public Renderer targetRenderer;

    [Header("Materials")]
    [Tooltip("Starting material (Material A)")]
    public Material materialA;
    [Tooltip("Material to swap to on first interact")]
    public Material materialB;

    [Header("Interaction")]
    public Camera playerCamera;
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("HUD")]
    public string promptText = "Press E to interact";
    public float promptYOffset = 60f;

    // ── State ─────────────────────────────────────────────────────
    private bool _onB = false;

    private bool _showPrompt = false;
    private GUIStyle _promptStyle;

    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        // Apply starting material
        if (targetRenderer != null && materialA != null)
            targetRenderer.material = materialA;
    }

    void Update()
    {
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, interactDistance)
                   && hitInfo.collider.gameObject == gameObject;

        _showPrompt = hit;

        if (hit && Input.GetKeyDown(interactKey))
            Toggle();
    }

    void Toggle()
    {
        _onB = !_onB;
        if (targetRenderer != null)
            targetRenderer.material = _onB ? materialB : materialA;
    }

    void OnGUI()
    {
        if (!_showPrompt) return;

        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 0.6f) }
            };

        const float w = 400f, h = 36f;
        float x = (Screen.width - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f + promptYOffset;

        GUI.color = Color.black;
        GUI.Label(new Rect(x + 2, y + 2, w, h), promptText, _promptStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, w, h), promptText, _promptStyle);
    }

    void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}