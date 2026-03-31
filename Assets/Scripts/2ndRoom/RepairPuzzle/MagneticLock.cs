using System.Collections;
using UnityEngine;

/// <summary>
/// MagneticLock.cs — attach to the repair box (the red toolbox).
/// Locked by default — releases when HackingPuzzle calls Unlock().
/// Once unlocked the box can be opened and the repair kit picked up.
///
/// SETUP:
/// 1. Attach to the repair box root GameObject (needs a Collider).
/// 2. Assign boxLid or boxDoor — the part that opens.
/// 3. Assign repairKit — the RepairKitItem inside.
/// 4. In HackingPuzzle Inspector → On Solved → drag this GO → MagneticLock.Unlock()
/// </summary>
public class MagneticLock : MonoBehaviour
{
    [Header("Box Opening")]
    public Transform boxLid;
    public bool      openByRotation  = true;
    public Vector3   openRotation    = new Vector3(-110f, 0f, 0f);
    public Vector3   openSlideOffset = new Vector3(0f, 0.15f, 0f);
    public float     openSpeed       = 2f;

    [Header("Repair Kit inside")]
    public GameObject repairKit;

    [Header("Lock Visual (optional — e.g. a glowing lock light)")]
    public Renderer lockLight;
    public Color    lockedColor   = Color.red;
    public Color    unlockedColor = Color.green;

    [Header("Interaction")]
    public float   interactRange = 3f;
    public KeyCode interactKey   = KeyCode.E;

    private Camera   _cam;
    private GUIStyle _promptStyle;
    private bool     _unlocked = false;
    private bool     _open     = false;
    private bool     _opening  = false;

    void Start()
    {
        _cam = Camera.main ?? FindFirstObjectByType<Camera>();

        // Hide repair kit until box opens
        if (repairKit != null)
        {
            foreach (var c in repairKit.GetComponentsInChildren<Collider>())
                c.enabled = false;
            var pickup = repairKit.GetComponent<ItemPickup>();
            if (pickup != null) pickup.enabled = false;
        }

        // Show locked light
        if (lockLight != null)
        {
            var mat = lockLight.material;
            if (mat.HasProperty("_EmissionColor"))
            {
                lockLight.material.EnableKeyword("_EMISSION");
                lockLight.material.SetColor("_EmissionColor", lockedColor * 2f);
            }
            else lockLight.material.color = lockedColor;
        }
    }

    // Called by HackingPuzzle OnSolved event
    public void Unlock()
    {
        if (_unlocked) return;
        _unlocked = true;

        // Update lock light to green
        if (lockLight != null)
        {
            var mat = lockLight.material;
            if (mat.HasProperty("_EmissionColor"))
                lockLight.material.SetColor("_EmissionColor", unlockedColor * 2f);
            else lockLight.material.color = unlockedColor;
        }
    }

    void Update()
    {
        if (!_unlocked || _open || _opening) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject &&
            !IsChildOf(hit.collider.gameObject, gameObject)) return;

        if (Input.GetKeyDown(interactKey))
            StartCoroutine(OpenBox());
    }

    IEnumerator OpenBox()
    {
        _opening = true;

        if (boxLid != null)
        {
            if (openByRotation)
            {
                Quaternion start = boxLid.localRotation;
                Quaternion end   = Quaternion.Euler(openRotation);
                float elapsed = 0f, dur = 1f / openSpeed;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    boxLid.localRotation = Quaternion.Slerp(start, end,
                        Mathf.SmoothStep(0f, 1f, elapsed / dur));
                    yield return null;
                }
                boxLid.localRotation = end;
            }
            else
            {
                Vector3 start = boxLid.localPosition;
                Vector3 end   = start + openSlideOffset;
                float elapsed = 0f, dur = 1f / openSpeed;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    boxLid.localPosition = Vector3.Lerp(start, end,
                        Mathf.SmoothStep(0f, 1f, elapsed / dur));
                    yield return null;
                }
                boxLid.localPosition = end;
            }
        }

        // Enable repair kit pickup
        if (repairKit != null)
        {
            foreach (var c in repairKit.GetComponentsInChildren<Collider>())
                c.enabled = true;
            var pickup = repairKit.GetComponent<ItemPickup>();
            if (pickup != null) pickup.enabled = true;
        }

        _open    = true;
        _opening = false;
    }

    void OnGUI()
    {
        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 22, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject &&
            !IsChildOf(hit.collider.gameObject, gameObject)) return;

        if (_open) return;

        string msg = _unlocked ? $"[{interactKey}]  Open Box" : "Magnetically Locked";
        float pw = 380f, ph = 40f;
        float px = (Screen.width  - pw) / 2f;
        float py = (Screen.height - ph) / 2f + 60f;
        GUI.color = Color.black; GUI.Label(new Rect(px+2,py+2,pw,ph), msg, _promptStyle);
        GUI.color = _unlocked ? Color.white : Color.red;
        GUI.Label(new Rect(px,py,pw,ph), msg, _promptStyle);
        GUI.color = Color.white;
    }

    static bool IsChildOf(GameObject child, GameObject parent)
    {
        Transform t = child.transform;
        while (t != null) { if (t.gameObject == parent) return true; t = t.parent; }
        return false;
    }
}
