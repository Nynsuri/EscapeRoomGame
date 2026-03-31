using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CircuitRepairPuzzle.cs — Unity 6000.3.9f1
/// Spider-Man PS4 style voltage circuit puzzle.
///
/// SETUP:
/// 1. Attach to the circuit board GameObject (needs a Collider).
/// 2. Assign puzzleCameraPosition.
/// 3. Requires player to have a RepairKitItem in inventory.
/// 4. Wire onSolved to unlock the repair box magnetic lock.
///
/// HOW IT WORKS:
/// - Linear row of slots from START to END
/// - Current voltage shown running left-to-right
/// - Hand of voltage pieces (+1 +2 -1 -2 +4 -4) shown at bottom
/// - Click a hand piece to select it, click an empty slot to place it
/// - Placed pieces can be clicked to flip sign (+2 ↔ -2) or right-click to remove
/// - Running total shown — must equal target exactly at last slot
/// - Submit button confirms — win if total == target, else flash error
/// </summary>
public class CircuitRepairPuzzle : MonoBehaviour
{
    [Header("Camera")]
    public Camera    playerCamera;
    public Transform puzzleCameraPosition;
    public float     cameraZoomSpeed = 3f;

    [Header("Puzzle")]
    public int targetVoltage  = 4;
    public int slotCount      = 6;   // number of circuit slots

    [Header("Interaction")]
    public float   interactRange = 3f;
    public KeyCode interactKey   = KeyCode.E;

    [Header("Requires Repair Kit")]
    [Tooltip("Player must have a RepairKitItem in inventory to start")]
    public bool requireRepairKit = true;

    [Header("On Solved")]
    public UnityEngine.Events.UnityEvent onSolved;

    // ── State ─────────────────────────────────────────────────────
    private enum State { Idle, ZoomingIn, Active, Solved }
    private State _state = State.Idle;

    private Vector3    _camOrigPos;
    private Quaternion _camOrigRot;
    private struct DetachedChild { public Transform child, parent; public Vector3 pos; public Quaternion rot; }
    private List<DetachedChild> _detached = new List<DetachedChild>();

    // Puzzle data
    private int[] _slots;         // value placed in each slot, 0 = empty
    private bool[] _slotFilled;
    private List<int> _hand;      // available pieces
    private int _selectedHandIdx = -1;
    private bool _solved = false;

    // Piece values available
    static readonly int[] PiecePool = { 1, 1, 2, -1, -1, -2, 4, -4 };

    // UI
    private GameObject  _panel;
    private Text        _totalText;
    private Text        _targetText;
    private Text        _feedbackText;
    private float       _feedbackTimer;
    private Image[]     _slotImages;
    private Text[]      _slotTexts;
    private Button[]    _slotButtons;
    private GameObject  _handContainer;
    private List<Image> _handImages  = new List<Image>();
    private List<Text>  _handTexts   = new List<Text>();
    private List<Button>_handButtons = new List<Button>();

    private GUIStyle _promptStyle;

    static readonly Color ColBg         = new Color(0.08f, 0.10f, 0.08f, 0.97f);
    static readonly Color ColSlotEmpty  = new Color(0.12f, 0.18f, 0.12f);
    static readonly Color ColSlotFilled = new Color(0.15f, 0.35f, 0.15f);
    static readonly Color ColSlotHover  = new Color(0.20f, 0.45f, 0.20f);
    static readonly Color ColHandNormal = new Color(0.18f, 0.22f, 0.30f);
    static readonly Color ColHandSelect = new Color(0.90f, 0.65f, 0.10f);
    static readonly Color ColPositive   = new Color(0.3f,  0.9f,  0.4f);
    static readonly Color ColNegative   = new Color(0.9f,  0.3f,  0.3f);
    static readonly Color ColNeutral    = new Color(0.7f,  0.7f,  0.7f);
    static readonly Color ColTarget     = new Color(1.0f,  0.6f,  0.0f);
    static readonly Color ColWire       = new Color(0.2f,  0.8f,  0.2f);

    // ── Awake ─────────────────────────────────────────────────────
    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main ?? FindFirstObjectByType<Camera>();
        BuildUI();
        _panel.SetActive(false);
    }

    // ── Update ────────────────────────────────────────────────────
    void Update()
    {
        switch (_state)
        {
            case State.Idle:     UpdateIdle();    break;
            case State.ZoomingIn:UpdateZoomIn();  break;
            case State.Active:   UpdateActive();  break;
        }
        if (_feedbackTimer > 0f) _feedbackTimer -= Time.deltaTime;
    }

    void UpdateIdle()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject) return;
        if (!Input.GetKeyDown(interactKey)) return;

        if (requireRepairKit && !HasRepairKit())
        {
            // Show no kit message — handled in OnGUI
            return;
        }
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
            StartCoroutine(ExitPuzzle());
    }

    // ── Puzzle logic ──────────────────────────────────────────────

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
        Cursor.visible   = true;
    }

    void BeginPuzzle()
    {
        // Init puzzle state
        _slots      = new int[slotCount];
        _slotFilled = new bool[slotCount];
        _hand       = new List<int>(PiecePool);
        _selectedHandIdx = -1;
        _solved = false;

        // Rebuild slot/hand UI for current slotCount
        RebuildSlotUI();
        RebuildHandUI();
        RefreshUI();

        _panel.SetActive(true);
        _state = State.Active;
    }

    void OnSlotClicked(int slotIdx)
    {
        if (_state != State.Active) return;

        if (_slotFilled[slotIdx])
        {
            // Return piece to hand
            _hand.Add(_slots[slotIdx]);
            _slots[slotIdx]      = 0;
            _slotFilled[slotIdx] = false;
            _selectedHandIdx     = -1;
            RefreshUI();
            return;
        }

        if (_selectedHandIdx < 0 || _selectedHandIdx >= _hand.Count) return;

        // Place selected hand piece into slot
        _slots[slotIdx]      = _hand[_selectedHandIdx];
        _slotFilled[slotIdx] = true;
        _hand.RemoveAt(_selectedHandIdx);
        _selectedHandIdx = -1;
        RefreshUI();
    }

    void OnHandClicked(int handIdx)
    {
        if (_state != State.Active) return;
        _selectedHandIdx = (_selectedHandIdx == handIdx) ? -1 : handIdx;
        RefreshUI();
    }

    void OnFlipSlot(int slotIdx)
    {
        if (!_slotFilled[slotIdx]) return;
        _slots[slotIdx] = -_slots[slotIdx];
        RefreshUI();
    }

    void TrySubmit()
    {
        int total = GetRunningTotal();
        if (total == targetVoltage)
        {
            _solved = true;
            _state  = State.Solved;
            ShowFeedback("✓ CIRCUIT REPAIRED!", ColPositive, 99f);
            // Flash all slots green
            for (int i = 0; i < _slotImages.Length; i++)
                if (_slotFilled[i]) _slotImages[i].color = ColPositive;
            onSolved?.Invoke();
            StartCoroutine(SolvedExit());
        }
        else
        {
            ShowFeedback($"✗  Voltage is {total}, need {targetVoltage}", ColNegative, 2f);
            StartCoroutine(FlashError());
        }
    }

    IEnumerator FlashError()
    {
        for (int i = 0; i < _slotImages.Length; i++)
            if (_slotFilled[i]) _slotImages[i].color = ColNegative;
        yield return new WaitForSeconds(0.4f);
        RefreshUI();
    }

    IEnumerator SolvedExit()
    {
        yield return new WaitForSeconds(2f);
        yield return StartCoroutine(ExitPuzzle());
    }

    IEnumerator ExitPuzzle()
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
            d.child.position = d.pos;
            d.child.rotation = d.rot;
        }
        _detached.Clear();
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        if (!_solved) _state = State.Idle;
    }

    int GetRunningTotal()
    {
        int t = 0;
        foreach (var s in _slots) t += s;
        return t;
    }

    void ShowFeedback(string msg, Color col, float dur)
    {
        if (_feedbackText == null) return;
        _feedbackText.text  = msg;
        _feedbackText.color = col;
        _feedbackTimer = dur;
    }

    // ── UI Refresh ────────────────────────────────────────────────

    void RefreshUI()
    {
        // Update slots
        for (int i = 0; i < slotCount; i++)
        {
            if (i >= _slotImages.Length) break;
            if (_slotFilled[i])
            {
                _slotImages[i].color = ColSlotFilled;
                int v = _slots[i];
                _slotTexts[i].text  = (v > 0 ? "+" : "") + v.ToString();
                _slotTexts[i].color = v > 0 ? ColPositive : ColNegative;
            }
            else
            {
                _slotImages[i].color = ColSlotEmpty;
                _slotTexts[i].text   = "";
            }
        }

        // Update hand
        for (int i = 0; i < _handImages.Count; i++)
        {
            if (i >= _hand.Count)
            {
                _handImages[i].transform.parent.gameObject.SetActive(false);
                continue;
            }
            _handImages[i].transform.parent.gameObject.SetActive(true);
            bool sel = (i == _selectedHandIdx);
            _handImages[i].color = sel ? ColHandSelect : ColHandNormal;
            int v = _hand[i];
            _handTexts[i].text  = (v > 0 ? "+" : "") + v.ToString();
            _handTexts[i].color = v > 0 ? ColPositive : (v < 0 ? ColNegative : ColNeutral);
        }

        // Running total
        int total = GetRunningTotal();
        _totalText.text  = $"Current: {(total >= 0 ? "+" : "")}{total}";
        _totalText.color = (total == targetVoltage) ? ColPositive : ColNeutral;
    }

    // ── UI Build ──────────────────────────────────────────────────

    void BuildUI()
    {
        var cgo = new GameObject("CircuitCanvas");
        var cv  = cgo.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 25;
        cgo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        cgo.AddComponent<GraphicRaycaster>();

        // Main panel
        _panel = MakeGO("CircuitPanel", cgo);
        var prt = _panel.AddComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot     = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(720f, 460f);
        _panel.AddComponent<Image>().color = ColBg;

        // Border
        var bdr = MakeGO("Border", _panel);
        var brt = bdr.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-3,-3); brt.offsetMax = new Vector2(3,3);
        bdr.AddComponent<Image>().color = new Color(0.2f, 0.7f, 0.2f, 0.5f);
        bdr.transform.SetAsFirstSibling();

        // Title
        MakeText(_panel, "CIRCUIT REPAIR", 18, FontStyle.Bold,
            new Color(0.3f,0.9f,0.4f), new Vector2(0.5f,1f), new Vector2(700f,34f),
            new Vector2(0f,-18f), TextAnchor.MiddleCenter);

        // Target display
        _targetText = MakeText(_panel, $"TARGET VOLTAGE: +{targetVoltage}", 14, FontStyle.Bold,
            ColTarget, new Vector2(0.5f,1f), new Vector2(700f,24f),
            new Vector2(0f,-52f), TextAnchor.MiddleCenter);
        _targetText.text = $"TARGET VOLTAGE:  {(targetVoltage >= 0 ? "+" : "")}{targetVoltage}";

        // Running total
        _totalText = MakeText(_panel, "Current: +0", 14, FontStyle.Bold,
            ColNeutral, new Vector2(0.5f,1f), new Vector2(700f,24f),
            new Vector2(0f,-76f), TextAnchor.MiddleCenter);

        // Wire line (decorative)
        var wire = MakeGO("Wire", _panel);
        var wrt  = wire.AddComponent<RectTransform>();
        wrt.anchorMin = wrt.anchorMax = new Vector2(0.5f, 0.5f);
        wrt.pivot     = new Vector2(0.5f, 0.5f);
        wrt.sizeDelta = new Vector2(680f, 4f);
        wrt.anchoredPosition = new Vector2(0f, 30f);
        wire.AddComponent<Image>().color = ColWire;

        // START label
        MakeText(_panel, "START", 11, FontStyle.Bold, ColWire,
            new Vector2(0f, 0.5f), new Vector2(60f, 24f),
            new Vector2(20f, 30f), TextAnchor.MiddleLeft);

        // END / target label
        MakeText(_panel, $"[ {(targetVoltage >= 0 ? "+" : "")}{targetVoltage} ]", 13, FontStyle.Bold,
            ColTarget, new Vector2(1f, 0.5f), new Vector2(60f, 24f),
            new Vector2(-20f, 30f), TextAnchor.MiddleRight);

        // Slot container — built dynamically in RebuildSlotUI
        var slotCont = MakeGO("SlotContainer", _panel);
        var scrt = slotCont.AddComponent<RectTransform>();
        scrt.anchorMin = scrt.anchorMax = new Vector2(0.5f, 0.5f);
        scrt.pivot     = new Vector2(0.5f, 0.5f);
        scrt.sizeDelta = new Vector2(680f, 90f);
        scrt.anchoredPosition = new Vector2(0f, 30f);
        slotCont.AddComponent<Image>().color = new Color(0,0,0,0);

        // Hand label
        MakeText(_panel, "AVAILABLE PIECES  (click to select, click slot to place, click placed to remove)",
            10, FontStyle.Normal, new Color(0.5f,0.8f,0.5f,0.8f),
            new Vector2(0.5f,0f), new Vector2(700f,20f),
            new Vector2(0f, 160f), TextAnchor.MiddleCenter);

        // Hand container
        _handContainer = MakeGO("HandContainer", _panel);
        var hcrt = _handContainer.AddComponent<RectTransform>();
        hcrt.anchorMin = hcrt.anchorMax = new Vector2(0.5f,0f);
        hcrt.pivot     = new Vector2(0.5f, 0f);
        hcrt.sizeDelta = new Vector2(680f, 80f);
        hcrt.anchoredPosition = new Vector2(0f, 80f);
        _handContainer.AddComponent<Image>().color = new Color(0,0,0,0);

        // Feedback
        var fbGO = MakeGO("Feedback", _panel);
        var fbrt = fbGO.AddComponent<RectTransform>();
        fbrt.anchorMin = fbrt.anchorMax = new Vector2(0.5f,0f);
        fbrt.pivot     = new Vector2(0.5f,0f);
        fbrt.sizeDelta = new Vector2(680f, 28f);
        fbrt.anchoredPosition = new Vector2(0f, 48f);
        _feedbackText = fbGO.AddComponent<Text>();
        _feedbackText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _feedbackText.fontSize  = 15; _feedbackText.fontStyle = FontStyle.Bold;
        _feedbackText.alignment = TextAnchor.MiddleCenter;
        _feedbackText.color     = Color.white;

        // Submit button
        var subGO = MakeGO("Submit", _panel);
        var srt   = subGO.AddComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f,0f);
        srt.pivot     = new Vector2(0.5f,0f);
        srt.sizeDelta = new Vector2(180f, 44f);
        srt.anchoredPosition = new Vector2(0f, 14f);
        subGO.AddComponent<Image>().color = new Color(0.15f, 0.4f, 0.15f);
        var subBtn = subGO.AddComponent<Button>();
        subBtn.targetGraphic = subGO.GetComponent<Image>();
        var sc = subBtn.colors; sc.highlightedColor = new Color(0.25f,0.6f,0.25f); subBtn.colors = sc;
        subBtn.onClick.AddListener(TrySubmit);
        MakeText(subGO, "SUBMIT", 14, FontStyle.Bold, Color.white,
            new Vector2(0.5f,0.5f), new Vector2(180f,44f), Vector2.zero, TextAnchor.MiddleCenter);

        // Hint
        MakeText(_panel, "Click placed piece to remove it   |   Esc — Exit",
            10, FontStyle.Normal, new Color(1f,1f,1f,0.3f),
            new Vector2(0.5f,0f), new Vector2(700f,20f),
            new Vector2(0f,5f), TextAnchor.MiddleCenter);
    }

    void RebuildSlotUI()
    {
        var slotCont = _panel.transform.Find("SlotContainer").gameObject;
        // Clear old
        foreach (Transform c in slotCont.transform) Destroy(c.gameObject);

        _slotImages  = new Image[slotCount];
        _slotTexts   = new Text[slotCount];
        _slotButtons = new Button[slotCount];

        float slotW  = Mathf.Min(90f, 660f / slotCount);
        float slotH  = 80f;
        float gap    = 8f;
        float totalW = slotCount * (slotW + gap) - gap;
        float startX = -totalW / 2f + slotW / 2f;

        for (int i = 0; i < slotCount; i++)
        {
            int idx = i;
            float cx = startX + i * (slotW + gap);

            var slotGO = MakeGO($"Slot{i}", slotCont);
            var srt    = slotGO.AddComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(slotW, slotH);
            srt.anchoredPosition = new Vector2(cx, 0f);
            var img = slotGO.AddComponent<Image>();
            img.color = ColSlotEmpty;
            _slotImages[i] = img;

            var btn = slotGO.AddComponent<Button>();
            btn.targetGraphic = img;
            var bc = btn.colors;
            bc.highlightedColor = ColSlotHover;
            btn.colors = bc;
            btn.onClick.AddListener(() => OnSlotClicked(idx));
            _slotButtons[i] = btn;

            var txt = MakeText(slotGO, "", 22, FontStyle.Bold, ColPositive,
                new Vector2(0.5f,0.5f), new Vector2(slotW, slotH),
                Vector2.zero, TextAnchor.MiddleCenter);
            _slotTexts[i] = txt;

            // Slot number
            MakeText(slotGO, (i+1).ToString(), 9, FontStyle.Normal,
                new Color(1f,1f,1f,0.3f), new Vector2(0f,1f), new Vector2(20f,16f),
                new Vector2(4f,-8f), TextAnchor.MiddleLeft);

            // Connector dots
            if (i < slotCount - 1)
            {
                var dot = MakeGO($"Dot{i}", slotCont);
                var drt = dot.AddComponent<RectTransform>();
                drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0.5f, 0.5f);
                drt.sizeDelta = new Vector2(8f, 8f);
                drt.anchoredPosition = new Vector2(cx + slotW/2f + gap/2f, 0f);
                dot.AddComponent<Image>().color = ColWire;
            }
        }
    }

    void RebuildHandUI()
    {
        foreach (Transform c in _handContainer.transform) Destroy(c.gameObject);
        _handImages.Clear();  _handTexts.Clear();  _handButtons.Clear();

        float pieceW = 68f, pieceH = 68f, gap = 8f;
        float totalW = PiecePool.Length * (pieceW + gap) - gap;
        float startX = -totalW / 2f + pieceW / 2f;

        for (int i = 0; i < PiecePool.Length; i++)
        {
            int idx = i;
            var pGO = MakeGO($"Piece{i}", _handContainer);
            var prt = pGO.AddComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(pieceW, pieceH);
            prt.anchoredPosition = new Vector2(startX + i * (pieceW + gap), 0f);
            var img = pGO.AddComponent<Image>();
            img.color = ColHandNormal;
            _handImages.Add(img);

            var btn = pGO.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => OnHandClicked(idx));
            _handButtons.Add(btn);

            int v = PiecePool[i];
            var txt = MakeText(pGO, (v>0?"+":"")+v, 22, FontStyle.Bold,
                v > 0 ? ColPositive : (v < 0 ? ColNegative : ColNeutral),
                new Vector2(0.5f,0.5f), new Vector2(pieceW, pieceH),
                Vector2.zero, TextAnchor.MiddleCenter);
            _handTexts.Add(txt);
        }
    }

    // ── OnGUI Prompt ──────────────────────────────────────────────

    void OnGUI()
    {
        if (_state != State.Idle) return;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject) return;

        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

        bool hasKit = !requireRepairKit || HasRepairKit();
        string msg = hasKit ? $"[{interactKey}]  Repair Circuit Board"
                            : "Need a Repair Kit";
        float pw = 440f, ph = 40f;
        float px = (Screen.width  - pw) / 2f;
        float py = (Screen.height - ph) / 2f + 60f;
        GUI.color = Color.black; GUI.Label(new Rect(px+2,py+2,pw,ph), msg, _promptStyle);
        GUI.color = hasKit ? Color.white : Color.red;
        GUI.Label(new Rect(px,py,pw,ph), msg, _promptStyle);
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
        rt.sizeDelta = sizeDelta; rt.anchoredPosition = pos;
        var t = go.GetComponent<Text>();
        t.text = text; t.fontSize = size; t.fontStyle = style;
        t.color = color; t.alignment = align;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }
}
