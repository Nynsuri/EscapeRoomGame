using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CircuitRepairPuzzle.cs — Spider-Man PS4 style voltage puzzle.
/// PIECE POOL: 2x(+1,-1,+2,-2) and 1x(+4,-4) = 10 pieces total.
/// CIRCUIT SHAPE: branching L/T shaped path with multiple valid solutions.
/// Player places pieces on slots — running total must equal target at END node.
/// Multiple valid paths/combinations work.
/// </summary>
public class CircuitRepairPuzzle : BasePuzzle
{
    [Header("Camera")]
    public Camera playerCamera;
    public Transform puzzleCameraPosition;
    public float cameraZoomSpeed = 3f;

    [Header("Puzzle")]
    [Tooltip("The voltage the player must reach at the END node")]
    public int targetVoltage = 4;

    [Header("Requires Repair Kit")]
    public bool requireRepairKit = true;

    [Header("Sound Effects")]
    public AudioClip completedSound;

    [Header("Audio Mixer")]
    public UnityEngine.Audio.AudioMixerGroup audioMixerGroup;

    [Header("On Solved")]
    public UnityEngine.Events.UnityEvent onSolved;

    [Header("Interaction")]
    public float interactRange = 3f;
    public KeyCode interactKey = KeyCode.E;

    // ── State ─────────────────────────────────────────────────────
    private enum State { Idle, ZoomingIn, Active, Solved }
    private State _state = State.Idle;
    private bool _everSolved = false;

    private Vector3 _camOrigPos;
    private Quaternion _camOrigRot;
    private struct DetachedChild { public Transform child, parent; public Vector3 pos; public Quaternion rot; }
    private List<DetachedChild> _detached = new List<DetachedChild>();

    // ── Circuit layout ────────────────────────────────────────────
    private struct CircuitNode
    {
        public int id;
        public float gridX, gridY;
        public bool isStart, isEnd;
    }

    // Node layout – expanded for better spacing
    static readonly CircuitNode[] Nodes = {
        new CircuitNode { id=0, gridX=0, gridY=2, isStart=true  },
        new CircuitNode { id=1, gridX=1, gridY=2 },
        new CircuitNode { id=2, gridX=2, gridY=2 },
        new CircuitNode { id=3, gridX=3, gridY=2 },
        new CircuitNode { id=4, gridX=3, gridY=3 },
        new CircuitNode { id=5, gridX=4, gridY=3 },
        new CircuitNode { id=6, gridX=4, gridY=2 },
        new CircuitNode { id=7, gridX=5, gridY=2, isEnd=true },
    };

    static readonly (int, int)[] Edges = {
        (0,1),(1,2),(2,3),(3,6),(6,7),
        (3,4),(4,5),(5,6),
    };

    static readonly int[] SlotNodeIDs = { 1, 2, 3, 4, 5, 6 };
    static readonly int[] PiecePool = { 1, 1, -1, -1, 2, 2, -2, -2, 4, -4 };

    // Puzzle state
    private Dictionary<int, int> _slotValues = new Dictionary<int, int>();
    private List<int> _hand = new List<int>();
    private int _selectedHandIdx = -1;

    // UI
    private Canvas _canvas;
    private GameObject _panel;
    private Text _totalText, _targetText, _feedbackText;
    private float _feedbackTimer;
    private GUIStyle _promptStyle;

    private Dictionary<int, Image> _nodeImages = new Dictionary<int, Image>();
    private Dictionary<int, Text> _nodeTexts = new Dictionary<int, Text>();
    private Dictionary<int, Button> _nodeButtons = new Dictionary<int, Button>();

    private List<Image> _handImages = new List<Image>();
    private List<Text> _handTexts = new List<Text>();
    private List<Button> _handButtons = new List<Button>();

    // Colors – more vibrant
    static readonly Color ColBg = new Color(0.03f, 0.05f, 0.04f, 0.98f);
    static readonly Color ColNodeEmpty = new Color(0.12f, 0.22f, 0.14f);
    static readonly Color ColNodeFilled = new Color(0.18f, 0.45f, 0.22f);
    static readonly Color ColNodeHover = new Color(0.28f, 0.60f, 0.32f);
    static readonly Color ColStart = new Color(0.25f, 0.70f, 0.95f);
    static readonly Color ColEnd = new Color(0.95f, 0.60f, 0.10f);
    static readonly Color ColHandNormal = new Color(0.12f, 0.16f, 0.22f);
    static readonly Color ColHandSelect = new Color(0.95f, 0.75f, 0.15f);
    static readonly Color ColPositive = new Color(0.35f, 0.95f, 0.48f);
    static readonly Color ColNegative = new Color(0.95f, 0.35f, 0.35f);
    static readonly Color ColWire = new Color(0.30f, 0.85f, 0.35f);
    static readonly Color ColNeutral = new Color(0.80f, 0.80f, 0.80f);

    // ── Unity Lifecycle ───────────────────────────────────────────
    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main ?? FindFirstObjectByType<Camera>();
        BuildUI();
        _panel.SetActive(false);
    }

    protected override void Update()
    {
        base.Update();
        switch (_state)
        {
            case State.Idle: UpdateIdle(); break;
            case State.ZoomingIn: UpdateZoomIn(); break;
            case State.Active: UpdateActive(); break;
        }
        if (_feedbackTimer > 0f) _feedbackTimer -= Time.deltaTime;
    }

    void UpdateIdle()
    {
        if (_everSolved) return;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject) return;
        if (!Input.GetKeyDown(interactKey)) return;
        if (requireRepairKit && !HasRepairKit()) return;
        StartPuzzle();
    }

    void UpdateZoomIn()
    {
        if (puzzleCameraPosition == null) { BeginPuzzle(); return; }
        playerCamera.transform.position = Vector3.Lerp(
            playerCamera.transform.position, puzzleCameraPosition.position, Time.deltaTime * cameraZoomSpeed);
        playerCamera.transform.rotation = Quaternion.Slerp(
            playerCamera.transform.rotation, puzzleCameraPosition.rotation, Time.deltaTime * cameraZoomSpeed);
        if (Vector3.Distance(playerCamera.transform.position, puzzleCameraPosition.position) < 0.01f)
        {
            playerCamera.transform.SetPositionAndRotation(puzzleCameraPosition.position, puzzleCameraPosition.rotation);
            BeginPuzzle();
        }
    }

    void UpdateActive()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            StartCoroutine(ExitPuzzle(false));
    }

    // ── Puzzle Logic ──────────────────────────────────────────────
    bool HasRepairKit()
    {
        var inv = FindFirstObjectByType<Inventory>();
        if (inv == null) return false;
        foreach (var item in inv.Items)
            if (item is RepairKitItem) return true;
        return false;
    }

    void StartPuzzle()
    {
        OpenPuzzle();
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

    void BeginPuzzle()
    {
        _slotValues.Clear();
        _hand.Clear();
        _hand.AddRange(PiecePool);
        _selectedHandIdx = -1;
        RefreshHandUI();
        RefreshNodeUI();
        UpdateTotal();
        _panel.SetActive(true);
        _state = State.Active;
    }

    void OnNodeClicked(int nodeId)
    {
        if (_state != State.Active) return;

        // Remove piece if slot already filled
        if (_slotValues.ContainsKey(nodeId))
        {
            _hand.Add(_slotValues[nodeId]);
            _slotValues.Remove(nodeId);
            _selectedHandIdx = -1;
            RefreshHandUI();
            RefreshNodeUI();
            UpdateTotal();
            ShowFeedback("Piece removed", ColNeutral, 1.2f);
            return;
        }

        // Place selected piece
        if (_selectedHandIdx < 0 || _selectedHandIdx >= _hand.Count)
        {
            ShowFeedback("Select a piece from your hand first!", new Color(1f, 0.6f, 0f), 1.5f);
            return;
        }

        _slotValues[nodeId] = _hand[_selectedHandIdx];
        _hand.RemoveAt(_selectedHandIdx);
        _selectedHandIdx = -1;
        RefreshHandUI();
        RefreshNodeUI();
        UpdateTotal();
    }

    void OnHandClicked(int idx)
    {
        if (_state != State.Active) return;
        _selectedHandIdx = (_selectedHandIdx == idx) ? -1 : idx;
        RefreshHandUI();
    }

    void TrySubmit()
    {
        if (_state != State.Active) return;

        int total = 0;
        foreach (var v in _slotValues.Values) total += v;

        if (total == targetVoltage)
        {
            _everSolved = true;
            _state = State.Solved;
            ShowFeedback("✓ CIRCUIT REPAIRED!", ColPositive, 99f);
            if (completedSound != null)
                AudioHelper.Play(completedSound, transform.position, audioMixerGroup);
            StartCoroutine(FlashSolved());
            onSolved?.Invoke();
            StartCoroutine(SolvedExit());
        }
        else
        {
            ShowFeedback($"✗  Total: {(total >= 0 ? "+" : "")}{total}  —  Need {(targetVoltage >= 0 ? "+" : "")}{targetVoltage}", ColNegative, 2.5f);
            StartCoroutine(FlashError());
        }
    }

    IEnumerator FlashError()
    {
        foreach (var id in _slotValues.Keys)
            if (_nodeImages.ContainsKey(id)) _nodeImages[id].color = ColNegative;
        yield return new WaitForSeconds(0.5f);
        RefreshNodeUI();
    }

    IEnumerator FlashSolved()
    {
        for (int i = 0; i < 3; i++)
        {
            foreach (var id in _slotValues.Keys)
                if (_nodeImages.ContainsKey(id)) _nodeImages[id].color = ColPositive;
            yield return new WaitForSeconds(0.2f);
            foreach (var id in _slotValues.Keys)
                if (_nodeImages.ContainsKey(id)) _nodeImages[id].color = ColWire;
            yield return new WaitForSeconds(0.2f);
        }
    }

    IEnumerator SolvedExit()
    {
        yield return new WaitForSeconds(2.5f);
        yield return StartCoroutine(ExitPuzzle(true));
    }

    IEnumerator ExitPuzzle(bool solved)
    {
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
            d.child.position = d.pos; d.child.rotation = d.rot;
        }
        _detached.Clear();
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        ClosePuzzle();
        if (!solved) _state = State.Idle;
    }

    void UpdateTotal()
    {
        int total = 0;
        foreach (var v in _slotValues.Values) total += v;
        _totalText.text = $"RUNNING TOTAL:  {(total >= 0 ? "+" : "")}{total}";
        _totalText.color = (total == targetVoltage) ? ColPositive : ColNeutral;
    }

    void ShowFeedback(string msg, Color col, float dur)
    {
        if (_feedbackText == null) return;
        _feedbackText.text = msg;
        _feedbackText.color = col;
        _feedbackTimer = dur;
    }

    // ── UI Refresh ────────────────────────────────────────────────
    void RefreshNodeUI()
    {
        foreach (var node in Nodes)
        {
            if (!_nodeImages.ContainsKey(node.id)) continue;

            if (node.isStart)
            {
                _nodeImages[node.id].color = ColStart;
                _nodeTexts[node.id].text = "⚡ START";
                _nodeTexts[node.id].fontSize = 14;
                continue;
            }
            if (node.isEnd)
            {
                _nodeImages[node.id].color = ColEnd;
                _nodeTexts[node.id].text = $"END\n{(targetVoltage >= 0 ? "+" : "")}{targetVoltage}V";
                _nodeTexts[node.id].fontSize = 13;
                continue;
            }

            bool filled = _slotValues.ContainsKey(node.id);
            _nodeImages[node.id].color = filled ? ColNodeFilled : ColNodeEmpty;
            if (filled)
            {
                int v = _slotValues[node.id];
                _nodeTexts[node.id].text = (v >= 0 ? "+" : "") + v;
                _nodeTexts[node.id].color = v > 0 ? ColPositive : ColNegative;
                _nodeTexts[node.id].fontSize = 18;
            }
            else
            {
                _nodeTexts[node.id].text = "➕";
                _nodeTexts[node.id].color = new Color(1f, 1f, 1f, 0.4f);
                _nodeTexts[node.id].fontSize = 20;
            }
        }
    }

    void RefreshHandUI()
    {
        for (int i = 0; i < _handImages.Count; i++)
        {
            bool hasPiece = i < _hand.Count;
            _handImages[i].transform.parent.gameObject.SetActive(true);
            Button btn = _handButtons[i];
            btn.interactable = hasPiece;
            if (!hasPiece)
            {
                _handImages[i].color = new Color(0.1f, 0.1f, 0.1f, 0.4f);
                _handTexts[i].text = "—";
                _handTexts[i].color = Color.gray;
                continue;
            }

            bool sel = (i == _selectedHandIdx);
            _handImages[i].color = sel ? ColHandSelect : ColHandNormal;
            int v = _hand[i];
            _handTexts[i].text = (v >= 0 ? "+" : "") + v;
            _handTexts[i].color = v > 0 ? ColPositive : ColNegative;
        }
    }

    // ── UI Building (Scaled & Polished) ───────────────────────────
    void BuildUI()
    {
        var cgo = new GameObject("CircuitCanvas");
        _canvas = cgo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 25;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        cgo.AddComponent<GraphicRaycaster>();

        _panel = MakeGO("CircuitPanel", cgo);
        var prt = _panel.AddComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(1100f, 700f);  // Much bigger
        _panel.AddComponent<Image>().color = ColBg;

        // Glow border
        var bdr = MakeGO("Border", _panel);
        var brt = bdr.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-4, -4); brt.offsetMax = new Vector2(4, 4);
        var borderImg = bdr.AddComponent<Image>();
        borderImg.color = new Color(0.3f, 0.9f, 0.35f, 0.7f);
        borderImg.type = Image.Type.Sliced;
        bdr.transform.SetAsFirstSibling();

        // Title
        MakeText(_panel, "⚡ CIRCUIT REPAIR ⚡", 24, FontStyle.Bold, new Color(0.4f, 0.95f, 0.5f),
            new Vector2(0.5f, 1f), new Vector2(1000f, 50f), new Vector2(0f, -28f), TextAnchor.MiddleCenter);

        // Target
        _targetText = MakeText(_panel, $"TARGET VOLTAGE:  {(targetVoltage >= 0 ? "+" : "")}{targetVoltage}V", 16, FontStyle.Bold,
            new Color(1f, 0.7f, 0.2f), new Vector2(0.5f, 1f), new Vector2(1000f, 28f),
            new Vector2(0f, -72f), TextAnchor.MiddleCenter);

        // Total
        _totalText = MakeText(_panel, "RUNNING TOTAL:  +0", 18, FontStyle.Bold, ColNeutral,
            new Vector2(0.5f, 1f), new Vector2(1000f, 32f),
            new Vector2(0f, -105f), TextAnchor.MiddleCenter);

        // Circuit graph container
        var graphGO = MakeGO("Graph", _panel);
        var grt = graphGO.AddComponent<RectTransform>();
        grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.pivot = new Vector2(0.5f, 0.5f);
        grt.sizeDelta = new Vector2(900f, 260f);
        grt.anchoredPosition = new Vector2(0f, 40f);
        graphGO.AddComponent<Image>().color = new Color(0, 0, 0, 0);

        float nodeSize = 84f;
        float cellW = 140f;
        float cellH = 110f;

        // Draw edges (thicker lines)
        foreach (var (a, b) in Edges)
        {
            var na = System.Array.Find(Nodes, n => n.id == a);
            var nb = System.Array.Find(Nodes, n => n.id == b);
            float ax = na.gridX * cellW - 3f * cellW;
            float ay = na.gridY * cellH - 2f * cellH;
            float bx = nb.gridX * cellW - 3f * cellW;
            float by = nb.gridY * cellH - 2f * cellH;

            var line = MakeGO($"Edge_{a}_{b}", graphGO);
            var lrt = line.AddComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
            float dx = bx - ax, dy = by - ay;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            lrt.sizeDelta = new Vector2(len, 8f);
            lrt.anchoredPosition = new Vector2((ax + bx) / 2f, (ay + by) / 2f);
            lrt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            var wireImg = line.AddComponent<Image>();
            wireImg.color = ColWire;
            wireImg.type = Image.Type.Sliced;
        }

        // Draw nodes
        foreach (var node in Nodes)
        {
            int id = node.id;
            float nx = node.gridX * cellW - 3f * cellW;
            float ny = node.gridY * cellH - 2f * cellH;

            var nGO = MakeGO($"Node_{id}", graphGO);
            var nrt = nGO.AddComponent<RectTransform>();
            nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.sizeDelta = new Vector2(nodeSize, nodeSize);
            nrt.anchoredPosition = new Vector2(nx, ny);
            var img = nGO.AddComponent<Image>();
            img.type = Image.Type.Sliced;
            _nodeImages[id] = img;

            var txt = MakeText(nGO, "", 18, FontStyle.Bold, ColPositive,
                new Vector2(0.5f, 0.5f), new Vector2(nodeSize, nodeSize),
                Vector2.zero, TextAnchor.MiddleCenter);
            _nodeTexts[id] = txt;

            bool isSlot = System.Array.IndexOf(SlotNodeIDs, id) >= 0;
            if (isSlot)
            {
                var btn = nGO.AddComponent<Button>();
                btn.targetGraphic = img;
                var bc = btn.colors;
                bc.highlightedColor = ColNodeHover;
                btn.colors = bc;
                btn.onClick.AddListener(() => OnNodeClicked(id));
                _nodeButtons[id] = btn;
            }
        }
        RefreshNodeUI();

        // Hand label
        MakeText(_panel, "🔧 PIECES  (click to select → click slot to place, click placed slot to remove)", 14, FontStyle.Normal,
            new Color(0.7f, 0.9f, 0.7f, 0.9f), new Vector2(0.5f, 0f), new Vector2(1000f, 26f),
            new Vector2(0f, 180f), TextAnchor.MiddleCenter);

        // Hand container (wider to fit all pieces)
        var handCont = MakeGO("Hand", _panel);
        var hrt = handCont.AddComponent<RectTransform>();
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0f);
        hrt.pivot = new Vector2(0.5f, 0f);
        hrt.sizeDelta = new Vector2(1000f, 90f);
        hrt.anchoredPosition = new Vector2(0f, 110f);
        handCont.AddComponent<Image>().color = new Color(0, 0, 0, 0);

        float pW = 70f, pGap = 12f;
        float totalPW = PiecePool.Length * (pW + pGap) - pGap;
        float startPX = -totalPW / 2f + pW / 2f;

        for (int i = 0; i < PiecePool.Length; i++)
        {
            int idx = i;
            var pGO = MakeGO($"Piece{i}", handCont);
            var prt2 = pGO.AddComponent<RectTransform>();
            prt2.anchorMin = prt2.anchorMax = prt2.pivot = new Vector2(0.5f, 0.5f);
            prt2.sizeDelta = new Vector2(pW, 72f);
            prt2.anchoredPosition = new Vector2(startPX + i * (pW + pGap), 0f);
            var img2 = pGO.AddComponent<Image>();
            img2.type = Image.Type.Sliced;
            img2.color = ColHandNormal;
            _handImages.Add(img2);
            var btn2 = pGO.AddComponent<Button>();
            btn2.targetGraphic = img2;
            btn2.onClick.AddListener(() => OnHandClicked(idx));
            _handButtons.Add(btn2);
            int v = PiecePool[i];
            var t2 = MakeText(pGO, (v >= 0 ? "+" : "") + v, 24, FontStyle.Bold,
                v > 0 ? ColPositive : ColNegative,
                new Vector2(0.5f, 0.5f), new Vector2(pW, 72f), Vector2.zero, TextAnchor.MiddleCenter);
            _handTexts.Add(t2);
        }

        // Feedback
        var fbGO = MakeGO("Feedback", _panel);
        var fbrt = fbGO.AddComponent<RectTransform>();
        fbrt.anchorMin = fbrt.anchorMax = new Vector2(0.5f, 0f);
        fbrt.pivot = new Vector2(0.5f, 0f);
        fbrt.sizeDelta = new Vector2(900f, 40f);
        fbrt.anchoredPosition = new Vector2(0f, 70f);
        _feedbackText = fbGO.AddComponent<Text>();
        _feedbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _feedbackText.fontSize = 18;
        _feedbackText.fontStyle = FontStyle.Bold;
        _feedbackText.alignment = TextAnchor.MiddleCenter;
        _feedbackText.color = Color.white;

        // Submit button
        var subGO = MakeGO("Submit", _panel);
        var srt2 = subGO.AddComponent<RectTransform>();
        srt2.anchorMin = srt2.anchorMax = new Vector2(0.5f, 0f);
        srt2.pivot = new Vector2(0.5f, 0f);
        srt2.sizeDelta = new Vector2(220f, 50f);
        srt2.anchoredPosition = new Vector2(0f, 20f);
        var subImg = subGO.AddComponent<Image>();
        subImg.color = new Color(0.15f, 0.45f, 0.18f);
        subImg.type = Image.Type.Sliced;
        var subBtn = subGO.AddComponent<Button>();
        subBtn.targetGraphic = subImg;
        var sc = subBtn.colors;
        sc.highlightedColor = new Color(0.25f, 0.65f, 0.28f);
        subBtn.colors = sc;
        subBtn.onClick.AddListener(TrySubmit);
        MakeText(subGO, "✔ SUBMIT", 18, FontStyle.Bold, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(220f, 50f), Vector2.zero, TextAnchor.MiddleCenter);

        // Hint line
        MakeText(_panel, "Click placed piece to remove it   |   ESC — Exit puzzle", 12, FontStyle.Normal,
            new Color(1f, 1f, 1f, 0.35f), new Vector2(0.5f, 0f), new Vector2(900f, 20f),
            new Vector2(0f, 8f), TextAnchor.MiddleCenter);
    }

    // ── OnGUI (Interaction Prompt) ────────────────────────────────
    void OnGUI()
    {
        if (_state != State.Idle || _everSolved) return;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject) return;
        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        bool hasKit = !requireRepairKit || HasRepairKit();
        string msg = hasKit ? $"[{interactKey}]  Repair Circuit Board" : "🔧 Need a Repair Kit";
        float pw = 500f, ph = 48f, px = (Screen.width - pw) / 2f, py = (Screen.height - ph) / 2f + 80f;
        GUI.color = Color.black;
        GUI.Label(new Rect(px + 2, py + 2, pw, ph), msg, _promptStyle);
        GUI.color = hasKit ? Color.white : Color.red;
        GUI.Label(new Rect(px, py, pw, ph), msg, _promptStyle);
        GUI.color = Color.white;
    }

    // ── Helpers ───────────────────────────────────────────────────
    static GameObject MakeGO(string name, GameObject parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static Text MakeText(GameObject parent, string text, int size, FontStyle style,
        Color color, Vector2 anchor, Vector2 sizeDelta, Vector2 pos, TextAnchor align)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
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