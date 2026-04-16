using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ClockPuzzle.cs — Unity 6000.3.9f1
/// Clean bottom-bar UI, hands stay at initial position, drawer opens on solve.
/// </summary>
public class ClockPuzzle : BasePuzzle
{
    [Header("Camera")]
    public Camera playerCamera;
    public Transform puzzleCameraPosition;
    public float cameraZoomSpeed = 3f;

    [Header("Clock Hands")]
    public Transform hourHand;
    public Transform minuteHand;
    public Transform secondHand;
    [Tooltip("Axis the hands rotate around. Try Z for flat clock face, X for clock facing forward.")]
    public Vector3 rotationAxis = Vector3.forward;

    [Header("Hand Calibration (degrees offset to correct for model orientation)")]
    [Tooltip("Increase/decrease until hour hand sits at 12 when dials show 12:00")]
    public float hourOffset = 0f;
    [Tooltip("Increase/decrease until minute hand sits at 12 when dials show 00")]
    public float minuteOffset = 0f;
    [Tooltip("Increase/decrease until second hand sits at 12 when dials show 00")]
    public float secondOffset = 0f;
    [Tooltip("Flip rotation direction for individual hands if they go backwards")]
    public bool invertHour = false;
    public bool invertMinute = false;
    public bool invertSecond = false;

    [Header("Starting Time (hands will be set to this when puzzle opens)")]
    [Range(1, 12)] public int startHour = 12;
    [Range(0, 59)] public int startMinute = 0;
    [Range(0, 59)] public int startSecond = 0;

    [Header("Solution")]
    [Range(1, 12)] public int correctHour = 3;
    [Range(0, 59)] public int correctMinute = 45;
    [Range(0, 59)] public int correctSecond = 0;
    public float angleTolerance = 6f;

    [Header("Drawer to open on solve")]
    public Transform solveDrawer;
    public float drawerOpenDistance = 0.3f;
    public float drawerOpenSpeed = 1.2f;

    [Header("Interaction")]
    public float interactRange = 4f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Sound Effects")]
    public AudioClip solvedSound;
    public AudioClip drawerOpenSound;

    [Header("Audio Mixer")]
    public UnityEngine.Audio.AudioMixerGroup audioMixerGroup;

    [Header("On Solved (optional Unity Event)")]
    public UnityEngine.Events.UnityEvent onSolved;

    // ── State ─────────────────────────────────────────────────────
    private enum State { Idle, ZoomingIn, Active, ZoomingOut, Solved }
    private State _state = State.Idle;

    private Vector3 _camOrigPos;
    private Quaternion _camOrigRot;

    // Current dial values
    private int _hour = 12;
    private int _minute = 0;
    private int _second = 0;

    // Base X-angle of each hand when physically pointing at 12 (set by Calibrate button)
    [HideInInspector] public float baseHourX = 0f;
    [HideInInspector] public float baseMinuteX = 0f;
    [HideInInspector] public float baseSecondX = 0f;
    // Y and Z euler of each hand (never changes, preserves mesh orientation)
    [HideInInspector] public float baseHourY, baseHourZ;
    [HideInInspector] public float baseMinuteY, baseMinuteZ;
    [HideInInspector] public float baseSecondY, baseSecondZ;

    // UI
    private GameObject _panel;
    private Text _hourLabel, _minuteLabel, _secondLabel;
    private Text _feedbackText;
    private float _feedbackTimer;
    private RectTransform[] _dialRects = new RectTransform[3];

    // Detached camera children
    private struct DetachedChild { public Transform child, parent; public Vector3 pos; public Quaternion rot; }
    private List<DetachedChild> _detached = new List<DetachedChild>();

    private GUIStyle _promptStyle;

    // ── Awake ─────────────────────────────────────────────────────
    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main ?? FindFirstObjectByType<Camera>();

        // Set dials and hands to the configured start time
        _hour = startHour;
        _minute = startMinute;
        _second = startSecond;
        ApplyHandRotations();

        BuildUI();
        _panel.SetActive(false);
    }

    // ── Update ────────────────────────────────────────────────────
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
        if (puzzleCameraPosition == null) { _state = State.Active; _panel.SetActive(true); OpenPuzzle(); return; }

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
        if (Input.GetKeyDown(KeyCode.Escape)) { StartCoroutine(ExitPuzzle(false)); return; }

        // Scroll wheel on hovered dial
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            Vector2 mouse = Input.mousePosition;
            for (int i = 0; i < 3; i++)
            {
                if (_dialRects[i] != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(_dialRects[i], mouse))
                {
                    int delta = scroll > 0 ? 1 : -1;
                    AdjustDial(i, delta);
                    break;
                }
            }
        }
    }

    void AdjustDial(int idx, int delta)
    {
        if (idx == 0) { _hour = Mod(_hour + delta, 12); if (_hour == 0) _hour = 12; }
        if (idx == 1) _minute = Mod(_minute + delta, 60);
        if (idx == 2) _second = Mod(_second + delta, 60);
        RefreshLabels();
        ApplyHandRotations();
    }

    // ── Start / Exit ──────────────────────────────────────────────
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
            _detached.Add(new DetachedChild
            {
                child = c,
                parent = playerCamera.transform,
                pos = c.position,
                rot = c.rotation
            });
            c.SetParent(null, true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    IEnumerator ExitPuzzle(bool solved)
    {
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
        ClosePuzzle();
        _state = solved ? State.Solved : State.Idle;

        if (solved && solveDrawer != null)
            StartCoroutine(OpenDrawer());
    }

    IEnumerator OpenDrawer()
    {
        if (drawerOpenSound != null)
            AudioHelper.Play(drawerOpenSound, solveDrawer.position, audioMixerGroup);

        Vector3 closed = solveDrawer.localPosition;
        Vector3 open = closed + new Vector3(-drawerOpenDistance, 0f, 0f);
        float elapsed = 0f, dur = drawerOpenDistance / drawerOpenSpeed;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            solveDrawer.localPosition = Vector3.Lerp(closed, open,
                Mathf.SmoothStep(0f, 1f, elapsed / dur));
            yield return null;
        }
        solveDrawer.localPosition = open;
    }

    // ── Solution check ────────────────────────────────────────────
    void CheckSolution()
    {
        bool hOk = AngleDiff(HourAngle(_hour, _minute), HourAngle(correctHour, correctMinute)) <= angleTolerance;
        bool mOk = AngleDiff(MinuteAngle(_minute), MinuteAngle(correctMinute)) <= angleTolerance;
        bool sOk = AngleDiff(SecondAngle(_second), SecondAngle(correctSecond)) <= angleTolerance;

        if (hOk && mOk && sOk)
        {
            ShowFeedback("✓  Correct!", Color.green);
            if (solvedSound != null)
                AudioHelper.Play(solvedSound, transform.position, audioMixerGroup);
            onSolved?.Invoke();
            StartCoroutine(SolvedExit());
        }
        else
        {
            string msg = "✗  Incorrect —";
            if (!hOk) msg += "  hour";
            if (!mOk) msg += "  minute";
            if (!sOk) msg += "  second";
            ShowFeedback(msg, new Color(1f, 0.4f, 0.4f));
        }
    }

    IEnumerator SolvedExit()
    {
        yield return new WaitForSeconds(1.2f);
        yield return StartCoroutine(ExitPuzzle(true));
    }

    // ── Hand rotation ─────────────────────────────────────────────
    void ApplyHandRotations()
    {
        float hSign = invertHour ? 1f : -1f;
        float mSign = invertMinute ? 1f : -1f;
        float sSign = invertSecond ? 1f : -1f;

        // baseX = X angle when hand points at 12. Subtract time angle from it.
        float hDeg = baseHourX + hourOffset + hSign * HourAngle(_hour, _minute);
        float mDeg = baseMinuteX + minuteOffset + mSign * MinuteAngle(_minute);
        float sDeg = baseSecondX + secondOffset + sSign * SecondAngle(_second);

        if (hourHand) hourHand.localRotation = Quaternion.Euler(hDeg, baseHourY, baseHourZ);
        if (minuteHand) minuteHand.localRotation = Quaternion.Euler(mDeg, baseMinuteY, baseMinuteZ);
        if (secondHand) secondHand.localRotation = Quaternion.Euler(sDeg, baseSecondY, baseSecondZ);
    }

    public void EditorSaveBaseRotations()
    {
        if (hourHand)
        {
            Vector3 e = hourHand.localEulerAngles;
            baseHourX = e.x; baseHourY = e.y; baseHourZ = e.z;
        }
        if (minuteHand)
        {
            Vector3 e = minuteHand.localEulerAngles;
            baseMinuteX = e.x; baseMinuteY = e.y; baseMinuteZ = e.z;
        }
        if (secondHand)
        {
            Vector3 e = secondHand.localEulerAngles;
            baseSecondX = e.x; baseSecondY = e.y; baseSecondZ = e.z;
        }
        hourOffset = 0f; minuteOffset = 0f; secondOffset = 0f;
    }

    public void EditorPreviewStartTime()
    {
        _hour = startHour;
        _minute = startMinute;
        _second = startSecond;
        ApplyHandRotations();
    }



    static float HourAngle(int h, int m) => (h % 12) * 30f + m * 0.5f;
    static float MinuteAngle(int m) => m * 6f;
    static float SecondAngle(int s) => s * 6f;
    static float AngleDiff(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a, b));
    static int Mod(int x, int m) => ((x % m) + m) % m;

    // ── UI ────────────────────────────────────────────────────────
    void BuildUI()
    {
        var cgo = new GameObject("ClockPuzzleCanvas");
        var cv = cgo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 20;
        cgo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        cgo.AddComponent<GraphicRaycaster>();

        // Bottom bar panel
        _panel = MakeGO("ClockPanel", cgo);
        var panelRT = _panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0f, 0f);
        panelRT.anchorMax = new Vector2(1f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        panelRT.sizeDelta = new Vector2(0f, 180f);
        _panel.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 0.93f);

        // Top divider
        var line = MakeGO("Line", _panel);
        var lineRT = line.AddComponent<RectTransform>();
        lineRT.anchorMin = new Vector2(0f, 1f); lineRT.anchorMax = new Vector2(1f, 1f);
        lineRT.pivot = new Vector2(0.5f, 1f);
        lineRT.sizeDelta = new Vector2(0f, 2f);
        lineRT.anchoredPosition = Vector2.zero;
        line.AddComponent<Image>().color = new Color(0.6f, 0.5f, 0.3f, 0.6f);

        // Legend — anchored to left edge, fixed 180px wide, won't overlap dials
        var legendGO = MakeGO("Legend", _panel);
        var legendRT = legendGO.AddComponent<RectTransform>();
        legendRT.anchorMin = new Vector2(0f, 0f);
        legendRT.anchorMax = new Vector2(0f, 1f);
        legendRT.pivot = new Vector2(0f, 0.5f);
        legendRT.sizeDelta = new Vector2(180f, -16f);
        legendRT.anchoredPosition = new Vector2(12f, 0f);
        legendGO.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.10f, 0.9f);

        var legTitle = AddText(legendGO, "Title", "SET THE TIME", 15, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(160f, 26f), new Vector2(10f, -10f),
            new Color(0.8f, 0.68f, 0.4f), TextAnchor.MiddleLeft);
        legTitle.raycastTarget = false;

        var legBody = AddText(legendGO, "Body",
            "Hover a dial\nand scroll\nup/down\n\nHOUR  1-12\nMIN   0-59\nSEC   0-59",
            12, FontStyle.Normal,
            new Vector2(0f, 1f), new Vector2(160f, 130f), new Vector2(10f, -40f),
            new Color(1f, 1f, 1f, 0.7f), TextAnchor.UpperLeft);
        legBody.raycastTarget = false;

        // Three dials — truly centered on screen
        string[] names = { "HOUR", "MIN", "SEC" };
        float dialW = 120f, dialH = 150f, spacing = 20f;
        // Total width of 3 dials + 2 gaps = 3*120 + 2*20 = 400px
        // Center each dial: leftmost at -200+60=-140, middle at 0, rightmost at +140
        float[] dialCX = { -(dialW + spacing), 0f, (dialW + spacing) };

        for (int i = 0; i < 3; i++)
        {
            var dial = MakeGO("Dial" + i, _panel);
            var dialRT = dial.AddComponent<RectTransform>();
            dialRT.anchorMin = dialRT.anchorMax = new Vector2(0.5f, 0.5f);
            dialRT.pivot = new Vector2(0.5f, 0.5f);
            dialRT.sizeDelta = new Vector2(dialW, dialH);
            dialRT.anchoredPosition = new Vector2(dialCX[i], 0f);
            dial.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.17f, 1f);
            _dialRects[i] = dialRT;

            var lbl = AddText(dial, "Lbl", names[i], 13, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(dialW, 26f), new Vector2(0f, 0f),
                new Color(0.55f, 0.65f, 0.85f), TextAnchor.MiddleCenter);
            lbl.raycastTarget = false;

            Text valTxt = AddText(dial, "Val", i == 0 ? "12" : "00", 48, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(dialW - 4f, 70f), new Vector2(0f, -4f),
                Color.white, TextAnchor.MiddleCenter);
            valTxt.raycastTarget = false;
            if (i == 0) _hourLabel = valTxt;
            if (i == 1) _minuteLabel = valTxt;
            if (i == 2) _secondLabel = valTxt;

            var hint = AddText(dial, "Hint", "scroll up/down", 10, FontStyle.Normal,
                new Vector2(0.5f, 0f), new Vector2(dialW, 20f), new Vector2(0f, 0f),
                new Color(1f, 1f, 1f, 0.3f), TextAnchor.MiddleCenter);
            hint.raycastTarget = false;
        }

        // Check Time button — to the right of the rightmost dial
        float btnX = dialCX[2] + dialW / 2f + 20f + 60f;  // right edge of dial[2] + gap + half button width
        var subGO = MakeGO("Submit", _panel);
        var subRT = subGO.AddComponent<RectTransform>();
        subRT.anchorMin = subRT.anchorMax = new Vector2(0.5f, 0.5f);
        subRT.pivot = new Vector2(0.5f, 0.5f);
        subRT.sizeDelta = new Vector2(120f, 70f);
        subRT.anchoredPosition = new Vector2(btnX, 0f);
        subGO.AddComponent<Image>().color = new Color(0.10f, 0.36f, 0.12f);
        var subBtn = subGO.AddComponent<Button>();
        subBtn.targetGraphic = subGO.GetComponent<Image>();
        SetBtnColors(subBtn, new Color(0.16f, 0.52f, 0.20f));
        subBtn.onClick.AddListener(CheckSolution);
        var subTxt = AddText(subGO, "T", "CHECK\nTIME", 14, FontStyle.Bold,
            new Vector2(0.5f, 0.5f), new Vector2(120f, 70f), Vector2.zero,
            Color.white, TextAnchor.MiddleCenter);
        subTxt.raycastTarget = false;

        // Feedback text — just above panel top edge
        var fbGO = MakeGO("Feedback", _panel);
        var fbRT = fbGO.AddComponent<RectTransform>();
        fbRT.anchorMin = new Vector2(0f, 1f);
        fbRT.anchorMax = new Vector2(1f, 1f);
        fbRT.pivot = new Vector2(0.5f, 0f);
        fbRT.sizeDelta = new Vector2(0f, 26f);
        fbRT.anchoredPosition = new Vector2(0f, 4f);
        _feedbackText = fbGO.AddComponent<Text>();
        _feedbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _feedbackText.fontSize = 15;
        _feedbackText.fontStyle = FontStyle.Bold;
        _feedbackText.alignment = TextAnchor.MiddleCenter;
        _feedbackText.color = new Color(1f, 1f, 1f, 0f);
        _feedbackText.raycastTarget = false;

        RefreshLabels();
    }

    void RefreshLabels()
    {
        if (_hourLabel) _hourLabel.text = _hour.ToString("D2");
        if (_minuteLabel) _minuteLabel.text = _minute.ToString("D2");
        if (_secondLabel) _secondLabel.text = _second.ToString("D2");
    }

    void ShowFeedback(string msg, Color col)
    {
        if (_feedbackText == null) return;
        _feedbackText.text = msg;
        _feedbackText.color = col;
        _feedbackTimer = 3f;
    }

    // ── Prompt ────────────────────────────────────────────────────
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

        string msg = $"[{interactKey}]  Inspect Clock";
        float pw = 400f, ph = 40f;
        float px = (Screen.width - pw) / 2f;
        float py = (Screen.height - ph) / 2f + 60f;
        GUI.color = Color.black; GUI.Label(new Rect(px + 2, py + 2, pw, ph), msg, _promptStyle);
        GUI.color = Color.white; GUI.Label(new Rect(px, py, pw, ph), msg, _promptStyle);
    }

    // ── Static helpers ────────────────────────────────────────────
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

    static void SetBtnColors(Button btn, Color? highlight = null)
    {
        var c = btn.colors;
        c.highlightedColor = highlight ?? new Color(0.3f, 0.3f, 0.38f);
        c.pressedColor = new Color(0.1f, 0.1f, 0.14f);
        btn.colors = c;
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(ClockPuzzle))]
public class ClockPuzzleEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var cp = (ClockPuzzle)target;

        UnityEditor.EditorGUILayout.Space(10);
        UnityEditor.EditorGUILayout.HelpBox(
            "SETUP:\n1. Set all hands to X=0 in their Transform (pointing at 12).\n2. Click Calibrate to save that as the base.\n3. Set Start Time and click Preview to verify.\n4. Adjust Hour/Minute/Second Offset if slightly off.",
            UnityEditor.MessageType.Info);

        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button("🕛  Calibrate: Save current position as 12 o'clock", GUILayout.Height(34)))
        {
            cp.EditorSaveBaseRotations();
            cp.hourOffset = 0f;
            cp.minuteOffset = 0f;
            cp.secondOffset = 0f;
            UnityEditor.EditorUtility.SetDirty(cp);
            Debug.Log("[ClockPuzzle] Base rotations saved. Offsets zeroed.");
        }
        GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
        if (GUILayout.Button("🕐  Preview Start Time on Hands", GUILayout.Height(30)))
        {
            cp.EditorPreviewStartTime();
            UnityEditor.EditorUtility.SetDirty(cp);
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif