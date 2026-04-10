using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SymphonyNote — Unity 6000.3.9f1
///
/// Attach to a paper/note GameObject (flat quad with collider).
/// Starts hidden. Activated by ShootingPuzzle (secret) or
/// BarBottleRoomManager (normal) when puzzle completes.
///
/// Player interacts → camera zooms in (same as WireTracePuzzle pattern) →
/// player reads the note → when they put it down, the matching syphons
/// are unlocked in BarRoomState.
/// </summary>
public class SymphonyNote : BasePuzzle
{
    public enum NoteType { Normal, Secret }

    [Header("Note Type")]
    [Tooltip("Normal = grants normal syphons. Secret = grants secret syphons.")]
    public NoteType noteType = NoteType.Normal;

    [Header("Camera")]
    public Camera playerCamera;
    public Transform puzzleCameraPosition;
    public float cameraZoomSpeed = 3f;

    [Header("Interaction")]
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;

    // ── State ─────────────────────────────────────────────────────
    private enum NoteState { Hidden, World, ZoomingIn, Reading, ZoomingOut }
    private NoteState _state = NoteState.Hidden;

    private bool _syphonsGranted = false;
    private bool _showPrompt = false;

    private Vector3 _camOriginalPos;
    private Quaternion _camOriginalRot;

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

        // Start hidden — activated by puzzle on solve
        gameObject.SetActive(false);
    }

    /// <summary>Called by ShootingPuzzle or BarBottleRoomManager to show the note.</summary>
    public void Activate()
    {
        gameObject.SetActive(true);
        _state = NoteState.World;
    }

    protected override void Update()
    {
        base.Update();
        switch (_state)
        {
            case NoteState.World: UpdateWorld(); break;
            case NoteState.ZoomingIn: UpdateZoomIn(); break;
            case NoteState.Reading: UpdateReading(); break;
        }
    }

    // ── World ─────────────────────────────────────────────────────

    void UpdateWorld()
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
        _state = NoteState.ZoomingIn;

        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = false;

        // Detach camera children (torch, light etc.)
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

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _showPrompt = false;
    }

    // ── Zoom in ───────────────────────────────────────────────────

    void UpdateZoomIn()
    {
        if (puzzleCameraPosition == null) { _state = NoteState.Reading; return; }

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
            _state = NoteState.Reading;
        }
    }

    // ── Reading ───────────────────────────────────────────────────

    void UpdateReading()
    {
        if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.Escape))
            StartCoroutine(ZoomOut());
    }


    // ── Zoom out ──────────────────────────────────────────────────

    IEnumerator ZoomOut()
    {
        _state = NoteState.ZoomingOut;

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

        // Re-parent camera children
        foreach (var d in _detached)
        {
            if (d.child == null) continue;
            d.child.SetParent(d.originalParent, true);
            d.child.position = d.worldPos;
            d.child.rotation = d.worldRot;
        }
        _detached.Clear();

        // Grant syphons on first read
        if (!_syphonsGranted)
        {
            _syphonsGranted = true;
            GrantSyphons();
        }

        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        ClosePuzzle();
        _state = NoteState.World;
    }

    // ─────────────────────────────────────────────────────────────

    void GrantSyphons()
    {
        switch (noteType)
        {
            case NoteType.Normal:
                BarRoomState.GrantNormalSyphons();
                break;
            case NoteType.Secret:
                BarRoomState.GrantSecretSyphons();
                break;
        }
    }

    // ── GUI ──────────────────────────────────────────────────────

    void OnGUI()
    {
        InitStyles();

        if (_state == NoteState.World && _showPrompt)
            DrawCentred($"[{interactKey}] Read note", _promptStyle, 60f);

        if (_state == NoteState.Reading)
            DrawCentred($"[{interactKey}] Put down", _hintStyle, Screen.height * 0.35f);
    }

    void DrawCentred(string text, GUIStyle style, float yOffset)
    {
        const float w = 480f, h = 36f;
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