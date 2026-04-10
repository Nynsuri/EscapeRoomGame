using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ReadableDocument — Unity 6000.3.9f1
///
/// Attach to the PARENT of two quad children (FrontQuad, BackQuad).
/// Parent needs a Collider for interaction detection.
///
/// Uses the same camera zoom/detach pattern as WireTracePuzzle:
///  - Camera moves to puzzleCameraPosition
///  - All camera children (torch, light etc.) detach so they stay in world
///  - PlayerController is disabled while reading
///  - ESC or E exits and zooms back out
///  - F flips between front and back quad
/// </summary>
public class ReadableDocument : BasePuzzle
{
    [Header("Camera")]
    public Camera playerCamera;
    [Tooltip("Empty Transform placed in front of the document, facing it — same as puzzle camera positions")]
    public Transform puzzleCameraPosition;
    public float cameraZoomSpeed = 3f;

    [Header("Interaction")]
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;
    public KeyCode flipKey = KeyCode.F;
    [Tooltip("Untick to disable flipping — for single-sided documents")]
    public bool canFlip = true;

    // ── State ─────────────────────────────────────────────────────
    private enum DocState { Idle, ZoomingIn, Reading, ZoomingOut }
    private DocState _state = DocState.Idle;

    private bool _showingBack = false; // tracks orientation, not used for visibility
    private bool _showPrompt = false;

    private Vector3 _camOriginalPos;
    private Quaternion _camOriginalRot;
    private Quaternion _docOriginalRot;

    private struct DetachedChild
    {
        public Transform child, originalParent;
        public Vector3 worldPos;
        public Quaternion worldRot;
    }
    private List<DetachedChild> _detached = new List<DetachedChild>();

    private GUIStyle _promptStyle;
    private GUIStyle _hintStyle;

    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main ?? FindFirstObjectByType<Camera>();

    }

    protected override void Update()
    {
        base.Update();
        switch (_state)
        {
            case DocState.Idle: UpdateIdle(); break;
            case DocState.ZoomingIn: UpdateZoomIn(); break;
            case DocState.Reading: UpdateReading(); break;
        }
    }

    // ── Idle ──────────────────────────────────────────────────────

    void UpdateIdle()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, interactDistance)
                   && hitInfo.collider.gameObject == gameObject;
        _showPrompt = hit;

        if (hit && Input.GetKeyDown(interactKey))
        {
            OpenPuzzle();
            StartReading();
        }
    }

    void StartReading()
    {
        _camOriginalPos = playerCamera.transform.position;
        _camOriginalRot = playerCamera.transform.rotation;
        _state = DocState.ZoomingIn;

        // Disable player movement
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = false;

        // Detach all camera children (torch, light, etc.) so they stay in world
        _detached.Clear();
        var camChildren = new List<Transform>();
        foreach (Transform c in playerCamera.transform)
            camChildren.Add(c);
        foreach (var c in camChildren)
        {
            _detached.Add(new DetachedChild
            {
                child = c,
                originalParent = playerCamera.transform,
                worldPos = c.position,
                worldRot = c.rotation
            });
            c.SetParent(null, true);
        }

        _docOriginalRot = transform.rotation;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _showPrompt = false;
    }

    // ── Zoom in ───────────────────────────────────────────────────

    void UpdateZoomIn()
    {
        if (puzzleCameraPosition == null) { _state = DocState.Reading; return; }

        playerCamera.transform.position = Vector3.Lerp(
            playerCamera.transform.position,
            puzzleCameraPosition.position,
            Time.deltaTime * cameraZoomSpeed);

        playerCamera.transform.rotation = Quaternion.Slerp(
            playerCamera.transform.rotation,
            puzzleCameraPosition.rotation,
            Time.deltaTime * cameraZoomSpeed);

        if (Vector3.Distance(playerCamera.transform.position, puzzleCameraPosition.position) < 0.01f)
        {
            playerCamera.transform.SetPositionAndRotation(
                puzzleCameraPosition.position, puzzleCameraPosition.rotation);
            _state = DocState.Reading;
        }
    }

    // ── Reading ───────────────────────────────────────────────────

    void UpdateReading()
    {
        if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.Escape))
            StartCoroutine(ZoomOut());

        if (canFlip && Input.GetKeyDown(flipKey))
            StartCoroutine(Flip());
    }


    // ── Zoom out ──────────────────────────────────────────────────

    IEnumerator ZoomOut()
    {
        _state = DocState.ZoomingOut;

        float elapsed = 0f;
        float duration = 1f / cameraZoomSpeed;
        Vector3 sp = playerCamera.transform.position;
        Quaternion sr = playerCamera.transform.rotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            playerCamera.transform.position = Vector3.Lerp(sp, _camOriginalPos, t);
            playerCamera.transform.rotation = Quaternion.Slerp(sr, _camOriginalRot, t);
            yield return null;
        }

        playerCamera.transform.SetPositionAndRotation(_camOriginalPos, _camOriginalRot);

        // Re-parent camera children back
        foreach (var d in _detached)
        {
            if (d.child == null) continue;
            d.child.SetParent(d.originalParent, true);
            d.child.position = d.worldPos;
            d.child.rotation = d.worldRot;
        }
        _detached.Clear();

        // Reset to front
        _showingBack = false;

        // Re-enable player
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        ClosePuzzle();
        _state = DocState.Idle;
    }

    // ── Flip ──────────────────────────────────────────────────────

    IEnumerator Flip()
    {
        if (_state != DocState.Reading) yield break;
        _state = DocState.ZoomingOut; // block input during flip

        float flipDuration = 0.35f;

        Quaternion startRot = transform.rotation;
        // Flip 180 degrees around the document's local Y axis
        Quaternion endRot = startRot * Quaternion.Euler(0f, 180f, 0f);

        float elapsed = 0f;
        while (elapsed < flipDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / flipDuration);
            transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        transform.rotation = endRot;
        _showingBack = !_showingBack;

        _state = DocState.Reading;
    }

    // ── GUI ───────────────────────────────────────────────────────

    void OnGUI()
    {
        InitStyles();

        if (_state == DocState.Idle && _showPrompt)
            DrawCentred($"[{interactKey}]  Examine", _promptStyle, 60f);

        if (_state == DocState.Reading)
        {
            string hint = canFlip
                ? $"[{interactKey}] Put down     [{flipKey}] Flip"
                : $"[{interactKey}] Put down";
            DrawCentred(hint, _hintStyle, Screen.height * 0.35f);
        }
    }

    void DrawCentred(string text, GUIStyle style, float yOffset)
    {
        const float w = 520f, h = 36f;
        float x = (Screen.width - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f + yOffset;
        GUI.color = Color.black;
        GUI.Label(new Rect(x + 2, y + 2, w, h), text, style);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, w, h), text, style);
    }

    void InitStyles()
    {
        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 0.6f) }
            };

        if (_hintStyle == null)
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.8f) }
            };
    }

    void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}