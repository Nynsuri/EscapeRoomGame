using UnityEngine;

/// <summary>
/// DevTeleport — Unity 6000.3.9f1
///
/// Developer tool for quickly teleporting to preset locations.
/// Only active when Developer Mode is enabled.
///
/// SETUP:
///   1. Attach to the Player GameObject (same one with PlayerController + CharacterController).
///   2. Set the 8 teleport positions in the Inspector by either:
///        a) Assigning Transform references (e.g. empty GameObjects placed in the scene), OR
///        b) Typing in world-space coordinates directly.
///   3. Toggle Developer Mode on/off in the Inspector or press F1 at runtime.
///
/// KEYS (only fire when Developer Mode is on):
///   V → Location 1    B → Location 2    N → Location 3    M → Location 4
///   F2 → Location 5   F3 → Location 6   F4 → Location 7   F5 → Location 8
///   F1 → Toggle Developer Mode on/off
/// </summary>
public class DevTeleport : MonoBehaviour
{
    [Header("Developer Mode")]
    [Tooltip("Toggle this off before shipping — disables all teleport keys")]
    public bool developerMode = true;

    [Header("Locations 1–4  (keys V B N M)")]
    [Tooltip("Optional: drag an empty Transform here and its position will be used. " +
             "If assigned, overrides the Vector3 below.")]
    public Transform location1Transform;
    public Transform location2Transform;
    public Transform location3Transform;
    public Transform location4Transform;

    [Tooltip("Used if the Transform above is not assigned")]
    public Vector3 location1Position = Vector3.zero;
    public Vector3 location2Position = Vector3.zero;
    public Vector3 location3Position = Vector3.zero;
    public Vector3 location4Position = Vector3.zero;

    [Header("Locations 5–8  (keys F2 F3 F4 F5)")]
    public Transform location5Transform;
    public Transform location6Transform;
    public Transform location7Transform;
    public Transform location8Transform;

    public Vector3 location5Position = Vector3.zero;
    public Vector3 location6Position = Vector3.zero;
    public Vector3 location7Position = Vector3.zero;
    public Vector3 location8Position = Vector3.zero;

    [Header("Location Names (shown in log + on screen)")]
    public string location1Name = "Location 1";
    public string location2Name = "Location 2";
    public string location3Name = "Location 3";
    public string location4Name = "Location 4";
    public string location5Name = "Location 5";
    public string location6Name = "Location 6";
    public string location7Name = "Location 7";
    public string location8Name = "Location 8";

    [Header("Keys — Locations 1–4")]
    public KeyCode key1 = KeyCode.V;
    public KeyCode key2 = KeyCode.B;
    public KeyCode key3 = KeyCode.N;
    public KeyCode key4 = KeyCode.M;

    [Header("Keys — Locations 5–8")]
    public KeyCode key5 = KeyCode.F2;
    public KeyCode key6 = KeyCode.F3;
    public KeyCode key7 = KeyCode.F4;
    public KeyCode key8 = KeyCode.F5;

    [Header("Toggle Key")]
    public KeyCode toggleKey = KeyCode.F1;

    [Header("On-Screen Notification")]
    public float notificationDuration = 2.5f;

    // ── Private ───────────────────────────────────────────────────
    private CharacterController _cc;
    private PlayerController _pc;
    private string _notification = "";
    private float _notificationTimer = 0f;
    private GUIStyle _style;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _pc = GetComponent<PlayerController>();
    }

    private void Update()
    {
        // Toggle developer mode
        if (Input.GetKeyDown(toggleKey))
        {
            developerMode = !developerMode;
            ShowNotification(developerMode ? "[DEV] Developer mode ON" : "[DEV] Developer mode OFF");
        }

        if (!developerMode) return;

        if (Input.GetKeyDown(key1)) TeleportTo(GetPosition(location1Transform, location1Position), location1Name);
        if (Input.GetKeyDown(key2)) TeleportTo(GetPosition(location2Transform, location2Position), location2Name);
        if (Input.GetKeyDown(key3)) TeleportTo(GetPosition(location3Transform, location3Position), location3Name);
        if (Input.GetKeyDown(key4)) TeleportTo(GetPosition(location4Transform, location4Position), location4Name);
        if (Input.GetKeyDown(key5)) TeleportTo(GetPosition(location5Transform, location5Position), location5Name);
        if (Input.GetKeyDown(key6)) TeleportTo(GetPosition(location6Transform, location6Position), location6Name);
        if (Input.GetKeyDown(key7)) TeleportTo(GetPosition(location7Transform, location7Position), location7Name);
        if (Input.GetKeyDown(key8)) TeleportTo(GetPosition(location8Transform, location8Position), location8Name);

        if (_notificationTimer > 0f)
            _notificationTimer -= Time.deltaTime;
    }

    // ── Teleport ──────────────────────────────────────────────────

    private void TeleportTo(Vector3 position, string locationName)
    {
        Debug.Log($"[DevTeleport] TeleportTo called. PC={_pc != null}, CC={_cc != null}, pos={position}");

        if (_pc != null)
        {
            _pc.TeleportTo(position);
        }
        else
        {
            // Fallback if no PlayerController found
            if (_cc != null) _cc.enabled = false;
            transform.position = position;
            Physics.SyncTransforms();
            if (_cc != null) _cc.enabled = true;
        }

        ShowNotification($"[DEV] Teleported to {locationName}  {position}");
        Debug.Log($"[DevTeleport] → {locationName}  {position}");
    }

    private Vector3 GetPosition(Transform t, Vector3 fallback)
        => t != null ? t.position : fallback;

    // ── Notification ──────────────────────────────────────────────

    private void ShowNotification(string msg)
    {
        _notification = msg;
        _notificationTimer = notificationDuration;
    }

    private void OnGUI()
    {
        if (_notificationTimer <= 0f) return;

        InitStyle();

        float alpha = Mathf.Clamp01(_notificationTimer);
        GUI.color = new Color(1f, 1f, 1f, alpha);

        const float w = 600f, h = 36f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.15f;

        GUI.color = new Color(0f, 0f, 0f, alpha * 0.7f);
        GUI.Label(new Rect(x + 2, y + 2, w, h), _notification, _style);
        GUI.color = new Color(1f, 0.85f, 0.2f, alpha);
        GUI.Label(new Rect(x, y, w, h), _notification, _style);

        GUI.color = Color.white;

        // Dev mode indicator in corner
        if (developerMode)
        {
            InitStyle();
            GUI.color = new Color(1f, 0.4f, 0.4f, 0.8f);
            GUI.Label(new Rect(10f, 10f, 200f, 24f), "[DEV MODE]", _style);
            GUI.color = Color.white;
        }
    }

    private void InitStyle()
    {
        if (_style != null) return;
        _style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
    }

    // ── Editor helper — draw gizmos at each location ──────────────

    private void OnDrawGizmos()
    {
        if (!developerMode) return;

        DrawLocationGizmo(GetPosition(location1Transform, location1Position), location1Name, new Color(1f, 0.3f, 0.3f));
        DrawLocationGizmo(GetPosition(location2Transform, location2Position), location2Name, new Color(0.3f, 1f, 0.3f));
        DrawLocationGizmo(GetPosition(location3Transform, location3Position), location3Name, new Color(0.3f, 0.6f, 1f));
        DrawLocationGizmo(GetPosition(location4Transform, location4Position), location4Name, new Color(1f, 0.8f, 0.2f));
        DrawLocationGizmo(GetPosition(location5Transform, location5Position), location5Name, new Color(1f, 0.4f, 1f));
        DrawLocationGizmo(GetPosition(location6Transform, location6Position), location6Name, new Color(0.3f, 1f, 1f));
        DrawLocationGizmo(GetPosition(location7Transform, location7Position), location7Name, new Color(1f, 0.6f, 0.2f));
        DrawLocationGizmo(GetPosition(location8Transform, location8Position), location8Name, new Color(0.7f, 0.7f, 1f));
    }

    private void DrawLocationGizmo(Vector3 pos, string name, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(pos, 0.4f);
        Gizmos.DrawLine(pos, pos + Vector3.up * 2f);
#if UNITY_EDITOR
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(pos + Vector3.up * 2.2f, name);
#endif
    }
}