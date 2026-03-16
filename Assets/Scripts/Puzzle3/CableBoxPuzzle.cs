using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CableBoxPuzzle.cs — Unity 6000.3.9f1
///
/// SETUP:
/// 1. Attach to the box root GameObject (needs a Collider).
/// 2. Assign boxDoor (the door that swings open).
/// 3. Assign puzzleCameraPosition (empty GO looking at the cable panel).
/// 4. Assign the 4 left cable transforms and 4 right cable transforms (top to bottom).
///    Left cables: Red, Blue, Yellow, Green  (fixed, cannot move)
///    Right cables: the 4 sockets (also colored but scrambled)
/// 5. Assign lever transform, objectToDeleteStraight (3 parts parent), objectToDeleteDiagonal (1 object).
/// 6. Assign solveDrawer and set drawerOpenAxis to X.
///
/// HOW IT WORKS:
/// - First E press  → door swings open (Y axis rotation)
/// - Second E press → camera zooms in, cable UI appears
/// - Click a left cable → it highlights
/// - Click a right cable → connection is made (line drawn)
/// - Pull lever → validates solution
///   * All same color = correct connection → diagonal solve
///   * All straight (top-to-top etc.) = straight solve
///   * Wrong → flash red, reset
/// </summary>
public class CableBoxPuzzle : MonoBehaviour
{
    [Header("Camera")]
    public Camera playerCamera;
    public Transform puzzleCameraPosition;
    public float cameraZoomSpeed = 3f;

    [Header("Box Door")]
    public Transform boxDoor;
    public float doorOpenAngle = -110f; // degrees on Y axis
    public float doorOpenSpeed = 2f;

    [Header("Cable Socket Positions (assign empty GO at each cable tip for button tracking)")]
    [Tooltip("Left side tips, top to bottom: Red, Blue, Yellow, Green")]
    public Transform leftSocket0, leftSocket1, leftSocket2, leftSocket3;
    [Tooltip("Right side tips, top to bottom matching right colors")]
    public Transform rightSocket0, rightSocket1, rightSocket2, rightSocket3;

    [Header("Pre-placed Cable Visuals (place in scene, disable by default)")]
    [Tooltip("16 slots: cable[leftIndex][rightIndex] — assign the GameObject that visually connects left cable L to right cable R. Leave empty if that connection is not allowed.")]
    // cable_L0_R0 = left Red   -> right slot 0
    // cable_L0_R1 = left Red   -> right slot 1  etc.
    public GameObject cable_L0_R0, cable_L0_R1, cable_L0_R2, cable_L0_R3;
    public GameObject cable_L1_R0, cable_L1_R1, cable_L1_R2, cable_L1_R3;
    public GameObject cable_L2_R0, cable_L2_R1, cable_L2_R2, cable_L2_R3;
    public GameObject cable_L3_R0, cable_L3_R1, cable_L3_R2, cable_L3_R3;

    [Header("Right side cable colors (set to match your model, top to bottom)")]
    public CableColor rightColor0 = CableColor.Blue;
    public CableColor rightColor1 = CableColor.Red;
    public CableColor rightColor2 = CableColor.Green;
    public CableColor rightColor3 = CableColor.Yellow;

    [Header("Lever")]
    public Transform lever;
    [Tooltip("The X rotation the lever should reach when pulled (world euler X). E.g. if lever starts at 90, set this to 0 to pull it down to 0.")]
    public float leverTargetX = 0f;   // target localEulerAngles.X when pulled
    public float leverSpeed = 2f;

    [Header("Solve Objects — STRAIGHT solve (cables top-to-top)")]
    [Tooltip("These get DESTROYED on straight solve — assign the BISHOP piece and its parts here (wrong piece for this solve)")]
    public GameObject straightDeleteObject1;


    [Header("Solve Objects — DIAGONAL/COLOR solve (cables by color)")]
    [Tooltip("This gets DESTROYED on color solve — assign the ROOK piece here (wrong piece for this solve)")]
    public GameObject diagonalDeleteObject;

    [Header("Drawer")]
    public Transform drawerTransform;
    public float drawerOpenDistance = 0.3f;
    public float drawerOpenSpeed = 1.2f;
    [Tooltip("Direction drawer slides in LOCAL space. X=right, -X=left, Z=forward, -Z=back")]
    public Vector3 drawerOpenDirection = new Vector3(1f, 0f, 0f);

    [Header("Interaction")]
    public float interactRange = 4f;
    public KeyCode interactKey = KeyCode.E;



    // ── Enums ─────────────────────────────────────────────────────
    public enum CableColor { Red, Blue, Yellow, Green }

    // Left side colors are always fixed: 0=Red, 1=Blue, 2=Yellow, 3=Green
    static readonly CableColor[] LeftColors = {
        CableColor.Red, CableColor.Blue, CableColor.Yellow, CableColor.Green };

    static readonly Color[] UIColors = {
        new Color(0.86f, 0.20f, 0.20f), // Red
        new Color(0.20f, 0.39f, 0.86f), // Blue
        new Color(0.90f, 0.78f, 0.12f), // Yellow
        new Color(0.20f, 0.78f, 0.31f), // Green
    };

    // ── State ─────────────────────────────────────────────────────
    private enum State { Idle, DoorOpening, DoorOpen, ZoomingIn, Puzzle, LeverPulling, Solved }
    private State _state = State.Idle;

    private bool _doorOpen = false;
    private bool _leverPulled = false;

    // connections[i] = which right cable index left cable i is connected to, -1 = none
    private int[] _connections = { -1, -1, -1, -1 };
    private int _selectedLeft = -1; // currently highlighted left cable index

    // Camera
    private Vector3 _camOrigPos;
    private Quaternion _camOrigRot;

    // Detached camera children
    private struct DetachedChild { public Transform child, parent; public Vector3 pos; public Quaternion rot; }
    private List<DetachedChild> _detached = new List<DetachedChild>();

    // UI
    private Canvas _canvas;
    private GameObject _panel;
    private Button[] _leftBtns = new Button[4];
    private Button[] _rightBtns = new Button[4];
    private Image[] _leftHighlight = new Image[4];
    private Image[] _rightHighlight = new Image[4];
    private GameObject _lineContainer;
    private Image[] _connectionLines = new Image[4]; // one line per left cable
    private Text _feedbackText;
    private float _feedbackTimer;


    private GUIStyle _promptStyle;

    // Lever world button (sphere collider trigger in world space)
    private bool _leverReady = false;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main ?? FindFirstObjectByType<Camera>();
        BuildUI();
        _panel.SetActive(false);
    }

    private float _leverOriginalX;

    void Start()
    {
        // Save lever starting X so we can return it after a wrong attempt
        if (lever != null) _leverOriginalX = lever.localEulerAngles.x;

        // Hide all pre-placed cable visuals — done in Start so serialized fields are ready
        for (int l = 0; l < 4; l++)
            for (int r = 0; r < 4; r++)
            {
                var go = GetCableVisual(l, r);
                if (go != null) go.SetActive(false);
            }
    }

    void Update()
    {
        switch (_state)
        {
            case State.Idle: UpdateIdle(); break;
            case State.DoorOpen: UpdateDoorOpen(); break;
            case State.ZoomingIn: UpdateZoomIn(); break;
            case State.Puzzle: UpdatePuzzle(); break;
        }
        if (_feedbackTimer > 0f) _feedbackTimer -= Time.deltaTime;
    }

    // ── Idle: first E opens door ──────────────────────────────────
    void UpdateIdle()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject && !IsChildOf(hit.collider.gameObject, gameObject)) return;
        if (Input.GetKeyDown(interactKey))
            StartCoroutine(OpenDoor());
    }

    IEnumerator OpenDoor()
    {
        _state = State.DoorOpening;
        Quaternion startRot = boxDoor.localRotation;
        Quaternion endRot = Quaternion.Euler(0f, doorOpenAngle, 0f);
        float elapsed = 0f, dur = 1f / doorOpenSpeed;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            boxDoor.localRotation = Quaternion.Slerp(startRot, endRot, Mathf.SmoothStep(0f, 1f, elapsed / dur));
            yield return null;
        }
        boxDoor.localRotation = endRot;
        _doorOpen = true;
        _state = State.DoorOpen;
    }

    void UpdateDoorOpen()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject && !IsChildOf(hit.collider.gameObject, gameObject)) return;
        if (Input.GetKeyDown(interactKey))
            StartPuzzle();
    }

    void StartPuzzle()
    {
        _camOrigPos = playerCamera.transform.position;
        _camOrigRot = playerCamera.transform.rotation;
        _state = State.ZoomingIn;

        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = false;

        _detached.Clear();
        var kids = new List<Transform>();
        foreach (Transform c in playerCamera.transform) kids.Add(c);
        foreach (var c in kids)
        {
            _detached.Add(new DetachedChild { child = c, parent = playerCamera.transform, pos = c.position, rot = c.rotation });
            c.SetParent(null, true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ResetConnections();
    }

    void UpdateZoomIn()
    {
        if (_panel.activeSelf) UpdateButtonPositions();
        if (puzzleCameraPosition == null) { _state = State.Puzzle; _panel.SetActive(true); return; }

        playerCamera.transform.position = Vector3.Lerp(
            playerCamera.transform.position, puzzleCameraPosition.position, Time.deltaTime * cameraZoomSpeed);
        playerCamera.transform.rotation = Quaternion.Slerp(
            playerCamera.transform.rotation, puzzleCameraPosition.rotation, Time.deltaTime * cameraZoomSpeed);

        if (Vector3.Distance(playerCamera.transform.position, puzzleCameraPosition.position) < 0.01f)
        {
            playerCamera.transform.SetPositionAndRotation(puzzleCameraPosition.position, puzzleCameraPosition.rotation);
            _state = State.Puzzle;
            _panel.SetActive(true);
        }
    }

    void UpdatePuzzle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            StartCoroutine(ExitPuzzle());
        UpdateButtonPositions();
    }

    // ── Cable UI logic ────────────────────────────────────────────
    void OnLeftClicked(int idx)
    {
        _selectedLeft = (_selectedLeft == idx) ? -1 : idx;
        RefreshHighlights();
    }

    // Allowed right-side colors per left cable index
    // Left: 0=Red, 1=Blue, 2=Yellow, 3=Green
    // Red   -> Red, Blue
    // Blue  -> Blue, Red, Green, Yellow (any)
    // Yellow-> Red, Green, Yellow
    // Green -> Green, Yellow
    static readonly CableColor[][] AllowedConnections = {
        new[]{ CableColor.Red,  CableColor.Blue   },               // Red   -> Red, Blue
        new[]{ CableColor.Blue, CableColor.Red,  CableColor.Green }, // Blue  -> Blue, Red, Green (NOT Yellow)
        new[]{ CableColor.Red,  CableColor.Green, CableColor.Yellow }, // Yellow-> Red, Green, Yellow
        new[]{ CableColor.Green, CableColor.Yellow },              // Green -> Green, Yellow
    };

    bool IsAllowed(int leftIdx, int rightIdx)
    {
        CableColor[] rightColors = { rightColor0, rightColor1, rightColor2, rightColor3 };
        CableColor rightCol = rightColors[rightIdx];
        foreach (var allowed in AllowedConnections[leftIdx])
            if (allowed == rightCol) return true;
        return false;
    }

    void OnRightClicked(int rightIdx)
    {
        if (_selectedLeft < 0) return;

        // Check if connection is allowed
        if (!IsAllowed(_selectedLeft, rightIdx))
        {
            ShowFeedback("Can't connect those cables!", Color.red, 1.5f);
            _selectedLeft = -1;
            RefreshHighlights();
            return;
        }

        // Disconnect any existing connection for this left cable
        _connections[_selectedLeft] = -1;

        // Disconnect any other left cable that was connected to this right
        for (int i = 0; i < 4; i++)
            if (i != _selectedLeft && _connections[i] == rightIdx)
                _connections[i] = -1;

        _connections[_selectedLeft] = rightIdx;
        ShowCableVisual(_selectedLeft, rightIdx);
        _selectedLeft = -1;
        RefreshHighlights();
        DrawConnectionLines();

        // All connected — show message then exit back to player to pull lever
        if (AllConnected())
        {
            ShowFeedback("All connected! Go pull the lever!", new Color(1f, 0.9f, 0.3f), 2f);
            StartCoroutine(ExitAfterDelay(2f));
        }
    }

    void RefreshHighlights()
    {
        for (int i = 0; i < 4; i++)
        {
            bool sel = (_selectedLeft == i);
            bool con = (_connections[i] >= 0);
            _leftHighlight[i].color = sel ? Color.white : (con ? new Color(1f, 1f, 1f, 0.3f) : new Color(1f, 1f, 1f, 0f));
        }
        // Right highlights: show which ones are targeted
        var usedRight = new bool[4];
        foreach (int c in _connections) if (c >= 0 && c < 4) usedRight[c] = true;
        for (int i = 0; i < 4; i++)
            _rightHighlight[i].color = usedRight[i] ? new Color(1f, 1f, 1f, 0.3f) : new Color(1f, 1f, 1f, 0f);
    }

    bool AllConnected() => CablesAllConnected();
    public bool CablesAllConnected()
    {
        foreach (var c in _connections) if (c < 0) return false;
        return true;
    }

    // ── Lever pull (called from world interaction) ─────────────────
    // Called by LeverInteractable after lever animation completes
    public void ValidateFromLever()
    {
        if (_leverPulled) return;
        _leverPulled = true;
        _state = State.LeverPulling;
        ValidateSolution();
    }

    public void PullLever()
    {
        if (_state != State.Puzzle) return;
        if (!AllConnected()) { ShowFeedback("Connect all cables first!", Color.red, 2f); return; }
        if (_leverPulled) return;
        StartCoroutine(PullLeverCoroutine());
    }

    IEnumerator PullLeverCoroutine()
    {
        _state = State.LeverPulling;
        _leverPulled = true;

        Quaternion startRot = lever.localRotation;
        Vector3 startE = lever.localEulerAngles;
        Quaternion endRot = Quaternion.Euler(leverTargetX, startE.y, startE.z);
        float elapsed = 0f, dur = 1f / leverSpeed;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            lever.localRotation = Quaternion.Slerp(startRot, endRot, Mathf.SmoothStep(0f, 1f, elapsed / dur));
            yield return null;
        }
        lever.localRotation = endRot;

        ValidateSolution();
    }

    void ValidateSolution()
    {
        // Check color match (diagonal solve)
        CableColor[] rightColors = { rightColor0, rightColor1, rightColor2, rightColor3 };
        bool colorMatch = true;
        for (int i = 0; i < 4; i++)
        {
            int ri = _connections[i];
            if (ri < 0 || rightColors[ri] != LeftColors[i]) { colorMatch = false; break; }
        }

        // Check straight match
        bool straightMatch = true;
        for (int i = 0; i < 4; i++)
            if (_connections[i] != i) { straightMatch = false; break; }

        if (colorMatch)
        {
            ShowFeedback("✓ Correct! Color match!", Color.green, 3f);
            StartCoroutine(Solve(false));
        }
        else if (straightMatch)
        {
            ShowFeedback("✓ Secret solve! Straight connection!", new Color(1f, 0.8f, 0f), 3f);
            StartCoroutine(Solve(true));
        }
        else
        {
            ShowFeedback("✗ Wrong connections! Try again.", Color.red, 2f);
            StartCoroutine(ResetLeverAndConnections());
        }
    }

    IEnumerator ResetLeverAndConnections()
    {
        yield return new WaitForSeconds(2f);
        // Return lever back to its starting rotation
        Quaternion startRot = lever.localRotation;
        Vector3 startE = lever.localEulerAngles;
        Quaternion origRot = Quaternion.Euler(_leverOriginalX, startE.y, startE.z);
        float elapsed = 0f, dur = 0.5f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            lever.localRotation = Quaternion.Slerp(startRot, origRot, elapsed / dur);
            yield return null;
        }
        lever.localRotation = origRot;
        _leverPulled = false;
        var li = FindFirstObjectByType<LeverInteractable>();
        if (li != null) li.ResetPull();
        ResetConnections();
        _state = State.DoorOpen;
    }

    IEnumerator Solve(bool straight)
    {
        _state = State.Solved;
        yield return new WaitForSeconds(1.5f);

        // Delete the appropriate objects
        if (straight)
        {
            if (straightDeleteObject1 != null) Destroy(straightDeleteObject1);

        }
        else
        {
            if (diagonalDeleteObject != null) Destroy(diagonalDeleteObject);
        }

        // Open drawer and wait for it to finish
        if (drawerTransform != null)
            yield return StartCoroutine(OpenDrawer());

        yield return new WaitForSeconds(0.3f);
    }

    IEnumerator OpenDrawer()
    {
        Vector3 startPos = drawerTransform.localPosition;
        Vector3 endPos = startPos + drawerOpenDirection.normalized * drawerOpenDistance;
        float elapsed = 0f, dur = drawerOpenDistance / drawerOpenSpeed;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            drawerTransform.localPosition = Vector3.Lerp(startPos, endPos,
                Mathf.SmoothStep(0f, 1f, elapsed / dur));
            yield return null;
        }
        drawerTransform.localPosition = endPos;
    }

    IEnumerator ExitPuzzle()
    {
        _panel.SetActive(false);

        // Only zoom camera back if StartPuzzle actually moved it
        if (_detached.Count > 0 || _camOrigPos != Vector3.zero)
        {
            float elapsed = 0f, dur = 1f / cameraZoomSpeed;
            Vector3 sp = playerCamera.transform.position;
            Quaternion sr = playerCamera.transform.rotation;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                playerCamera.transform.position = Vector3.Lerp(sp, _camOrigPos, t);
                playerCamera.transform.rotation = Quaternion.Slerp(sr, _camOrigRot, t);
                yield return null;
            }
            playerCamera.transform.SetPositionAndRotation(_camOrigPos, _camOrigRot);

            foreach (var d in _detached)
            {
                if (d.child == null) continue;
                d.child.SetParent(d.parent, true);
                d.child.position = d.pos;
                d.child.rotation = d.rot;
            }
            _detached.Clear();

            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) pc.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (_state != State.Solved)
            _state = _doorOpen ? State.DoorOpen : State.Idle;
    }

    GameObject GetCableVisual(int l, int r)
    {
        // Return the pre-placed cable GO for this left→right combination
        if (l == 0) { if (r == 0) return cable_L0_R0; if (r == 1) return cable_L0_R1; if (r == 2) return cable_L0_R2; return cable_L0_R3; }
        if (l == 1) { if (r == 0) return cable_L1_R0; if (r == 1) return cable_L1_R1; if (r == 2) return cable_L1_R2; return cable_L1_R3; }
        if (l == 2) { if (r == 0) return cable_L2_R0; if (r == 1) return cable_L2_R1; if (r == 2) return cable_L2_R2; return cable_L2_R3; }
        { if (r == 0) return cable_L3_R0; if (r == 1) return cable_L3_R1; if (r == 2) return cable_L3_R2; return cable_L3_R3; }
    }

    void ShowCableVisual(int leftIdx, int rightIdx)
    {
        // Hide any cable currently shown for this left slot
        for (int r = 0; r < 4; r++)
        {
            var go = GetCableVisual(leftIdx, r);
            if (go != null) go.SetActive(false);
        }
        // Show the new one
        var cable = GetCableVisual(leftIdx, rightIdx);
        if (cable != null) cable.SetActive(true);
    }

    IEnumerator ExitAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        yield return StartCoroutine(ExitPuzzle());
    }

    void ResetConnections()
    {
        for (int i = 0; i < 4; i++)
        {
            _connections[i] = -1;
            // Hide all cable visuals for this left slot
            for (int r = 0; r < 4; r++)
            {
                var go = GetCableVisual(i, r);
                if (go != null) go.SetActive(false);
            }
        }
        _selectedLeft = -1;
        RefreshHighlights();
        DrawConnectionLines();

    }

    // ── OnGUI prompt ──────────────────────────────────────────────
    void OnGUI()
    {
        if (_state != State.Idle && _state != State.DoorOpen) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject && !IsChildOf(hit.collider.gameObject, gameObject)) return;

        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

        string msg = _state == State.Idle ? "[E]  Open Box" : "[E]  Inspect Cables";
        float pw = 400f, ph = 40f;
        float px = (Screen.width - pw) / 2f;
        float py = (Screen.height - ph) / 2f + 60f;
        GUI.color = Color.black; GUI.Label(new Rect(px + 2, py + 2, pw, ph), msg, _promptStyle);
        GUI.color = Color.white; GUI.Label(new Rect(px, py, pw, ph), msg, _promptStyle);
    }

    // ── UI Build ──────────────────────────────────────────────────
    // RectTransforms for world-tracked buttons
    private RectTransform[] _leftRTs = new RectTransform[4];
    private RectTransform[] _rightRTs = new RectTransform[4];
    private RectTransform _feedbackRT;

    void BuildUI()
    {
        var cgo = new GameObject("CableBoxCanvas");
        _canvas = cgo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 20;
        cgo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        cgo.AddComponent<GraphicRaycaster>();

        // Transparent full-screen panel
        _panel = MakePanel(cgo, new Color(0f, 0f, 0f, 0f));

        CableColor[] rightColors = { rightColor0, rightColor1, rightColor2, rightColor3 };

        for (int i = 0; i < 4; i++)
        {
            int idx = i;

            // Left button — positioned over left socket each frame
            var leftGO = new GameObject($"LBtn{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            leftGO.transform.SetParent(_panel.transform, false);
            var lrt = leftGO.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(70f, 22f);
            leftGO.GetComponent<Image>().color = ColorToUI(LeftColors[i]);
            var lBtn = leftGO.GetComponent<Button>();
            lBtn.targetGraphic = leftGO.GetComponent<Image>();
            var lc = lBtn.colors;
            lc.normalColor = ColorToUI(LeftColors[i]);
            lc.highlightedColor = Color.Lerp(ColorToUI(LeftColors[i]), Color.white, 0.4f);
            lc.pressedColor = Color.Lerp(ColorToUI(LeftColors[i]), Color.black, 0.3f);
            lBtn.colors = lc;
            lBtn.onClick.AddListener(() => OnLeftClicked(idx));
            _leftBtns[i] = lBtn;
            _leftRTs[i] = lrt;

            // Highlight overlay
            var hlGO = MakePanel(leftGO, new Color(1f, 1f, 1f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(76f, 28f), Vector2.zero);
            _leftHighlight[i] = hlGO.GetComponent<Image>();

            // Right button — positioned over right socket each frame
            var rightGO = new GameObject($"RBtn{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            rightGO.transform.SetParent(_panel.transform, false);
            var rrt = rightGO.GetComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = new Vector2(70f, 22f);
            rightGO.GetComponent<Image>().color = ColorToUI(rightColors[i]);
            var rBtn = rightGO.GetComponent<Button>();
            rBtn.targetGraphic = rightGO.GetComponent<Image>();
            var rc = rBtn.colors;
            rc.normalColor = ColorToUI(rightColors[i]);
            rc.highlightedColor = Color.Lerp(ColorToUI(rightColors[i]), Color.white, 0.4f);
            rc.pressedColor = Color.Lerp(ColorToUI(rightColors[i]), Color.black, 0.3f);
            rBtn.colors = rc;
            rBtn.onClick.AddListener(() => OnRightClicked(idx));
            _rightBtns[i] = rBtn;
            _rightRTs[i] = rrt;

            var rhlGO = MakePanel(rightGO, new Color(1f, 1f, 1f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(76f, 28f), Vector2.zero);
            _rightHighlight[i] = rhlGO.GetComponent<Image>();

            // Connection line (hidden — no lines needed)
            var lineGO = new GameObject($"Line{i}", typeof(RectTransform), typeof(Image));
            lineGO.transform.SetParent(_panel.transform, false);
            lineGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // always invisible
            _connectionLines[i] = lineGO.GetComponent<Image>();
        }

        // Feedback text — bottom center of screen
        var fbGO = new GameObject("Feedback", typeof(RectTransform), typeof(Text));
        fbGO.transform.SetParent(_panel.transform, false);
        _feedbackRT = fbGO.GetComponent<RectTransform>();
        _feedbackRT.anchorMin = _feedbackRT.anchorMax = new Vector2(0.5f, 0f);
        _feedbackRT.pivot = new Vector2(0.5f, 0f);
        _feedbackRT.sizeDelta = new Vector2(600f, 30f);
        _feedbackRT.anchoredPosition = new Vector2(0f, 60f);
        _feedbackText = fbGO.GetComponent<Text>();
        _feedbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _feedbackText.fontSize = 15;
        _feedbackText.fontStyle = FontStyle.Bold;
        _feedbackText.alignment = TextAnchor.MiddleCenter;
        _feedbackText.color = Color.white;

    }

    // Called every frame while puzzle is active to keep buttons on top of cables
    void UpdateButtonPositions()
    {
        Transform[] lefts = { leftSocket0, leftSocket1, leftSocket2, leftSocket3 };
        Transform[] rights = { rightSocket0, rightSocket1, rightSocket2, rightSocket3 };

        for (int i = 0; i < 4; i++)
        {
            if (lefts[i] != null && _leftRTs[i] != null)
            {
                Vector3 sp = playerCamera.WorldToScreenPoint(lefts[i].position);
                if (sp.z > 0) _leftRTs[i].anchoredPosition = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f);
            }
            if (rights[i] != null && _rightRTs[i] != null)
            {
                Vector3 sp = playerCamera.WorldToScreenPoint(rights[i].position);
                if (sp.z > 0) _rightRTs[i].anchoredPosition = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f);
            }
        }
    }

    void DrawConnectionLines() { } // lines hidden — cables shown in world space

    void ShowFeedback(string msg, Color col, float dur)
    {
        if (_feedbackText == null) return;
        _feedbackText.text = msg;
        _feedbackText.color = col;
        _feedbackTimer = dur;
    }

    // ── Helpers ───────────────────────────────────────────────────
    static bool IsChildOf(GameObject child, GameObject parent)
    {
        Transform t = child.transform;
        while (t != null) { if (t.gameObject == parent) return true; t = t.parent; }
        return false;
    }

    static Color ColorToUI(CableColor c) => UIColors[(int)c];
    static string ColorName(CableColor c) => c.ToString().ToUpper();

    static GameObject MakePanel(GameObject parent, Color color,
        Vector2 anchor, Vector2 size, Vector2 pos)
    {
        var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        go.GetComponent<Image>().color = color;
        return go;
    }

    static GameObject MakePanel(GameObject parent, Color color)
    {
        var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return go;
    }

    static void AddLabel(GameObject parent, string text, int size, FontStyle style, Color color,
        Vector2 anchor, Vector2 sizeDelta, Vector2 pos, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = sizeDelta; rt.anchoredPosition = pos;
        var t = go.GetComponent<Text>();
        t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color; t.alignment = align;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}