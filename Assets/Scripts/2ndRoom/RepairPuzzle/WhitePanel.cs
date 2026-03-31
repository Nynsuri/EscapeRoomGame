using System.Collections;
using UnityEngine;

/// <summary>
/// WhitePanel.cs — Unity 6000.3.9f1
/// The white panel box on the wall. Opens on interact, reveals the circuit board inside.
/// SETUP:
/// 1. Attach to the panel frame GameObject (needs a Collider).
/// 2. Assign panelDoor (the white cover that swings/slides open).
/// 3. Assign circuitBoard — the CircuitRepairPuzzle GameObject inside.
/// 4. Set openRotation or openSlideOffset depending on how it opens.
/// </summary>
public class WhitePanel : MonoBehaviour
{
    [Header("Panel Door")]
    public Transform panelDoor;
    public bool      openByRotation  = true;
    public Vector3   openRotation    = new Vector3(0f, -110f, 0f);
    public Vector3   openSlideOffset = new Vector3(0f, 0f, -0.1f);
    public float     openSpeed       = 2f;

    [Header("Circuit Board inside")]
    [Tooltip("The CircuitRepairPuzzle GameObject — hidden until panel opens")]
    public GameObject circuitBoard;

    [Header("Interaction")]
    public float   interactRange = 3f;
    public KeyCode interactKey   = KeyCode.E;

    private Camera   _cam;
    private GUIStyle _promptStyle;
    private bool     _open    = false;
    private bool     _opening = false;

    void Start()
    {
        _cam = Camera.main ?? FindFirstObjectByType<Camera>();

        // Hide circuit board collider until panel opens
        if (circuitBoard != null)
        {
            foreach (var c in circuitBoard.GetComponentsInChildren<Collider>())
                c.enabled = false;
        }
    }

    void Update()
    {
        if (_open || _opening) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject &&
            !IsChildOf(hit.collider.gameObject, gameObject)) return;

        if (Input.GetKeyDown(interactKey))
            StartCoroutine(OpenPanel());
    }

    IEnumerator OpenPanel()
    {
        _opening = true;

        if (panelDoor != null)
        {
            if (openByRotation)
            {
                Quaternion start = panelDoor.localRotation;
                Quaternion end   = Quaternion.Euler(openRotation);
                float elapsed = 0f, dur = 1f / openSpeed;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    panelDoor.localRotation = Quaternion.Slerp(start, end,
                        Mathf.SmoothStep(0f, 1f, elapsed / dur));
                    yield return null;
                }
                panelDoor.localRotation = end;
            }
            else
            {
                Vector3 start = panelDoor.localPosition;
                Vector3 end   = start + openSlideOffset;
                float elapsed = 0f, dur = 1f / openSpeed;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    panelDoor.localPosition = Vector3.Lerp(start, end,
                        Mathf.SmoothStep(0f, 1f, elapsed / dur));
                    yield return null;
                }
                panelDoor.localPosition = end;
            }
        }

        // Enable circuit board interaction
        if (circuitBoard != null)
        {
            foreach (var c in circuitBoard.GetComponentsInChildren<Collider>())
                c.enabled = true;
        }

        _open    = true;
        _opening = false;
    }

    void OnGUI()
    {
        if (_open || _opening) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject &&
            !IsChildOf(hit.collider.gameObject, gameObject)) return;

        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 22, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };

        string msg = $"[{interactKey}]  Open Panel";
        float pw = 380f, ph = 40f;
        float px = (Screen.width  - pw) / 2f;
        float py = (Screen.height - ph) / 2f + 60f;
        GUI.color = Color.black; GUI.Label(new Rect(px+2,py+2,pw,ph), msg, _promptStyle);
        GUI.color = Color.white; GUI.Label(new Rect(px,py,pw,ph),     msg, _promptStyle);
    }

    static bool IsChildOf(GameObject child, GameObject parent)
    {
        Transform t = child.transform;
        while (t != null) { if (t.gameObject == parent) return true; t = t.parent; }
        return false;
    }
}
