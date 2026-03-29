using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// EngineCalibratorPuzzle.cs — Unity 6000+
/// Matches ClockPuzzle / CableBoxPuzzle style. All UI built in code.
///
/// SETUP:
/// 1. Attach to the Screen Quad (needs a Collider).
/// 2. Assign playerCamera (or leave null — auto-finds Camera.main).
/// 3. Assign puzzleCameraPosition — empty Transform in front of screen, facing it.
/// 4. Assign uncalibratedMaterial (shown by default).
/// 5. Assign normalSolveMaterial   (shown on normal correct answer).
/// 6. Assign specialSolveMaterial  (shown on special correct answer).
/// 7. Set the 4 engine names in Inspector (e.g. "PORT BOW", "STARBOARD BOW"…).
/// 8. Set normalAnswer[0..3] — the standard correct values (can be negative).
/// 9. Set specialAnswer[0..3] — the secret correct values (can be negative).
///    Sliders range 0–100 but keyboard allows any integer including negatives.
/// 10. Player needs a PlayerController component (disabled during puzzle).
/// </summary>
public class EngineCalibratorPuzzle : MonoBehaviour
{
    [Header("Camera")]
    public Camera playerCamera;
    public Transform puzzleCameraPosition;
    public float cameraZoomSpeed = 3f;

    [Header("Materials")]
    public Material uncalibratedMaterial;   // default, shown before solve
    public Material normalSolveMaterial;    // shown after normal correct answer
    public Material specialSolveMaterial;   // shown after special correct answer

    [Header("Engine Names (4 engines)")]
    public string engineName0 = "ENGINE 1";
    public string engineName1 = "ENGINE 2";
    public string engineName2 = "ENGINE 3";
    public string engineName3 = "ENGINE 4";

    [Header("Normal Answer (standard solve → normalSolveMaterial)")]
    public int normalAnswer0 = 75;
    public int normalAnswer1 = 50;
    public int normalAnswer2 = 60;
    public int normalAnswer3 = 80;

    [Header("Special Answer (secret solve → specialSolveMaterial)")]
    public int specialAnswer0 = -10;
    public int specialAnswer1 = 100;
    public int specialAnswer2 = -25;
    public int specialAnswer3 = 33;

    [Header("Interaction")]
    public float interactRange = 4f;
    public KeyCode interactKey = KeyCode.E;

    [Header("On Solved (optional)")]
    public UnityEngine.Events.UnityEvent onNormalSolve;
    public UnityEngine.Events.UnityEvent onSpecialSolve;

    // ── State ──────────────────────────────────────────────────────────────
    private enum State { Idle, ZoomingIn, Active, ZoomingOut, Solved }
    private State _state = State.Idle;

    private Vector3 _camOrigPos;
    private Quaternion _camOrigRot;

    private struct DetachedChild { public Transform child, parent; public Vector3 pos; public Quaternion rot; }
    private List<DetachedChild> _detached = new List<DetachedChild>();

    // ── Engine values ──────────────────────────────────────────────────────
    // These are the live values — can go below 0 or above 100 via keyboard
    private int[] _values = new int[4];

    // Which engine slot is "focused" for keyboard input (-1 = none)
    private int _focused = -1;
    // Keyboard input buffer (user types digits, we parse on Enter / Tab / click-away)
    private string _inputBuffer = "";
    private bool _typing = false;

    // ── UI ─────────────────────────────────────────────────────────────────
    private GameObject _panel;
    private Slider[] _sliders = new Slider[4];
    private Text[] _valueTexts = new Text[4];
    private Text[] _nameTexts = new Text[4];
    private Image[] _focusHighlights = new Image[4];
    private Text _statusText;
    private Text _feedbackText;
    private float _feedbackTimer;
    private Text _typingText;

    private static readonly Color ColPanel = new Color(0.03f, 0.06f, 0.12f, 0.97f);
    private static readonly Color ColEngine = new Color(0.05f, 0.11f, 0.20f);
    private static readonly Color ColSliderBg = new Color(0.04f, 0.08f, 0.15f);
    private static readonly Color ColAccent = new Color(0f, 0.65f, 1f);
    private static readonly Color ColWarning = new Color(1f, 0.55f, 0.1f);
    private static readonly Color ColOk = new Color(0.2f, 1f, 0.5f);
    private static readonly Color ColBad = new Color(1f, 0.15f, 0.25f);

    private GUIStyle _promptStyle;
    private Renderer _renderer;

    // ── Awake ──────────────────────────────────────────────────────────────
    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (uncalibratedMaterial != null) _renderer.material = uncalibratedMaterial;

        if (playerCamera == null)
            playerCamera = Camera.main ?? FindFirstObjectByType<Camera>();

        // Default starting values all 0
        for (int i = 0; i < 4; i++) _values[i] = 0;

        BuildUI();
        _panel.SetActive(false);
    }

    // ── Update ─────────────────────────────────────────────────────────────
    void Update()
    {
        switch (_state)
        {
            case State.Idle: UpdateIdle(); break;
            case State.ZoomingIn: UpdateZoomIn(); break;
            case State.Active: UpdateActive(); break;
        }

        if (_feedbackTimer > 0f) _feedbackTimer -= Time.deltaTime;
        if (_feedbackText != null)
            _feedbackText.color = new Color(_feedbackText.color.r, _feedbackText.color.g,
                                            _feedbackText.color.b, Mathf.Min(1f, _feedbackTimer));
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
        if (puzzleCameraPosition == null) { _state = State.Active; _panel.SetActive(true); return; }

        playerCamera.transform.position = Vector3.Lerp(
            playerCamera.transform.position, puzzleCameraPosition.position, Time.deltaTime * cameraZoomSpeed);
        playerCamera.transform.rotation = Quaternion.Slerp(
            playerCamera.transform.rotation, puzzleCameraPosition.rotation, Time.deltaTime * cameraZoomSpeed);

        if (Vector3.Distance(playerCamera.transform.position, puzzleCameraPosition.position) < 0.01f)
        {
            playerCamera.transform.SetPositionAndRotation(puzzleCameraPosition.position, puzzleCameraPosition.rotation);
            _state = State.Active;
            _panel.SetActive(true);
        }
    }

    void UpdateActive()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CommitTyping(); // commit any in-progress value before exiting
            StartCoroutine(ExitPuzzle(false, false));
            return;
        }

        HandleKeyboardInput();
    }

    // ── Keyboard input ─────────────────────────────────────────────────────
    void HandleKeyboardInput()
    {
        if (_focused < 0) return;

        // Number keys + minus sign
        foreach (char c in Input.inputString)
        {
            if (c == '-' && _inputBuffer.Length == 0)
            {
                _inputBuffer = "-";
                _typing = true;
            }
            else if (char.IsDigit(c))
            {
                _inputBuffer += c;
                _typing = true;
            }
            else if (c == '\b' && _inputBuffer.Length > 0) // backspace
            {
                _inputBuffer = _inputBuffer.Substring(0, _inputBuffer.Length - 1);
            }
            else if (c == '\n' || c == '\r') // Enter — commit
            {
                CommitTyping();
                return;
            }
        }

        // Tab — commit and move to next engine
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            CommitTyping();
            _focused = (_focused + 1) % 4;
            SetFocus(_focused);
            return;
        }

        // Update typing preview text
        if (_typingText != null)
        {
            if (_typing && _inputBuffer.Length > 0)
                _typingText.text = $"ENGINE {_focused + 1} INPUT: {_inputBuffer}_";
            else
                _typingText.text = _focused >= 0 ? $"ENGINE {_focused + 1} SELECTED — TYPE VALUE, ENTER TO CONFIRM" : "";
        }

        // Update slider visually to clamp (but don't commit typed value until Enter)
        RefreshUI();
    }

    void CommitTyping()
    {
        if (_focused >= 0 && _typing && _inputBuffer.Length > 0)
        {
            if (int.TryParse(_inputBuffer, out int parsed))
                _values[_focused] = parsed;
        }
        _inputBuffer = "";
        _typing = false;
        RefreshUI();
        if (_typingText != null)
            _typingText.text = _focused >= 0 ? $"ENGINE {_focused + 1} SELECTED — TYPE VALUE, ENTER TO CONFIRM" : "";
    }

    // ── Start / Exit ────────────────────────────────────────────────────────
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

        // Reset values
        for (int i = 0; i < 4; i++) _values[i] = 0;
        _focused = -1;
        _inputBuffer = "";
        _typing = false;
        RefreshUI();
        UpdateFocusHighlights();
    }

    IEnumerator ExitPuzzle(bool solved, bool special)
    {
        _state = State.ZoomingOut;
        _panel.SetActive(false);
        _focused = -1;

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
            if (special)
            {
                if (specialSolveMaterial != null) _renderer.material = specialSolveMaterial;
                onSpecialSolve?.Invoke();
            }
            else
            {
                if (normalSolveMaterial != null) _renderer.material = normalSolveMaterial;
                onNormalSolve?.Invoke();
            }
        }
    }

    // ── Check solution ──────────────────────────────────────────────────────
    void CheckSolution()
    {
        CommitTyping();

        int[] answers = GetAnswers();
        int[] special = GetSpecialAnswers();

        bool isNormal = _values[0] == answers[0] && _values[1] == answers[1]
                      && _values[2] == answers[2] && _values[3] == answers[3];
        bool isSpecial = _values[0] == special[0] && _values[1] == special[1]
                      && _values[2] == special[2] && _values[3] == special[3];

        if (isSpecial)
        {
            ShowFeedback("SPECIAL CALIBRATION SEQUENCE ACCEPTED", ColOk);
            StartCoroutine(SolvedExit(true));
        }
        else if (isNormal)
        {
            ShowFeedback("ENGINES CALIBRATED SUCCESSFULLY", ColOk);
            StartCoroutine(SolvedExit(false));
        }
        else
        {
            ShowFeedback("CALIBRATION FAILED — INCORRECT VALUES", ColBad);
            StartCoroutine(WrongFlash());
        }
    }

    IEnumerator WrongFlash()
    {
        // Flash panel red briefly
        var panelImg = _panel.GetComponent<Image>();
        Color orig = panelImg.color;
        panelImg.color = new Color(0.2f, 0.03f, 0.05f, 0.97f);
        yield return new WaitForSeconds(0.4f);
        panelImg.color = orig;
    }

    IEnumerator SolvedExit(bool special)
    {
        yield return new WaitForSeconds(1.5f);
        yield return StartCoroutine(ExitPuzzle(true, special));
    }

    // ── Slider changed (called by slider OnValueChanged) ───────────────────
    void OnSliderChanged(int idx, float val)
    {
        if (_typing && _focused == idx) return; // keyboard takes priority
        _values[idx] = Mathf.RoundToInt(val);
        RefreshValueText(idx);
    }

    // ── Focus management ────────────────────────────────────────────────────
    void SetFocus(int idx)
    {
        CommitTyping();
        _focused = idx;
        UpdateFocusHighlights();
        if (_typingText != null)
            _typingText.text = idx >= 0 ? $"ENGINE {idx + 1} SELECTED — TYPE VALUE, ENTER TO CONFIRM" : "";
    }

    void UpdateFocusHighlights()
    {
        for (int i = 0; i < 4; i++)
        {
            if (_focusHighlights[i] != null)
                _focusHighlights[i].color = i == _focused
                    ? new Color(0f, 0.65f, 1f, 0.18f)
                    : new Color(0f, 0f, 0f, 0f);
        }
    }

    // ── UI refresh ──────────────────────────────────────────────────────────
    void RefreshUI()
    {
        for (int i = 0; i < 4; i++) RefreshValueText(i);
    }

    void RefreshValueText(int i)
    {
        // If this slot is being typed into, show buffer instead
        if (_typing && _focused == i && _inputBuffer.Length > 0)
        {
            if (_valueTexts[i] != null) _valueTexts[i].text = _inputBuffer + "_";
            return;
        }

        if (_valueTexts[i] != null) _valueTexts[i].text = _values[i].ToString();

        // Keep slider in 0-100 range visually (slider can't show negatives)
        if (_sliders[i] != null)
        {
            _sliders[i].onValueChanged.RemoveAllListeners();
            _sliders[i].value = Mathf.Clamp(_values[i], 0, 100);
            int idx = i;
            _sliders[i].onValueChanged.AddListener((v) => OnSliderChanged(idx, v));
        }
    }

    int[] GetAnswers() => new[] { normalAnswer0, normalAnswer1, normalAnswer2, normalAnswer3 };
    int[] GetSpecialAnswers() => new[] { specialAnswer0, specialAnswer1, specialAnswer2, specialAnswer3 };

    // ── Feedback ────────────────────────────────────────────────────────────
    void SetStatus(string msg, Color col)
    {
        if (_statusText == null) return;
        _statusText.text = msg;
        _statusText.color = col;
    }

    void ShowFeedback(string msg, Color col)
    {
        if (_feedbackText == null) return;
        _feedbackText.text = msg;
        _feedbackText.color = col;
        _feedbackTimer = 3f;
    }

    // ── OnGUI prompt ────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (_state != State.Idle) return;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
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

        string msg = $"[{interactKey}]  Calibrate Engines";
        float pw = 400f, ph = 40f;
        float px = (Screen.width - pw) / 2f;
        float py = (Screen.height - ph) / 2f + 60f;
        GUI.color = Color.black; GUI.Label(new Rect(px + 2, py + 2, pw, ph), msg, _promptStyle);
        GUI.color = Color.white; GUI.Label(new Rect(px, py, pw, ph), msg, _promptStyle);
    }

    // ── UI Builder ───────────────────────────────────────────────────────────
    void BuildUI()
    {
        string[] names = { engineName0, engineName1, engineName2, engineName3 };

        var cgo = new GameObject("EngineCalibratorCanvas");
        DontDestroyOnLoad(cgo);
        var cv = cgo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 20;
        cgo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        cgo.AddComponent<GraphicRaycaster>();

        // ── Centered panel — matches HackingPuzzle sizing convention ──
        // Panel is 980×480, scaled 1.2x so it fills nicely at 1080p
        _panel = MakeGO("EnginePanel", cgo);
        var panelRT = _panel.AddComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(980f, 480f);
        panelRT.anchoredPosition = Vector2.zero;
        _panel.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        _panel.AddComponent<Image>().color = ColPanel;

        // Top accent line
        var line = MakeGO("Line", _panel);
        var lineRT = line.AddComponent<RectTransform>();
        lineRT.anchorMin = new Vector2(0f, 1f); lineRT.anchorMax = new Vector2(1f, 1f);
        lineRT.pivot = new Vector2(0.5f, 1f);
        lineRT.sizeDelta = new Vector2(0f, 3f);
        lineRT.anchoredPosition = new Vector2(0f, -22f);
        line.AddComponent<Image>().color = new Color(1f, 0.55f, 0.1f, 0.9f);

        // Title
        AddText(_panel, "Title", "// ENGINE CALIBRATION SYSTEM", 19, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(600f, 40f), new Vector2(45f, -48f),
            ColWarning, TextAnchor.MiddleLeft);

        // Hint
        AddText(_panel, "Hint", "Sliders: 0–100  |  Click engine + type number (negatives ok)  |  Enter to confirm  |  Tab to cycle  |  Esc to abort", 12, FontStyle.Normal,
            new Vector2(0f, 1f), new Vector2(900f, 30f), new Vector2(45f, -82f),
            new Color(1f, 1f, 1f, 0.30f), TextAnchor.MiddleLeft);

        // Typing display (top-right)
        _typingText = AddText(_panel, "TypingHint", "", 13, FontStyle.Bold,
            new Vector2(1f, 1f), new Vector2(420f, 30f), new Vector2(-45f, -48f),
            new Color(0.4f, 0.85f, 1f), TextAnchor.MiddleRight);

        // ── 4 Engine columns ──────────────────────────────────────────────────
        // Panel is 980 wide. 4 cards × 180 + 3 gaps × 16 = 720 + 48 = 768 total
        // Centered: startX = -768/2 + 90 = -294
        float colW = 180f, colH = 270f, spacing = 16f;
        float totalW = 4 * colW + 3 * spacing;  // 768
        float startX = -totalW / 2f + colW / 2f; // -294
        float baseY = -30f; // vertical center of cards inside panel

        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            float cx = startX + i * (colW + spacing);

            // Engine card
            var card = MakeGO($"Engine{i}", _panel);
            var cardRT = card.AddComponent<RectTransform>();
            cardRT.anchorMin = cardRT.anchorMax = cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(colW, colH);
            cardRT.anchoredPosition = new Vector2(cx, baseY);
            card.AddComponent<Image>().color = ColEngine;

            // Focus highlight
            var hl = MakeGO("Highlight", card);
            var hlRT = hl.AddComponent<RectTransform>();
            hlRT.anchorMin = Vector2.zero; hlRT.anchorMax = Vector2.one;
            hlRT.offsetMin = hlRT.offsetMax = Vector2.zero;
            _focusHighlights[i] = hl.AddComponent<Image>();
            _focusHighlights[i].color = new Color(0f, 0f, 0f, 0f);

            // Engine name — top of card
            _nameTexts[i] = AddText(card, "Name", names[i], 12, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(colW - 10f, 28f), new Vector2(0f, -14f),
                ColAccent, TextAnchor.MiddleCenter);

            // Big value number
            _valueTexts[i] = AddText(card, "Value", "0", 36, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(colW - 10f, 56f), new Vector2(0f, -52f),
                Color.white, TextAnchor.MiddleCenter);

            // Slider — positioned in the lower half of the card
            var sliderGO = MakeGO($"Slider{i}", card);
            var sliderRT = sliderGO.AddComponent<RectTransform>();
            sliderRT.anchorMin = sliderRT.anchorMax = sliderRT.pivot = new Vector2(0.5f, 1f);
            sliderRT.sizeDelta = new Vector2(colW - 24f, 30f);
            sliderRT.anchoredPosition = new Vector2(0f, -120f);

            var slider = sliderGO.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.value = 0f;
            slider.direction = Slider.Direction.LeftToRight;

            // Slider bg
            var bgImg = MakeGO("Background", sliderGO);
            var bgRT = bgImg.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            bgImg.AddComponent<Image>().color = new Color(0.04f, 0.08f, 0.16f);
            slider.targetGraphic = bgImg.GetComponent<Image>();

            // Fill area
            var fillArea = MakeGO("Fill Area", sliderGO);
            var faRT = fillArea.AddComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0f, 0.25f); faRT.anchorMax = new Vector2(1f, 0.75f);
            faRT.offsetMin = new Vector2(6f, 0f); faRT.offsetMax = new Vector2(-6f, 0f);
            var fillGO = MakeGO("Fill", fillArea);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = ColAccent;
            slider.fillRect = fillRT;

            // Handle area
            var handleArea = MakeGO("Handle Slide Area", sliderGO);
            var haRT = handleArea.AddComponent<RectTransform>();
            haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
            haRT.offsetMin = new Vector2(12f, 0f); haRT.offsetMax = new Vector2(-12f, 0f);
            var handleGO = MakeGO("Handle", handleArea);
            var handleRT = handleGO.AddComponent<RectTransform>();
            handleRT.anchorMin = handleRT.anchorMax = new Vector2(0f, 0.5f);
            handleRT.pivot = new Vector2(0.5f, 0.5f);
            handleRT.sizeDelta = new Vector2(20f, 20f);
            var handleImg = handleGO.AddComponent<Image>();
            handleImg.color = Color.white;
            slider.handleRect = handleRT;

            slider.onValueChanged.AddListener((v) => OnSliderChanged(idx, v));
            _sliders[i] = slider;

            // Min / Max labels
            AddText(card, "Min", "0", 10, FontStyle.Normal,
                new Vector2(0f, 1f), new Vector2(30f, 18f), new Vector2(12f, -152f),
                new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);
            AddText(card, "Max", "100", 10, FontStyle.Normal,
                new Vector2(1f, 1f), new Vector2(40f, 18f), new Vector2(-12f, -152f),
                new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleRight);

            // Click-to-focus button
            var btn = card.AddComponent<Button>();
            btn.targetGraphic = card.GetComponent<Image>();
            var btnColors = btn.colors;
            btnColors.normalColor = ColEngine;
            btnColors.highlightedColor = new Color(0.07f, 0.16f, 0.28f);
            btnColors.pressedColor = new Color(0.04f, 0.10f, 0.18f);
            btn.colors = btnColors;
            btn.onClick.AddListener(() => SetFocus(idx));
        }

        // ── CONFIRM button — bottom center, below the cards ──
        var confirmGO = MakeGO("Confirm", _panel);
        var confirmRT = confirmGO.AddComponent<RectTransform>();
        confirmRT.anchorMin = confirmRT.anchorMax = confirmRT.pivot = new Vector2(0.5f, 0f);
        confirmRT.sizeDelta = new Vector2(200f, 54f);
        confirmRT.anchoredPosition = new Vector2(0f, 22f);
        confirmGO.AddComponent<Image>().color = new Color(0.05f, 0.22f, 0.08f);
        var confirmBtn = confirmGO.AddComponent<Button>();
        confirmBtn.targetGraphic = confirmGO.GetComponent<Image>();
        var cc = confirmBtn.colors;
        cc.highlightedColor = new Color(0.09f, 0.38f, 0.14f);
        cc.pressedColor = new Color(0.03f, 0.14f, 0.05f);
        confirmBtn.colors = cc;
        confirmBtn.onClick.AddListener(CheckSolution);
        AddText(confirmGO, "T", "CALIBRATE ENGINES", 14, FontStyle.Bold,
            new Vector2(0.5f, 0.5f), new Vector2(200f, 54f), Vector2.zero,
            Color.white, TextAnchor.MiddleCenter);

        // ── Status text — just above confirm ──
        _statusText = AddText(_panel, "Status", "", 16, FontStyle.Bold,
            new Vector2(0.5f, 0f), new Vector2(880f, 36f), new Vector2(0f, 82f),
            new Color(0.45f, 0.9f, 1f), TextAnchor.MiddleCenter);

        // ── Feedback text (fades out) ──
        var fbGO = MakeGO("Feedback", _panel);
        var fbRT = fbGO.AddComponent<RectTransform>();
        fbRT.anchorMin = new Vector2(0.5f, 1f); fbRT.anchorMax = new Vector2(0.5f, 1f);
        fbRT.pivot = new Vector2(0.5f, 1f);
        fbRT.sizeDelta = new Vector2(880f, 40f);
        fbRT.anchoredPosition = new Vector2(0f, -108f);
        _feedbackText = fbGO.AddComponent<Text>();
        _feedbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _feedbackText.fontSize = 22;
        _feedbackText.fontStyle = FontStyle.Bold;
        _feedbackText.alignment = TextAnchor.MiddleCenter;
        _feedbackText.color = Color.clear;
    }

    // ── Static helpers (identical to ClockPuzzle / HackingPuzzle) ────────────
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
        rt.sizeDelta = sizeDelta; rt.anchoredPosition = pos;
        var t = go.GetComponent<Text>();
        t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color; t.alignment = align;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }
}