using System.Collections;
using UnityEngine;

/// <summary>
/// KeyDoor — Unity 6000.3.9f1
///
/// Attach to the door GameObject (the one that should rotate open).
/// Make sure the door's pivot/origin is at the HINGE EDGE, not the center —
/// that is what controls the rotation point. If your model pivots from the
/// center, wrap it in an empty parent GameObject positioned at the hinge and
/// attach this script to the parent instead.
///
/// ─── Setup ───────────────────────────────────────────────────────
///  1. Set "Required Key ID" to match the keyID on your KeyItem.
///  2. Set "Open Angle" — how many degrees the door swings (e.g. 90 or -90
///     depending on which side it opens).
///  3. Assign "Player Camera" (Main Camera) for the raycast.
///  4. Make sure the door has a Collider so the raycast can hit it.
///
/// ─── Interaction ─────────────────────────────────────────────────
///  Player looks at the door and presses the interact key (default E).
///  • Has the right key  → door swings open, key is consumed.
///  • Has no / wrong key → on-screen message shown.
///  • Door already open  → no interaction.
/// ─────────────────────────────────────────────────────────────────
/// </summary>
public class KeyDoor : MonoBehaviour
{
    // ── Identity ─────────────────────────────────────────────────
    [Header("Lock")]
    [Tooltip("Must match the keyID field on the KeyItem that opens this door")]
    public string requiredKeyID = "room_key";

    [Tooltip("Consume (remove) the key from inventory after use")]
    public bool consumeKey = true;

    // ── Interaction ──────────────────────────────────────────────
    [Header("Interaction")]
    public Camera playerCamera;
    public float  interactDistance = 3f;
    public KeyCode interactKey     = KeyCode.E;

    // ── Door swing ───────────────────────────────────────────────
    [Header("Door Swing")]
    [Tooltip("Degrees to rotate around the Y axis when opening.\n" +
             "Use negative value if the door should swing the other way.")]
    public float openAngle    = 90f;

    [Tooltip("Seconds the swing animation takes")]
    public float openDuration = 0.8f;

    // ── Audio ────────────────────────────────────────────────────
    [Header("Audio (optional)")]
    public AudioClip unlockSound;
    public AudioClip lockedSound;

    [Header("Audio Mixer")]
    public UnityEngine.Audio.AudioMixerGroup audioMixerGroup;

    // ── HUD ──────────────────────────────────────────────────────
    [Header("Messages")]
    public string noKeyMessage      = "This door is locked.";
    public string wrongKeyMessage   = "The key doesn't fit.";
    public string alreadyOpenMsg    = "";          // leave empty to stay silent
    public float  messageDuration   = 2.5f;

    // ── Internals ────────────────────────────────────────────────
    private bool   _isOpen    = false;
    private bool   _animating = false;
    private bool   _showPrompt = false;

    private string _hudMessage      = "";
    private float  _hudMessageTimer = 0f;

    private GUIStyle _msgStyle;
    private GUIStyle _promptStyle;
    private AudioSource _audio;

    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        AudioHelper.Configure(_audio, audioMixerGroup);

        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    void Update()
    {
        if (_hudMessageTimer > 0f) _hudMessageTimer -= Time.deltaTime;
        if (_isOpen || _animating) { _showPrompt = false; return; }
        if (playerCamera == null)  { _showPrompt = false; return; }

        // ── Raycast ───────────────────────────────────────────────
        Ray ray = new Ray(playerCamera.transform.position,
                          playerCamera.transform.forward);

        bool looking = Physics.Raycast(ray, out RaycastHit hit, interactDistance)
                       && hit.collider.gameObject == gameObject;

        _showPrompt = looking;

        if (looking && Input.GetKeyDown(interactKey))
            TryOpen();
    }

    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempt to open the door. Can also be called from an external
    /// interaction manager instead of using the built-in raycast.
    /// </summary>
    public void TryOpen()
    {
        if (_isOpen || _animating) return;

        // ── Find player inventory ─────────────────────────────────
        Inventory inventory = FindPlayerInventory();
        if (inventory == null)
        {
            Debug.LogWarning("[KeyDoor] No Inventory found on Player tag!");
            return;
        }

        // ── Search inventory for a matching KeyItem ───────────────
        KeyItem matchingKey = null;
        foreach (var invItem in inventory.Items)
        {
            if (invItem is KeyItem k && k.keyID == requiredKeyID)
            {
                matchingKey = k;
                break;
            }
        }

        if (matchingKey == null)
        {
            // Check whether they have ANY key to give a better message
            bool hasAnyKey = false;
            foreach (var invItem in inventory.Items)
                if (invItem is KeyItem) { hasAnyKey = true; break; }

            string msg = hasAnyKey ? wrongKeyMessage : noKeyMessage;
            ShowMessage(msg, Color.red);
            PlaySound(lockedSound);
            return;
        }

        // ── Unlock! ───────────────────────────────────────────────
        PlaySound(unlockSound);
        _showPrompt = false;

        if (consumeKey)
            inventory.RemoveItem(matchingKey);

        StartCoroutine(SwingOpen());
    }

    // ── Animation ────────────────────────────────────────────────

    IEnumerator SwingOpen()
    {
        _animating = true;

        Quaternion startRot = transform.localRotation;
        Quaternion endRot   = startRot * Quaternion.Euler(0f, openAngle, 0f);

        float elapsed = 0f;
        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / openDuration);
            transform.localRotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        transform.localRotation = endRot;
        _isOpen    = true;
        _animating = false;
    }

    // ── Helpers ──────────────────────────────────────────────────

    Inventory FindPlayerInventory()
    {
        // Tries the object tagged "Player" first, then falls back to any in scene
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            var inv = player.GetComponent<Inventory>();
            if (inv != null) return inv;
        }
        return FindFirstObjectByType<Inventory>();
    }

    void ShowMessage(string msg, Color color)
    {
        _hudMessage      = msg;
        _hudMessageTimer = messageDuration;
        if (_msgStyle != null) _msgStyle.normal.textColor = color;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (_audio != null) _audio.PlayOneShot(clip);
        else AudioHelper.Play(clip, transform.position, audioMixerGroup);
    }

    // ── GUI ──────────────────────────────────────────────────────

    void OnGUI()
    {
        InitStyles();

        if (_showPrompt && !_isOpen)
        {
            string prompt = $"[{interactKey}] Unlock door";
            DrawCentred(prompt, _promptStyle, yOffset: 60f);
        }

        if (_hudMessageTimer > 0f && !string.IsNullOrEmpty(_hudMessage))
            DrawCentred(_hudMessage, _msgStyle, yOffset: 0f);
    }

    void DrawCentred(string text, GUIStyle style, float yOffset)
    {
        const float w = 480f, h = 40f;
        float x = (Screen.width  - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f + yOffset;

        GUI.color = Color.black;
        GUI.Label(new Rect(x + 2, y + 2, w, h), text, style);
        GUI.color = Color.white;
        GUI.Label(new Rect(x,     y,     w, h), text, style);
    }

    void InitStyles()
    {
        if (_msgStyle == null)
            _msgStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.red }
            };

        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 18,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(1f, 1f, 0.6f) }
            };
    }

    // ── Gizmo ────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        // Visualise where the door will swing to
        Gizmos.color = Color.green;
        Vector3 hinge = transform.position;
        Vector3 face  = transform.forward;
        Gizmos.DrawRay(hinge, face * 0.6f);

        Quaternion swung = transform.rotation * Quaternion.Euler(0f, openAngle, 0f);
        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
        Gizmos.DrawRay(hinge, swung * Vector3.forward * 0.6f);
    }
}
