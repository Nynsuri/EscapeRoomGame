using System.Collections;
using UnityEngine;

/// <summary>
/// ItemPickup.cs — attach to a world item alongside an InventoryItem subclass
/// OR a CollectibleItem. Handles both cases automatically.
/// Optionally assign a drawer that closes when this item is picked up.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [Header("Drawer (optional — closes when item is picked up)")]
    public Transform drawerToClose;
    public Vector3 drawerCloseDirection = new Vector3(-1f, 0f, 0f);
    public float drawerCloseDistance = 0.3f;
    public float drawerCloseSpeed = 1.2f;

    [Header("Sound Effects")]
    public AudioClip pickupSound;
    public AudioClip drawerCloseSound;

    [Header("Audio Mixer")]
    public UnityEngine.Audio.AudioMixerGroup audioMixerGroup;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onPickup;

    [Header("Magnetic Lock (optional — blocks pickup until unlocked)")]
    public MagneticLock magneticLock;

    public bool IsLocked => magneticLock != null && !magneticLock.IsUnlocked;

    private InventoryItem _item;
    private CollectibleItem _collectible;

    void Awake()
    {
        _item = GetComponent<InventoryItem>();
        _collectible = GetComponent<CollectibleItem>();

        if (_item == null && _collectible == null)
            Debug.LogWarning($"[ItemPickup] No InventoryItem or CollectibleItem on {gameObject.name}!");
    }

    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by your interaction / raycast script.
    /// Handles both regular inventory items and special collectibles.
    /// </summary>
    public void TryPickup(Inventory inventory)
    {
        if (IsLocked)
        {
            _lockedMessage = "Magnetic Lock Disengaged";
            _lockedMessageTimer = 2f;
            return;
        }

        // ── Collectible path (bypasses inventory) ─────────────────
        if (_collectible != null)
        {
            bool collected = _collectible.TryCollect();
            if (!collected) return;         // already collected or no manager

            if (pickupSound != null) AudioHelper.Play(pickupSound, transform.position, audioMixerGroup);
            onPickup?.Invoke();
            if (drawerToClose != null) StartCoroutine(CloseDrawer());
            return;
        }

        // ── Normal inventory item path ────────────────────────────
        if (_item == null || inventory == null) return;

        bool added = inventory.AddItem(_item);
        if (!added) return;

        if (pickupSound != null) AudioHelper.Play(pickupSound, transform.position, audioMixerGroup);
        onPickup?.Invoke();
        if (drawerToClose != null) StartCoroutine(CloseDrawer());
    }

    // ─────────────────────────────────────────────────────────────

    private string _lockedMessage = "";
    private float _lockedMessageTimer = 0f;
    private GUIStyle _lockedStyle;

    void Update()
    {
        if (_lockedMessageTimer > 0f) _lockedMessageTimer -= Time.deltaTime;
    }

    void OnGUI()
    {
        if (_lockedMessageTimer <= 0f || string.IsNullOrEmpty(_lockedMessage)) return;
        if (_lockedStyle == null)
            _lockedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.red }
            };
        float pw = 400f, ph = 40f;
        float px = (Screen.width - pw) / 2f;
        float py = (Screen.height - ph) / 2f - 40f;
        GUI.color = Color.black;
        GUI.Label(new Rect(px + 2, py + 2, pw, ph), _lockedMessage, _lockedStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(px, py, pw, ph), _lockedMessage, _lockedStyle);
    }

    IEnumerator CloseDrawer()
    {
        if (drawerCloseSound != null)
            AudioHelper.Play(drawerCloseSound, drawerToClose.position, audioMixerGroup);

        Vector3 start = drawerToClose.localPosition;
        Vector3 end = start + drawerCloseDirection.normalized * drawerCloseDistance;
        float elapsed = 0f, dur = drawerCloseDistance / drawerCloseSpeed;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            drawerToClose.localPosition = Vector3.Lerp(start, end,
                Mathf.SmoothStep(0f, 1f, elapsed / dur));
            yield return null;
        }
        drawerToClose.localPosition = end;
    }
}