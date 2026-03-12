using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WireTracePuzzle.cs — Unity 6000.3.9f1
/// Fixed for 3D wires: uses closest-point-on-ray-to-path-segment
/// instead of projecting onto a flat plane.
/// </summary>
public class WireTracePuzzle : MonoBehaviour
{
    [Header("Wire Path")]
    public Transform[] pathPoints;
    [Tooltip("How far in world units the cursor ray can be from the wire before reset")]
    public float tolerance = 0.15f;

    [Header("Camera Zoom")]
    public Camera playerCamera;
    public Transform puzzleCameraPosition;
    public float cameraZoomSpeed = 3f;

    [Header("On Complete — Key Object")]
    public Transform keyObject;
    public Transform keyDestination;
    public float keyMoveSpeed = 2f;

    [Header("On Complete — Drawer")]
    public Transform drawer;
    public float drawerOpenDistance = 0.35f;
    public float drawerOpenSpeed = 1.5f;

    [Header("Cursor Tracer")]
    public GameObject cursorDotPrefab;

    // ── State ─────────────────────────────────────────────────────
    private enum PuzzleState { Idle, ZoomingIn, Playing, Resetting, Completing, Done }
    private PuzzleState _state = PuzzleState.Idle;

    private int _currentPoint = 0;
    private Vector3 _camOriginalPos;
    private Quaternion _camOriginalRot;
    private GameObject _cursorDot;
    private GUIStyle _labelStyle;
    private string _message = "";
    private float _messageTimer = 0f;

    private struct DetachedChild
    {
        public Transform child;
        public Transform originalParent;
        public Vector3 worldPos;
        public Quaternion worldRot;
    }
    private List<DetachedChild> _detached = new List<DetachedChild>();
    private float _offWireTimer = 0f;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main ?? FindFirstObjectByType<Camera>();

        if (cursorDotPrefab != null)
        {
            _cursorDot = Instantiate(cursorDotPrefab);
            _cursorDot.SetActive(false);
        }
    }

    void Update()
    {
        switch (_state)
        {
            case PuzzleState.Idle: UpdateIdle(); break;
            case PuzzleState.ZoomingIn: UpdateZoomIn(); break;
            case PuzzleState.Playing: UpdatePlaying(); break;
        }
        if (_messageTimer > 0f) _messageTimer -= Time.deltaTime;
    }

    // ── Idle ─────────────────────────────────────────────────────

    void UpdateIdle()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 3f))
            if (hit.collider.gameObject == gameObject && Input.GetKeyDown(KeyCode.E))
                StartPuzzle();
    }

    void StartPuzzle()
    {
        _camOriginalPos = playerCamera.transform.position;
        _camOriginalRot = playerCamera.transform.rotation;
        _currentPoint = 0;
        _state = PuzzleState.ZoomingIn;

        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = false;

        // Detach all camera children (torch, light, etc.) so they stay in place
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
            c.SetParent(null, true); // unparent but keep world position
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ShowMessage("Follow the wire!");
    }

    // ── Zoom in ───────────────────────────────────────────────────

    void UpdateZoomIn()
    {
        if (puzzleCameraPosition == null) { _state = PuzzleState.Playing; return; }

        playerCamera.transform.position = Vector3.Lerp(
            playerCamera.transform.position, puzzleCameraPosition.position,
            Time.deltaTime * cameraZoomSpeed);
        playerCamera.transform.rotation = Quaternion.Slerp(
            playerCamera.transform.rotation, puzzleCameraPosition.rotation,
            Time.deltaTime * cameraZoomSpeed);

        if (Vector3.Distance(playerCamera.transform.position, puzzleCameraPosition.position) < 0.01f)
        {
            playerCamera.transform.SetPositionAndRotation(
                puzzleCameraPosition.position, puzzleCameraPosition.rotation);
            _state = PuzzleState.Playing;
            if (_cursorDot != null) _cursorDot.SetActive(true);
        }
    }

    // ── Playing ───────────────────────────────────────────────────

    void UpdatePlaying()
    {
        if (pathPoints == null || pathPoints.Length == 0) return;

        if (Input.GetKeyDown(KeyCode.Escape)) { StartCoroutine(ExitPuzzle()); return; }

        Ray mouseRay = playerCamera.ScreenPointToRay(Input.mousePosition);

        // ── Find the closest point on the wire PATH to the mouse RAY ──
        // This works for 3D wires — no flat plane needed.
        Transform target = pathPoints[_currentPoint];
        float distToTarget = RayToPointDistance(mouseRay, target.position);

        // Distance to the wire SEGMENT between previous and current point
        float distToWire;
        if (_currentPoint == 0)
        {
            distToWire = distToTarget;
        }
        else
        {
            Vector3 prev = pathPoints[_currentPoint - 1].position;
            Vector3 curr = target.position;
            distToWire = RayToSegmentDistance(mouseRay, prev, curr);
        }

        // Move cursor dot to closest point on wire from ray
        if (_cursorDot != null)
        {
            Vector3 closestOnWire = _currentPoint == 0
                ? target.position
                : ClosestPointOnSegmentToRay(mouseRay,
                    pathPoints[_currentPoint - 1].position, target.position);
            _cursorDot.transform.position = closestOnWire;
        }

        // ── Off wire: grace timer — must be off for 0.6s before reset ──
        if (distToWire > tolerance && _currentPoint > 3)
        {
            _offWireTimer += Time.deltaTime;
            if (_offWireTimer >= 0.6f)
            {
                _offWireTimer = 0f;
                StartCoroutine(ResetPuzzle());
            }
            return; // pause advancing but don't reset yet
        }
        else
        {
            _offWireTimer = 0f; // back on wire — clear timer
        }

        // Close enough to advance
        if (distToTarget < tolerance)
        {
            _currentPoint++;
            if (_currentPoint >= pathPoints.Length)
                StartCoroutine(CompletePuzzle());
            else
                ShowMessage($"{_currentPoint} / {pathPoints.Length}");
        }
    }

    // ── 3D Ray-to-Point distance ──────────────────────────────────
    // Returns the closest distance between a ray and a world point.

    static float RayToPointDistance(Ray ray, Vector3 point)
    {
        Vector3 v = point - ray.origin;
        float t = Vector3.Dot(v, ray.direction);
        Vector3 closest = ray.origin + ray.direction * Mathf.Max(0f, t);
        return Vector3.Distance(closest, point);
    }

    // ── 3D Ray-to-Segment distance ────────────────────────────────
    // Returns the shortest distance between a ray and a line segment.

    static float RayToSegmentDistance(Ray ray, Vector3 a, Vector3 b)
    {
        // Parametric: ray P(s) = O + s*D, segment Q(t) = A + t*(B-A)
        Vector3 d1 = ray.direction;
        Vector3 d2 = b - a;
        Vector3 r = ray.origin - a;

        float a1 = Vector3.Dot(d1, d1);
        float e = Vector3.Dot(d2, d2);
        float f = Vector3.Dot(d2, r);

        float s, t2;

        if (e < 1e-6f)
        {
            // Degenerate segment
            s = 0f;
            t2 = 0f;
        }
        else
        {
            float c = Vector3.Dot(d1, r);
            float b2 = Vector3.Dot(d1, d2);
            float denom = a1 * e - b2 * b2;

            s = denom != 0f ? Mathf.Clamp((b2 * f - c * e) / denom, 0f, float.MaxValue) : 0f;
            t2 = Mathf.Clamp01((b2 * s + f) / e);
            s = Mathf.Max(0f, (b2 * t2 - c) / a1);
        }

        Vector3 p1 = ray.origin + d1 * s;
        Vector3 p2 = a + d2 * t2;
        return Vector3.Distance(p1, p2);
    }

    static Vector3 ClosestPointOnSegmentToRay(Ray ray, Vector3 a, Vector3 b)
    {
        Vector3 d1 = ray.direction;
        Vector3 d2 = b - a;
        Vector3 r = ray.origin - a;

        float e = Vector3.Dot(d2, d2);
        float f = Vector3.Dot(d2, r);
        float b2 = Vector3.Dot(d1, d2);
        float c = Vector3.Dot(d1, r);
        float a1 = Vector3.Dot(d1, d1);
        float denom = a1 * e - b2 * b2;

        float s = denom != 0f ? Mathf.Max(0f, (b2 * f - c * e) / denom) : 0f;
        float t2 = Mathf.Clamp01((b2 * s + f) / e);
        return a + d2 * t2;
    }

    // ── Reset ─────────────────────────────────────────────────────

    IEnumerator ResetPuzzle()
    {
        _state = PuzzleState.Resetting;
        if (_cursorDot != null) _cursorDot.SetActive(false);
        ShowMessage("Off the wire! Resetting...");
        yield return new WaitForSeconds(0.4f);
        _currentPoint = 0;
        _state = PuzzleState.Playing;
        if (_cursorDot != null) _cursorDot.SetActive(true);
        ShowMessage("Try again — follow the wire!");
    }

    // ── Complete ──────────────────────────────────────────────────

    IEnumerator CompletePuzzle()
    {
        _state = PuzzleState.Completing;
        if (_cursorDot != null) _cursorDot.SetActive(false);
        ShowMessage("Puzzle solved!");
        yield return new WaitForSeconds(0.8f);
        yield return StartCoroutine(ZoomOut());

        if (keyObject != null && keyDestination != null)
            yield return StartCoroutine(MoveObject(keyObject, keyDestination.position, keyDestination.rotation, keyMoveSpeed));
        if (drawer != null)
            yield return StartCoroutine(OpenDrawer());

        _state = PuzzleState.Done;
        ShowMessage("A drawer has opened...");

        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    IEnumerator ZoomOut()
    {
        float elapsed = 0f, duration = 1f / cameraZoomSpeed;
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

        // Re-parent everything back to camera
        foreach (var d in _detached)
        {
            if (d.child == null) continue;
            d.child.SetParent(d.originalParent, true);
            d.child.position = d.worldPos;
            d.child.rotation = d.worldRot;
        }
        _detached.Clear();
    }

    IEnumerator MoveObject(Transform obj, Vector3 targetPos, Quaternion targetRot, float speed)
    {
        while (Vector3.Distance(obj.position, targetPos) > 0.005f)
        {
            obj.position = Vector3.MoveTowards(obj.position, targetPos, speed * Time.deltaTime);
            obj.rotation = Quaternion.RotateTowards(obj.rotation, targetRot, speed * 90f * Time.deltaTime);
            yield return null;
        }
        obj.SetPositionAndRotation(targetPos, targetRot);
    }

    IEnumerator OpenDrawer()
    {
        Vector3 closedPos = drawer.localPosition;
        Vector3 openPos = closedPos + new Vector3(-drawerOpenDistance, 0f, 0f);
        float elapsed = 0f, duration = drawerOpenDistance / drawerOpenSpeed;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            drawer.localPosition = Vector3.Lerp(closedPos, openPos, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        drawer.localPosition = openPos;
    }

    IEnumerator ExitPuzzle()
    {
        _state = PuzzleState.Resetting;
        if (_cursorDot != null) _cursorDot.SetActive(false);
        yield return StartCoroutine(ZoomOut());
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _state = PuzzleState.Idle;
    }

    // ── GUI ───────────────────────────────────────────────────────

    void OnGUI()
    {
        if (_messageTimer <= 0f) return;
        if (_labelStyle == null)
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

        float w = 500f, h = 50f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.75f;

        GUI.color = Color.black;
        GUI.Label(new Rect(x + 2, y + 2, w, h), _message, _labelStyle);
        GUI.color = _state == PuzzleState.Resetting ? Color.red : Color.white;
        GUI.Label(new Rect(x, y, w, h), _message, _labelStyle);
        GUI.color = Color.white;
    }

    void ShowMessage(string msg, float duration = 2f)
    {
        _message = msg; _messageTimer = duration;
    }
}