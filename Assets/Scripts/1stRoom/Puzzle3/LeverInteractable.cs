using System.Collections;
using UnityEngine;

public class LeverInteractable : MonoBehaviour
{
    [Header("Puzzle Reference")]
    public CableBoxPuzzle puzzle;

    [Header("Lever Movement")]
    [Tooltip("Target X euler angle when pulled (e.g. 0 if lever starts at 90, or 90 if starts at 0)")]
    public float leverTargetX = 0f;
    public float leverSpeed = 2f;

    [Header("Sound Effects")]
    public AudioClip leverPullSound;

    [Header("Interaction")]
    public float interactRange = 3f;
    public KeyCode interactKey = KeyCode.E;

    private Camera _cam;
    private GUIStyle _promptStyle;
    private bool _pulled = false;
    private bool _animating = false;
    private float _originalX;

    void Start()
    {
        _cam = Camera.main ?? FindFirstObjectByType<Camera>();
        _originalX = transform.localEulerAngles.x;
    }

    public void ResetPull()
    {
        _pulled = false;
        StartCoroutine(AnimateLever(_originalX));
    }

    bool CanInteract()
    {
        if (_pulled || _animating) return false;
        if (puzzle == null) return true;
        return puzzle.CablesAllConnected();
    }

    void Update()
    {
        if (!CanInteract()) return;
        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject) return;
        if (Input.GetKeyDown(interactKey))
        {
            _pulled = true;
            StartCoroutine(PullAndValidate());
        }
    }

    IEnumerator PullAndValidate()
    {
        if (leverPullSound != null)
            AudioSource.PlayClipAtPoint(leverPullSound, transform.position);
        yield return StartCoroutine(AnimateLever(leverTargetX));
        if (puzzle != null)
            puzzle.ValidateFromLever();
    }

    IEnumerator AnimateLever(float targetX)
    {
        _animating = true;
        Quaternion startRot = transform.localRotation;
        Vector3 startE = transform.localEulerAngles;
        Quaternion endRot = Quaternion.Euler(targetX, startE.y, startE.z);
        float elapsed = 0f, dur = 1f / leverSpeed;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(startRot, endRot,
                Mathf.SmoothStep(0f, 1f, elapsed / dur));
            yield return null;
        }
        transform.localRotation = endRot;
        _animating = false;
    }

    void OnGUI()
    {
        if (!CanInteract()) return;
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
        string msg = $"[{interactKey}]  Pull Lever";
        float pw = 360f, ph = 40f;
        float px = (Screen.width - pw) / 2f;
        float py = (Screen.height - ph) / 2f + 60f;
        GUI.color = Color.black; GUI.Label(new Rect(px + 2, py + 2, pw, ph), msg, _promptStyle);
        GUI.color = Color.white; GUI.Label(new Rect(px, py, pw, ph), msg, _promptStyle);
    }
}