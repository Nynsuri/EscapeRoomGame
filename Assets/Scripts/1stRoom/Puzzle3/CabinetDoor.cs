using System.Collections;
using UnityEngine;

/// <summary>
/// CabinetDoor.cs — Unity 6000.3.9f1
/// Attach to each cabinet door. Some are locked (stuck), some open freely.
/// When opened, reveals the cable piece inside.
///
/// SETUP:
/// 1. Attach to the door GameObject (needs a Collider).
/// 2. Set isLocked = true for doors that can't be opened.
/// 3. Assign cablePieceInside — the CablePieceItem world object inside.
/// 4. Set openRotation to the door's open euler angles.
/// </summary>
public class CabinetDoor : MonoBehaviour
{
    [Header("Door Settings")]
    public bool isLocked = false;
    [Tooltip("Local euler angles when door is fully open")]
    public Vector3 openRotation = new Vector3(0f, -110f, 0f);
    public float openSpeed = 2f;

    [Header("Cable Inside (assign the CablePieceItem world object)")]
    public GameObject cablePieceInside;

    [Header("Interaction")]
    public float interactRange = 3f;
    public KeyCode interactKey = KeyCode.E;

    private Camera _cam;
    private GUIStyle _promptStyle;
    private bool _open = false;
    private bool _opening = false;

    void Start()
    {
        _cam = Camera.main ?? FindFirstObjectByType<Camera>();

        // Cable inside hidden and uninteractable until door opens
        if (cablePieceInside != null)
        {
            foreach (var c in cablePieceInside.GetComponentsInChildren<Collider>())
                c.enabled = false;
            var pickup = cablePieceInside.GetComponent<ItemPickup>();
            if (pickup != null) pickup.enabled = false;
        }
    }

    void Update()
    {
        if (_open || _opening) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject) return;

        if (Input.GetKeyDown(interactKey))
        {
            if (isLocked)
            {
                ShowMessage("Stuck.");
                return;
            }
            StartCoroutine(OpenDoor());
        }
    }

    IEnumerator OpenDoor()
    {
        _opening = true;

        // Nudge player out of the door's swing arc before it moves
        NudgePlayerClear();

        Quaternion startRot = transform.localRotation;
        Quaternion endRot = Quaternion.Euler(openRotation);
        float elapsed = 0f, dur = 1f / openSpeed;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(startRot, endRot,
                Mathf.SmoothStep(0f, 1f, elapsed / dur));
            yield return null;
        }
        transform.localRotation = endRot;

        // Reveal cable
        if (cablePieceInside != null)
        {
            foreach (var c in cablePieceInside.GetComponentsInChildren<Collider>())
                c.enabled = true;
            var pickup = cablePieceInside.GetComponent<ItemPickup>();
            if (pickup != null) pickup.enabled = true;
        }

        _open = true;
        _opening = false;
    }

    // ── Door clearance ────────────────────────────────────────────
    /// <summary>
    /// If the player is within the door's swing radius, push them back
    /// along the door's local Z axis (away from the cabinet) before opening.
    /// This prevents getting trapped between two opening doors.
    /// </summary>
    void NudgePlayerClear()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc == null) return;

        // Use the door's hinge point (this transform) as the pivot
        // Door swings on local Y — the danger zone is within swingRadius units
        float swingRadius = 1.2f;   // adjust to match your door width
        float nudgeDistance = 1.5f; // how far to push the player out

        Vector3 toPlayer = pc.transform.position - transform.position;
        float dist = new Vector2(toPlayer.x, toPlayer.z).magnitude; // horizontal only

        if (dist < swingRadius)
        {
            // Push player away from the cabinet face (door's forward = into room)
            Vector3 pushDir = transform.forward;
            // Make sure we push outward, not into the cabinet
            if (Vector3.Dot(pushDir, toPlayer) < 0) pushDir = -pushDir;
            pushDir.y = 0f;
            pushDir.Normalize();

            var cc = pc.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                pc.transform.position += pushDir * (nudgeDistance - dist + 0.1f);
                cc.enabled = true;
            }
        }
    }

    // ── Prompts ───────────────────────────────────────────────────
    private string _message = "";
    private float _messageTimer = 0f;

    void ShowMessage(string msg) { _message = msg; _messageTimer = 1.5f; }

    void OnGUI()
    {
        if (_open || _opening) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject) return;

        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

        // Show stuck message or open prompt
        string msg;
        if (_messageTimer > 0f)
        {
            _messageTimer -= Time.deltaTime;
            msg = _message;
        }
        else
        {
            msg = isLocked ? "Stuck" : $"[{interactKey}]  Open";
        }

        float pw = 360f, ph = 40f;
        float px = (Screen.width - pw) / 2f;
        float py = (Screen.height - ph) / 2f + 60f;
        GUI.color = Color.black; GUI.Label(new Rect(px + 2, py + 2, pw, ph), msg, _promptStyle);
        GUI.color = isLocked ? Color.red : Color.white;
        GUI.Label(new Rect(px, py, pw, ph), msg, _promptStyle);
        GUI.color = Color.white;
    }
}