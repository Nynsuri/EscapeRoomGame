using System.Collections;
using UnityEngine;

/// <summary>
/// ItemPedestal — Unity 6000.3.9f1
///
/// Attach to each pedestal GameObject in the scene.
///
/// ─── Setup ──────────────────────────────────────────────────────
///  1. Set "Required ID" to match the CollectibleItem in the same room.
///  2. Create an empty child GameObject on top of the pedestal (the display
///     point), and assign it to "Item Mount Point".
///  3. Make sure the pedestal has a Collider (for the raycast to hit).
///  4. This script calls CollectibleManager.RegisterPlaced() — make sure
///     CollectibleManager is in the scene.
///
/// ─── Player interaction ──────────────────────────────────────────
///  Your existing raycast/interaction script should call:
///      pedestal.TryPlace(collectibleManager)
///  — OR — this script can self-poll if you enable "Self Poll Raycast"
///  and assign the Player Camera below.
/// ────────────────────────────────────────────────────────────────
/// </summary>
public class ItemPedestal : MonoBehaviour
{
    // ── Identity ─────────────────────────────────────────────────
    [Header("Identity")]
    [Tooltip("The CollectibleID this pedestal accepts. Must match the item's ID.")]
    public CollectibleID requiredID = CollectibleID.None;

    [Tooltip("Friendly name shown in on-screen messages")]
    public string pedestalName = "Relic Pedestal";

    // ── Mount ────────────────────────────────────────────────────
    [Header("Item Mount")]
    [Tooltip("Empty child Transform where the item will be visually placed")]
    public Transform itemMountPoint;

    // ── Raycast (self-poll mode) ──────────────────────────────────
    [Header("Raycast — Self Poll Mode")]
    [Tooltip("Enable if you want this script to handle its own E-key / click detection.\n" +
             "Disable if your existing interaction script calls TryPlace() directly.")]
    public bool selfPollRaycast = true;

    [Tooltip("The player's camera used for the raycast (assign Main Camera)")]
    public Camera playerCamera;

    [Tooltip("Max distance the player can interact from")]
    public float interactDistance = 3f;

    [Tooltip("Key to press when looking at the pedestal")]
    public KeyCode interactKey = KeyCode.E;

    // ── Audio (optional) ─────────────────────────────────────────
    [Header("Audio (optional)")]
    public AudioClip placeSound;
    public AudioClip rejectSound;

    [Header("Audio Mixer")]
    public UnityEngine.Audio.AudioMixerGroup audioMixerGroup;

    // ── HUD message ──────────────────────────────────────────────
    [Header("On-Screen Messages")]
    [Tooltip("Duration the message stays on screen (seconds)")]
    public float messageDuration = 2.5f;

    // ── State ────────────────────────────────────────────────────
    private bool _filled = false;
    public bool IsFilled => _filled;

    // GUI state
    private string _hudMessage      = "";
    private float  _hudMessageTimer = 0f;
    private bool   _showPrompt      = false;   // "Press E to place"
    private GUIStyle _messageStyle;
    private GUIStyle _promptStyle;
    private AudioSource _audio;

    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        AudioHelper.Configure(_audio, audioMixerGroup);

        if (itemMountPoint == null)
            Debug.LogWarning($"[ItemPedestal] '{gameObject.name}' has no Item Mount Point assigned!");

        if (requiredID == CollectibleID.None)
            Debug.LogWarning($"[ItemPedestal] '{gameObject.name}' has no Required ID set!");
    }

    void Update()
    {
        if (_hudMessageTimer > 0f) _hudMessageTimer -= Time.deltaTime;

        if (!selfPollRaycast || _filled) { _showPrompt = false; return; }
        if (playerCamera == null)        { _showPrompt = false; return; }

        // ── Raycast ───────────────────────────────────────────────
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, interactDistance)
                   && hitInfo.collider.gameObject == gameObject;

        _showPrompt = hit && !_filled;

        if (hit && Input.GetKeyDown(interactKey))
            TryPlace();
    }

    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempt to place the matching collectible on this pedestal.
    /// Call this from an external interaction script if selfPollRaycast = false.
    /// </summary>
    public void TryPlace()
    {
        if (_filled)
        {
            ShowMessage("Already placed.", isReject: true);
            return;
        }

        if (CollectibleManager.Instance == null)
        {
            Debug.LogWarning("[ItemPedestal] No CollectibleManager in scene!");
            return;
        }

        // Ask the manager for the collected item with our required ID
        CollectibleItem item = CollectibleManager.Instance.GetCollectedItem(requiredID);

        if (item == null)
        {
            // Player hasn't collected the matching item yet
            string needed = CollectibleManager.Instance.GetCollectibleName(requiredID);
            ShowMessage(
                string.IsNullOrEmpty(needed)
                    ? "You don't have the required relic."
                    : $"Missing: {needed}",
                isReject: true);

            PlaySound(rejectSound);
            return;
        }

        // ── Place it ──────────────────────────────────────────────
        _filled = true;
        _showPrompt = false;

        if (itemMountPoint != null)
            item.PlaceOnPedestal(itemMountPoint);

        PlaySound(placeSound);
        ShowMessage($"{item.collectibleName} placed!", isReject: false);

        CollectibleManager.Instance.RegisterPlaced(requiredID);

        Debug.Log($"[ItemPedestal] '{pedestalName}' filled with ID: {requiredID}");
    }

    // ── GUI ───────────────────────────────────────────────────────

    void OnGUI()
    {
        InitStyles();

        // Interaction prompt
        if (_showPrompt)
        {
            string prompt = $"[{interactKey}] Place relic on {pedestalName}";
            DrawCentredLabel(prompt, _promptStyle, yOffset: 60f);
        }

        // Feedback message
        if (_hudMessageTimer > 0f && !string.IsNullOrEmpty(_hudMessage))
            DrawCentredLabel(_hudMessage, _messageStyle, yOffset: 0f);
    }

    void DrawCentredLabel(string text, GUIStyle style, float yOffset)
    {
        const float w = 500f, h = 40f;
        float x = (Screen.width  - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f + yOffset;

        // Drop shadow
        GUI.color = Color.black;
        GUI.Label(new Rect(x + 2, y + 2, w, h), text, style);
        GUI.color = Color.white;
        GUI.Label(new Rect(x,     y,     w, h), text, style);
    }

    void ShowMessage(string msg, bool isReject)
    {
        _hudMessage      = msg;
        _hudMessageTimer = messageDuration;
        if (_messageStyle != null)
            _messageStyle.normal.textColor = isReject ? Color.red : Color.green;
    }

    void InitStyles()
    {
        if (_messageStyle == null)
            _messageStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize    = 22,
                fontStyle   = FontStyle.Bold,
                alignment   = TextAnchor.MiddleCenter,
                normal      = { textColor = Color.white }
            };

        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize    = 18,
                fontStyle   = FontStyle.Normal,
                alignment   = TextAnchor.MiddleCenter,
                normal      = { textColor = new Color(1f, 1f, 0.6f) }
            };
    }

    // ── Audio helper ──────────────────────────────────────────────

    void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (_audio != null) _audio.PlayOneShot(clip);
        else AudioHelper.Play(clip, transform.position, audioMixerGroup);
    }

    // ── Gizmo ────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (itemMountPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(itemMountPoint.position, 0.1f);
        Gizmos.DrawLine(transform.position, itemMountPoint.position);
    }
}
