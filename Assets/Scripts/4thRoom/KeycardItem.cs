using UnityEngine;

/// <summary>
/// KeycardItem — InventoryItem subclass for the keycard.
/// When selected and used (left-click), it unlocks any GameObject
/// tagged "KeycardLock" that the player is looking at.
///
/// Setup:
///   1. Create a GameObject with a keycard mesh/sprite.
///   2. Attach this script + ItemPickup.
///   3. Set itemName, description, and icon in Inspector.
///   4. Start the GameObject as DISABLED (SetActive false) if inside a DissolvableBox.
///   5. Tag doors/locks you want to open as "KeycardLock".
/// </summary>
public class KeycardItem : InventoryItem
{
    [Header("Keycard Settings")]
    [Tooltip("How far the player can be to use the keycard")]
    public float useRange = 4f;

    [Tooltip("Key to use the keycard (when selected)")]
    public KeyCode useKey = KeyCode.Mouse0;

    [Tooltip("Tag on objects that accept this keycard")]
    public string lockTag = "KeycardLock";

    [Tooltip("If true, keycard is consumed after use")]
    public bool consumeOnUse = true;

    private bool _isSelected = false;
    private Camera _cam;
    private GUIStyle _promptStyle;

    // Feedback message
    private string _message = "";
    private float _messageTimer = 0f;
    private Color _messageColor = Color.white;

    void Awake()
    {
        if (string.IsNullOrEmpty(itemName))    itemName = "Security Keycard";
        if (string.IsNullOrEmpty(description)) description = "A high-clearance keycard. Use on locked doors to gain access.";
    }

    public override void OnSelect()
    {
        base.OnSelect();
        _isSelected = true;
        _cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
    }

    public override void OnDeselect()
    {
        base.OnDeselect();
        _isSelected = false;
    }

    public override void OnInventoryUpdate()
    {
        if (_messageTimer > 0f) _messageTimer -= Time.deltaTime;

        if (!_isSelected || _cam == null) return;

        if (Input.GetKeyDown(useKey))
        {
            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, useRange))
            {
                if (hit.collider.CompareTag(lockTag))
                {
                    UseKeycard(hit.collider.gameObject);
                }
                else
                {
                    ShowMessage("CAN'T USE KEYCARD HERE", new Color(1f, 0.4f, 0.3f));
                }
            }
        }
    }

    private void UseKeycard(GameObject lockObject)
    {
        Debug.Log($"[KeycardItem] Unlocking: {lockObject.name}");
        ShowMessage("ACCESS GRANTED", new Color(0.2f, 1f, 0.5f));

        // Try to call OnUnlock on the lock object (if it has a compatible script)
        lockObject.SendMessage("OnKeycardUsed", SendMessageOptions.DontRequireReceiver);

        // Trigger any UnityEvents on the lock
        var handler = lockObject.GetComponent<KeycardLockHandler>();
        if (handler != null)
            handler.Unlock();

        if (consumeOnUse)
        {
            var inv = Object.FindFirstObjectByType<Inventory>();
            if (inv != null)
                inv.RemoveItem(this);
        }
    }

    private void ShowMessage(string msg, Color col)
    {
        _message = msg;
        _messageColor = col;
        _messageTimer = 2f;
    }

    void OnGUI()
    {
        if (!_isSelected || _cam == null) return;

        // Show use prompt when aiming at a lock
        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, useRange) &&
            hit.collider.CompareTag(lockTag))
        {
            DrawCenteredText("[LMB] USE KEYCARD", new Color(0.3f, 0.85f, 1f), 60f);
        }

        // Show feedback message
        if (_messageTimer > 0f && !string.IsNullOrEmpty(_message))
        {
            Color c = _messageColor;
            c.a = Mathf.Min(1f, _messageTimer);
            DrawCenteredText(_message, c, -40f);
        }
    }

    private void DrawCenteredText(string msg, Color col, float yOffset)
    {
        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };

        float pw = 400f, ph = 40f;
        float px = (Screen.width - pw) / 2f;
        float py = (Screen.height - ph) / 2f + yOffset;

        _promptStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(px + 2, py + 2, pw, ph), msg, _promptStyle);
        _promptStyle.normal.textColor = col;
        GUI.Label(new Rect(px, py, pw, ph), msg, _promptStyle);
    }
}
