using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HackingPuzzle : MonoBehaviour
{
    [Header("Camera")]
    public Camera playerCamera;
    public Transform puzzleCameraPosition;
    public float cameraZoomSpeed = 3f;

    [Header("Materials")]
    public Material hackedMaterial;
    public Material solvedMaterial;

    [Header("Interaction")]
    public float interactRange = 4f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Timer")]
    public float totalTime = 60f;

    [Header("On Solved")]
    public UnityEngine.Events.UnityEvent onSolved;

    // Grid
    private static readonly int[,] Layout = new int[7, 9]
    {
        { 0,1,1,1,0,1,1,1,0 },
        { 0,1,0,1,2,1,0,1,0 },
        { 1,1,0,1,1,1,0,1,1 },
        { 1,2,1,1,0,1,1,3,0 },
        { 1,1,0,1,1,1,0,1,0 },
        { 0,1,0,1,2,1,0,1,0 },
        { 0,1,1,1,0,1,1,1,0 },
    };
    private const int ROWS = 7, COLS = 9;

    private static readonly Dictionary<Vector2Int, string[]> LockSequences = new Dictionary<Vector2Int, string[]>
    {
        { new Vector2Int(1, 4), new[] { "U", "D", "R" } },
        { new Vector2Int(3, 1), new[] { "L", "U", "R", "D" } },
        { new Vector2Int(5, 4), new[] { "R", "R", "U", "L" } },
    };

    private enum State { Idle, ZoomingIn, Active, ZoomingOut, Solved }
    private State _state = State.Idle;

    private Vector3 _camOrigPos;
    private Quaternion _camOrigRot;

    private struct DetachedChild { public Transform child; public Transform parent; public Vector3 pos; public Quaternion rot; }
    private List<DetachedChild> _detached = new List<DetachedChild>();

    private Vector2Int _player;
    private HashSet<Vector2Int> _visited = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> _unlocked = new HashSet<Vector2Int>();
    private bool _inSequence;
    private Vector2Int _seqLock;
    private string[] _seqRequired;
    private List<string> _seqInput = new List<string>();
    private float _timeLeft;
    private bool _timerRunning;

    // UI
    private GameObject _panel;
    private GameObject _seqParent;
    private Image[,] _cellImages;
    private Text[,] _cellTexts;
    private List<Image> _arrowImages = new List<Image>();
    private List<Text> _arrowTexts = new List<Text>();
    private Text _statusText;
    private Text _timerText;
    private Image _timerBar;
    private Text _feedbackText;
    private Text _titleText;
    private Text _hintText;
    private RectTransform _timerFillRT;
    private float _timerBarMaxWidth = 580f;

    private float _feedbackTimer;

    private static readonly Color ColWall     = new Color(0.04f, 0.05f, 0.10f);
    private static readonly Color ColOpen     = new Color(0.06f, 0.11f, 0.18f);
    private static readonly Color ColPlayer   = new Color(0.06f, 0.25f, 0.45f);
    private static readonly Color ColGoal     = new Color(0.05f, 0.20f, 0.08f);
    private static readonly Color ColLock     = new Color(0.18f, 0.07f, 0.00f);
    private static readonly Color ColUnlocked = new Color(0.05f, 0.18f, 0.07f);
    private static readonly Color ColVisited  = new Color(0.04f, 0.08f, 0.13f);

    private GUIStyle _promptStyle;
    private Renderer _renderer;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (hackedMaterial != null) _renderer.material = hackedMaterial;

        if (playerCamera == null)
            playerCamera = Camera.main ?? FindFirstObjectByType<Camera>();

        BuildUI();
        _panel.SetActive(false);
    }

    void Update()
    {
        switch (_state)
        {
            case State.Idle:      UpdateIdle();   break;
            case State.ZoomingIn: UpdateZoomIn(); break;
            case State.Active:    UpdateActive(); break;
        }

        if (_feedbackTimer > 0f) _feedbackTimer -= Time.deltaTime;
        if (_feedbackText != null)
            _feedbackText.color = new Color(_feedbackText.color.r, _feedbackText.color.g, _feedbackText.color.b, Mathf.Min(1f, _feedbackTimer));
    }

    void UpdateIdle()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            if (hit.collider.gameObject == gameObject && Input.GetKeyDown(interactKey))
                StartPuzzle();
    }

    void UpdateZoomIn()
    {
        if (puzzleCameraPosition == null) { ActivatePuzzle(); return; }

        playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, puzzleCameraPosition.position, Time.deltaTime * cameraZoomSpeed);
        playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, puzzleCameraPosition.rotation, Time.deltaTime * cameraZoomSpeed);

        if (Vector3.Distance(playerCamera.transform.position, puzzleCameraPosition.position) < 0.01f)
            ActivatePuzzle();
    }

    void ActivatePuzzle()
    {
        playerCamera.transform.SetPositionAndRotation(puzzleCameraPosition.position, puzzleCameraPosition.rotation);
        _state = State.Active;
        _panel.SetActive(true);
        ResetPuzzleState();
        StartCoroutine(TimerRoutine());
    }

    void UpdateActive()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) { StartCoroutine(ExitPuzzle(false)); return; }

        if (!_inSequence)
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) TryMove(new Vector2Int(-1, 0));
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) TryMove(new Vector2Int(1, 0));
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) TryMove(new Vector2Int(0, -1));
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) TryMove(new Vector2Int(0, 1));
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) SeqInput("U");
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) SeqInput("D");
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) SeqInput("L");
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) SeqInput("R");
        }

        if (_timerRunning)
        {
            float pct = Mathf.Clamp01(_timeLeft / totalTime);
            if (_timerBar != null)
            {
                if (_timerFillRT != null)
                    _timerFillRT.sizeDelta = new Vector2(_timerBarMaxWidth * pct, 3f);

                _timerBar.color = Color.Lerp(new Color(0.9f, 0.1f, 0.1f), new Color(1f, 0.55f, 0.1f), pct);
            }
            if (_timerText != null) _timerText.text = Mathf.CeilToInt(_timeLeft) + "s";
        }
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
    }

    IEnumerator ExitPuzzle(bool solved)
    {
        _timerRunning = false;
        _state = State.ZoomingOut;
        _panel.SetActive(false);

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
        _state = solved ? State.Solved : State.Idle;

        if (solved)
        {
            if (solvedMaterial != null) _renderer.material = solvedMaterial;
            onSolved?.Invoke();
        }
    }

    void ResetPuzzleState()
    {
        _player = new Vector2Int(3, 0);
        _visited.Clear();
        _unlocked.Clear();
        _seqInput.Clear();
        _inSequence = false;
        _timeLeft = totalTime;
        _timerRunning = true;
        SetStatus("NAVIGATE TO EXIT — WASD / ARROW KEYS", new Color(0.4f, 0.85f, 1f));
        ClearArrowUI();
        RefreshGrid();
    }

    void TryMove(Vector2Int delta)
    {
        var next = _player + delta;
        if (next.x < 0 || next.x >= ROWS || next.y < 0 || next.y >= COLS) return;
        int t = Layout[next.x, next.y];
        if (t == 0) return;

        if (t == 2 && !_unlocked.Contains(next)) { BeginSequence(next); return; }

        _player = next;
        _visited.Add(next);
        if (t == 3) { StartCoroutine(SolvedExit()); return; }
        RefreshGrid();
    }

    void BeginSequence(Vector2Int lockPos)
    {
        _inSequence = true;
        _seqLock = lockPos;
        _seqRequired = LockSequences.ContainsKey(lockPos) ? LockSequences[lockPos] : new[] { "U", "R", "D" };
        _seqInput.Clear();
        SetStatus("INPUT SEQUENCE TO BYPASS LOCK", new Color(1f, 0.55f, 0.1f));
        RebuildArrowUI();
    }

    void SeqInput(string dir)
    {
        int idx = _seqInput.Count;
        if (dir == _seqRequired[idx])
        {
            _seqInput.Add(dir);
            UpdateArrowBox(idx, true);
            if (_seqInput.Count == _seqRequired.Length)
            {
                _unlocked.Add(_seqLock);
                _inSequence = false;
                _seqInput.Clear();
                SetStatus("LOCK BYPASSED", new Color(0.2f, 1f, 0.5f));
                ShowFeedback("LOCK BYPASSED", new Color(0.2f, 1f, 0.5f));
                ClearArrowUI();
                RefreshGrid();
            }
        }
        else
        {
            StartCoroutine(WrongFlash());
        }
    }

    IEnumerator WrongFlash()
    {
        foreach (var img in _arrowImages) if (img) img.color = new Color(0.5f, 0.04f, 0.08f);
        SetStatus("WRONG SEQUENCE — RETRY", new Color(1f, 0.15f, 0.25f));
        ShowFeedback("WRONG SEQUENCE", new Color(1f, 0.15f, 0.25f));
        yield return new WaitForSeconds(0.45f);
        _seqInput.Clear();
        RebuildArrowUI();
        SetStatus("INPUT SEQUENCE TO BYPASS LOCK", new Color(1f, 0.55f, 0.1f));
    }

    IEnumerator TimerRoutine()
    {
        while (_timerRunning && _timeLeft > 0f)
        {
            _timeLeft -= Time.deltaTime;
            yield return null;
        }
        if (_timerRunning && _state == State.Active)
        {
            _timerRunning = false;
            SetStatus("TIMER EXPIRED — ACCESS DENIED", new Color(1f, 0.15f, 0.25f));
            ShowFeedback("ACCESS DENIED", new Color(1f, 0.15f, 0.25f));
            yield return new WaitForSeconds(1.8f);
            yield return StartCoroutine(ExitPuzzle(false));
        }
    }

    IEnumerator SolvedExit()
    {
        _timerRunning = false;
        SetStatus("ACCESS GRANTED", new Color(0.2f, 1f, 0.5f));
        ShowFeedback("ACCESS GRANTED", new Color(0.2f, 1f, 0.5f));
        yield return new WaitForSeconds(1.5f);
        yield return StartCoroutine(ExitPuzzle(true));
    }

    void RefreshGrid()
    {
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                var pos = new Vector2Int(r, c);
                int type = Layout[r, c];
                var img = _cellImages[r, c];
                var txt = _cellTexts[r, c];
                bool isPlayer = pos == _player;
                bool isLock = type == 2;
                bool isUnlocked = _unlocked.Contains(pos);
                bool isVisited = _visited.Contains(pos);

                if (type == 0) img.color = ColWall;
                else if (isPlayer) img.color = ColPlayer;
                else if (type == 3) img.color = ColGoal;
                else if (isLock && !isUnlocked) img.color = ColLock;
                else if (isLock && isUnlocked) img.color = ColUnlocked;
                else if (isVisited) img.color = ColVisited;
                else img.color = ColOpen;

                if (txt != null)
                {
                    if (isPlayer) { txt.text = ">>"; txt.color = new Color(0.4f, 0.9f, 1f); }
                    else if (type == 3) { txt.text = "EXIT"; txt.color = new Color(0.3f, 1f, 0.5f); }
                    else if (isLock && !isUnlocked) { txt.text = "LCK"; txt.color = new Color(1f, 0.55f, 0.1f); }
                    else if (isLock && isUnlocked) { txt.text = "OK"; txt.color = new Color(0.3f, 1f, 0.5f); }
                    else txt.text = "";
                }
            }
        }
    }

    void RebuildArrowUI()
    {
        ClearArrowUI();
        if (_seqRequired == null) return;

        float boxW = 50f, spacing = 9f;
        float totalW = _seqRequired.Length * boxW + (_seqRequired.Length - 1) * spacing;
        float startX = -totalW / 2f + boxW / 2f;

        for (int i = 0; i < _seqRequired.Length; i++)
        {
            string sym = _seqRequired[i] == "U" ? "▲" : _seqRequired[i] == "D" ? "▼" : _seqRequired[i] == "L" ? "◄" : "►";

            var box = MakeGO($"Arrow{i}", _seqParent);
            var rt = box.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(boxW, boxW);
            rt.anchoredPosition = new Vector2(startX + i * (boxW + spacing), 0f);

            var img = box.AddComponent<Image>();
            img.color = new Color(0.06f, 0.11f, 0.18f);

            var txt = AddText(box, "T", sym, 26, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(boxW, boxW), Vector2.zero,
                new Color(0.2f, 0.5f, 0.9f), TextAnchor.MiddleCenter);

            _arrowImages.Add(img);
            _arrowTexts.Add(txt);
        }
    }

    void UpdateArrowBox(int idx, bool hit)
    {
        if (idx >= _arrowImages.Count) return;
        _arrowImages[idx].color = hit ? new Color(0f, 0.45f, 0.7f) : new Color(0.06f, 0.11f, 0.18f);
        if (_arrowTexts[idx] != null)
            _arrowTexts[idx].color = hit ? new Color(0.4f, 0.9f, 1f) : new Color(0.2f, 0.5f, 0.9f);
    }

    void ClearArrowUI()
    {
        foreach (var img in _arrowImages) if (img) Destroy(img.gameObject);
        _arrowImages.Clear();
        _arrowTexts.Clear();
    }

    void SetStatus(string msg, Color col)
    {
        if (_statusText != null)
        {
            _statusText.text = msg;
            _statusText.color = col;
        }
    }

    void ShowFeedback(string msg, Color col)
    {
        if (_feedbackText != null)
        {
            _feedbackText.text = msg;
            _feedbackText.color = col;
            _feedbackTimer = 2.5f;
        }
    }

    void OnGUI()
    {
        if (_state != State.Idle) return;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange) || hit.collider.gameObject != gameObject) return;

        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.cyan } };

        string msg = $"[{interactKey}] HACK TERMINAL";
        float pw = 420f, ph = 50f;
        float px = (Screen.width - pw) / 2f;
        float py = (Screen.height - ph) / 2f + 140f;

        GUI.color = Color.black; GUI.Label(new Rect(px + 3, py + 3, pw, ph), msg, _promptStyle);
        GUI.color = Color.white; GUI.Label(new Rect(px, py, pw, ph), msg, _promptStyle);
    }

    // ── UI BUILDER - Centered, thinner timer, better positioning ─────────────
    void BuildUI()
    {
        var cgo = new GameObject("HackPuzzleCanvas");
        DontDestroyOnLoad(cgo);
        var cv = cgo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 30;
        cgo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        cgo.AddComponent<GraphicRaycaster>();

        _panel = MakeGO("HackPanel", cgo);
        var panelRT = _panel.AddComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(1020f, 680f);
        _panel.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        _panel.AddComponent<Image>().color = new Color(0.015f, 0.03f, 0.09f, 0.97f);

        // Top line
        var line = MakeGO("Line", _panel);
        var lineRT = line.AddComponent<RectTransform>();
        lineRT.anchorMin = new Vector2(0f, 1f);
        lineRT.anchorMax = new Vector2(1f, 1f);
        lineRT.sizeDelta = new Vector2(0f, 3f);
        lineRT.anchoredPosition = new Vector2(0f, -22f);
        line.AddComponent<Image>().color = new Color(0f, 0.7f, 1f, 0.7f);

        // Title - upper left
        _titleText = AddText(_panel, "Title", "// SYSTEM BREACH", 19, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(380f, 40f), new Vector2(45f, -48f),
            new Color(0f, 0.85f, 1f), TextAnchor.MiddleLeft);

        // Hint
        _hintText = AddText(_panel, "Hint", "WASD / Arrows to move  •  Input sequences at LOCK nodes  •  Esc to abort", 
            12, FontStyle.Normal, new Vector2(0f, 1f), new Vector2(720f, 30f), new Vector2(45f, -78f),
            new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);

        var timerBg = MakeGO("TimerBg", _panel);
        var tbgRT = timerBg.AddComponent<RectTransform>();
        tbgRT.anchorMin = new Vector2(0.5f, 1f);
        tbgRT.anchorMax = new Vector2(0.5f, 1f);
        tbgRT.pivot = new Vector2(0.5f, 0.5f);
        tbgRT.sizeDelta = new Vector2(_timerBarMaxWidth, 3f);
        tbgRT.anchoredPosition = new Vector2(0f, -115f);
        timerBg.AddComponent<Image>().color = new Color(0.08f, 0.16f, 0.26f, 0.9f);

        var timerFill = MakeGO("TimerFill", timerBg);
        _timerFillRT = timerFill.AddComponent<RectTransform>();
        _timerFillRT.anchorMin = new Vector2(0f, 0.5f);
        _timerFillRT.anchorMax = new Vector2(0f, 0.5f);
        _timerFillRT.pivot = new Vector2(0f, 0.5f);
        _timerFillRT.sizeDelta = new Vector2(_timerBarMaxWidth, 3f);
        _timerFillRT.anchoredPosition = Vector2.zero;

        _timerBar = timerFill.AddComponent<Image>();
        _timerBar.type = Image.Type.Simple;
        _timerBar.color = new Color(1f, 0.55f, 0.1f);

        _timerText = AddText(_panel, "Timer", "60s", 17, FontStyle.Bold,
            new Vector2(1f, 1f), new Vector2(100f, 35f), new Vector2(-45f, -122f),
            new Color(1f, 0.6f, 0.15f), TextAnchor.MiddleRight);

        // Maze - moved down a bit
        float cellW = 64f, cellH = 54f, gap = 5.5f;
        float gridW = COLS * cellW + (COLS - 1) * gap;
        float gridY = 10f;   // lowered from previous versions

        _cellImages = new Image[ROWS, COLS];
        _cellTexts = new Text[ROWS, COLS];

        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                float x = -gridW / 2f + c * (cellW + gap) + cellW / 2f;
                float y = gridY + (ROWS - 1 - r) * (cellH + gap) - (ROWS * (cellH + gap) / 2f) + cellH / 2f;

                var cell = MakeGO($"Cell{r}_{c}", _panel);
                var rt = cell.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cellW, cellH);
                rt.anchoredPosition = new Vector2(x, y);

                _cellImages[r, c] = cell.AddComponent<Image>();
                _cellTexts[r, c] = AddText(cell, "T", "", 11, FontStyle.Bold,
                    new Vector2(0.5f, 0.5f), new Vector2(cellW, cellH), Vector2.zero,
                    Color.white, TextAnchor.MiddleCenter);
            }
        }

        // Sequence arrows - moved higher (closer to maze)
        _seqParent = MakeGO("SeqParent", _panel);
        var seqRT = _seqParent.AddComponent<RectTransform>();
        seqRT.anchorMin = seqRT.anchorMax = seqRT.pivot = new Vector2(0.5f, 0f);
        seqRT.sizeDelta = new Vector2(560f, 62f);
        seqRT.anchoredPosition = new Vector2(0f, -5f);   // raised

        // Status
        _statusText = AddText(_panel, "Status", "", 16, FontStyle.Bold,
            new Vector2(0.5f, 0f), new Vector2(800f, 42f), new Vector2(0f, -245f),
            new Color(0.45f, 0.9f, 1f), TextAnchor.MiddleCenter);

        // Feedback
        var fbGO = MakeGO("Feedback", _panel);
        var fbRT = fbGO.AddComponent<RectTransform>();
        fbRT.anchorMin = new Vector2(0.5f, 0f);
        fbRT.anchorMax = new Vector2(0.5f, 0f);
        fbRT.sizeDelta = new Vector2(700f, 48f);
        fbRT.anchoredPosition = new Vector2(0f, -305f);
        _feedbackText = fbGO.AddComponent<Text>();
        _feedbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _feedbackText.fontSize = 24;
        _feedbackText.fontStyle = FontStyle.Bold;
        _feedbackText.alignment = TextAnchor.MiddleCenter;
        _feedbackText.color = Color.clear;
    }

    static GameObject MakeGO(string name, GameObject parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static Text AddText(GameObject parent, string name, string text, int size, FontStyle style,
                        Vector2 anchor, Vector2 sizeDelta, Vector2 pos, Color color, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = pos;

        var t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }
}