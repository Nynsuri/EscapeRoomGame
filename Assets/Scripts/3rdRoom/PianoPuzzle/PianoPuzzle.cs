using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// PianoPuzzle -- Unity 6000.3.9f1
///
/// Checks BarRoomState on open to decide which syphons and song to load:
///
///   HasSecretSyphons AND bottle not done -> SECRET mode (secret song, secret reward)
///   HasNormalSyphons                     -> NORMAL mode (normal song, normal reward)
///   Neither                              -> show locked message
///
/// Player clicks syphon cards to select, clicks slots to place.
/// All 4 in correct order -> music plays -> puzzle solved.
/// </summary>
public class PianoPuzzle : MonoBehaviour
{
    [Header("Interaction")]
    public float interactRange = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Normal Syphons")]
    public string[] normalSymphonyLabels = new string[] { "I", "II", "III", "IV" };
    public Color[] normalSymphonyColors = new Color[]
    {
        new Color(0.85f, 0.25f, 0.25f),
        new Color(0.25f, 0.55f, 0.85f),
        new Color(0.25f, 0.75f, 0.35f),
        new Color(0.85f, 0.75f, 0.20f),
    };
    [Tooltip("Which syphon index belongs in each slot for the normal solution")]
    public int[] normalCorrectOrder = new int[] { 0, 1, 2, 3 };
    public AudioClip normalMusicClip;

    [Header("Normal Reward")]
    public UnityEvent onNormalSolved;
    public GameObject normalRewardObject;

    [Header("Secret Syphons")]
    public string[] secretSymphonyLabels = new string[] { "A", "B", "C", "D" };
    public Color[] secretSymphonyColors = new Color[]
    {
        new Color(0.6f,  0.2f,  0.8f),
        new Color(0.2f,  0.7f,  0.8f),
        new Color(0.9f,  0.5f,  0.1f),
        new Color(0.4f,  0.9f,  0.5f),
    };
    [Tooltip("Which syphon index belongs in each slot for the secret solution")]
    public int[] secretCorrectOrder = new int[] { 3, 1, 0, 2 };
    public AudioClip secretMusicClip;

    [Header("Secret Reward")]
    public UnityEvent onSecretSolved;
    public GameObject secretRewardObject;

    [Header("Music")]
    public float musicVolume = 1f;

    // -- Runtime state ---------------------------------------------------------

    private bool _isOpen = false;
    private bool _solved = false;
    private bool _secretMode = false;

    private string[] _labels;
    private Color[] _colors;
    private int[] _correctOrder;

    private int[] _slots;    // slot i holds syphon index, -1 = empty
    private bool[] _placed;   // syphon i has been placed
    private int _selected = -1;

    // -- UI refs ---------------------------------------------------------------

    private GameObject _canvas;
    private GameObject _panel;
    private Button[] _slotButtons;
    private Image[] _slotImages;
    private Text[] _slotTexts;
    private Button[] _trayButtons;
    private Image[] _trayImages;
    private Text[] _trayTexts;
    private Text _statusText;
    private Text _modeText;
    private GUIStyle _promptStyle;

    // -- Cache -----------------------------------------------------------------

    private Camera _cam;
    private AudioSource _audio;

    // -- Unity -----------------------------------------------------------------

    void Start()
    {
        _cam = Camera.main ?? FindFirstObjectByType<Camera>();
        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();

        if (normalRewardObject != null) normalRewardObject.SetActive(false);
        if (secretRewardObject != null) secretRewardObject.SetActive(false);

        BuildUI();
        _panel.SetActive(false);
    }

    void Update()
    {
        if (_solved) return;

        if (_isOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseUI();
            return;
        }

        if (!IsLookingAtPiano()) return;
        if (Input.GetKeyDown(interactKey)) TryOpen();
    }

    // -- Interaction -----------------------------------------------------------

    bool IsLookingAtPiano()
    {
        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return false;
        Transform t = hit.collider.transform;
        while (t != null) { if (t.gameObject == gameObject) return true; t = t.parent; }
        return false;
    }

    void TryOpen()
    {
        // Decide mode based on current state
        if (BarRoomState.CanDoSecretPiano)
        {
            _secretMode = true;
            LoadSymphony(secretSymphonyLabels, secretSymphonyColors, secretCorrectOrder);
        }
        else if (BarRoomState.HasNormalSyphons)
        {
            _secretMode = false;
            LoadSymphony(normalSymphonyLabels, normalSymphonyColors, normalCorrectOrder);
        }
        else
        {
            // No syphons yet -- show locked message briefly and don't open
            ShowLockedMessage();
            return;
        }

        OpenUI();
    }

    void LoadSymphony(string[] labels, Color[] colors, int[] order)
    {
        _labels = labels;
        _colors = colors;
        _correctOrder = order;

        _slots = new int[] { -1, -1, -1, -1 };
        _placed = new bool[] { false, false, false, false };
        _selected = -1;

        RebuildTrayCards();
    }

    void OpenUI()
    {
        _isOpen = true;
        _panel.SetActive(true);

        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _modeText.text = _secretMode ? "SECRET SYMPHONY" : "ARRANGE THE SYPHONS";
        _modeText.color = _secretMode
            ? new Color(0.7f, 0.3f, 1f)
            : new Color(0.9f, 0.75f, 0.4f);

        RefreshUI();
    }

    void CloseUI()
    {
        _isOpen = false;
        _panel.SetActive(false);

        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _selected = -1;
    }

    // -- Puzzle logic ----------------------------------------------------------

    void OnTrayClick(int idx)
    {
        if (_placed[idx]) return;
        _selected = (_selected == idx) ? -1 : idx;
        RefreshUI();
    }

    void OnSlotClick(int slotIdx)
    {
        if (_selected == -1)
        {
            // Pick back up from slot
            if (_slots[slotIdx] != -1)
            {
                _placed[_slots[slotIdx]] = false;
                _slots[slotIdx] = -1;
                RefreshUI();
            }
            return;
        }

        // Swap out existing if needed
        if (_slots[slotIdx] != -1)
        {
            _placed[_slots[slotIdx]] = false;
            _slots[slotIdx] = -1;
        }

        _slots[slotIdx] = _selected;
        _placed[_selected] = true;
        _selected = -1;

        RefreshUI();
        CheckSolution();
    }

    void CheckSolution()
    {
        for (int i = 0; i < 4; i++)
            if (_slots[i] != _correctOrder[i]) return;

        _solved = true;
        SetStatus(_secretMode ? "The secret melody fills the room..." : "Correct! Beautiful...",
            _secretMode ? new Color(0.7f, 0.3f, 1f) : new Color(0.3f, 1f, 0.4f));

        BarRoomState.OnPianoSolved();
        StartCoroutine(SolveSequence());
    }

    IEnumerator SolveSequence()
    {
        yield return new WaitForSeconds(0.8f);

        var clip = _secretMode ? secretMusicClip : normalMusicClip;
        if (clip != null)
        {
            _audio.clip = clip;
            _audio.volume = musicVolume;
            _audio.loop = false;
            _audio.Play();
        }

        yield return new WaitForSeconds(1.5f);

        CloseUI();

        if (_secretMode)
        {
            onSecretSolved?.Invoke();
            if (secretRewardObject != null) secretRewardObject.SetActive(true);
        }
        else
        {
            onNormalSolved?.Invoke();
            if (normalRewardObject != null) normalRewardObject.SetActive(true);
        }
    }

    // -- Locked message --------------------------------------------------------

    private float _lockedTimer = 0f;

    void ShowLockedMessage()
    {
        _lockedTimer = 2.5f;
    }

    void OnGUI()
    {
        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

        if (_lockedTimer > 0f)
        {
            _lockedTimer -= Time.deltaTime;
            float pw = 520f, ph = 40f;
            float px = (Screen.width - pw) / 2f;
            float py = (Screen.height - ph) / 2f + 60f;
            _promptStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
            GUI.color = Color.black; GUI.Label(new Rect(px + 2, py + 2, pw, ph), "You have no syphons yet.", _promptStyle);
            GUI.color = Color.white; GUI.Label(new Rect(px, py, pw, ph), "You have no syphons yet.", _promptStyle);
            _promptStyle.normal.textColor = Color.white;
            return;
        }

        if (_isOpen || _solved) return;
        if (!IsLookingAtPiano()) return;

        string msg = $"[{interactKey}]  Inspect Piano";
        float w = 400f, h = 40f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f + 60f;
        GUI.color = Color.black; GUI.Label(new Rect(x + 2, y + 2, w, h), msg, _promptStyle);
        GUI.color = Color.white; GUI.Label(new Rect(x, y, w, h), msg, _promptStyle);
    }

    // -- UI Refresh ------------------------------------------------------------

    void RefreshUI()
    {
        int n = Mathf.Min(4, _labels.Length);

        for (int i = 0; i < n; i++)
        {
            bool placed = _placed[i];
            bool sel = (_selected == i);
            _trayImages[i].color = placed
                ? new Color(0.2f, 0.2f, 0.2f, 0.4f)
                : sel ? Color.Lerp(_colors[i], Color.white, 0.5f) : _colors[i];
            _trayTexts[i].color = placed ? new Color(0.4f, 0.4f, 0.4f) : Color.white;
            _trayButtons[i].interactable = !placed;
        }

        for (int i = 0; i < 4; i++)
        {
            int s = _slots[i];
            if (s == -1)
            {
                _slotImages[i].color = _selected != -1
                    ? new Color(0.4f, 0.6f, 0.4f, 0.6f)
                    : new Color(0.15f, 0.15f, 0.15f, 0.8f);
                _slotTexts[i].text = $"Slot {i + 1}";
                _slotTexts[i].color = new Color(0.5f, 0.5f, 0.5f);
            }
            else
            {
                _slotImages[i].color = _colors[s];
                _slotTexts[i].text = _labels[s];
                _slotTexts[i].color = Color.white;
            }
        }

        if (!_solved)
        {
            if (_selected != -1)
                SetStatus($"Place  \"{_labels[_selected]}\"  into a slot", Color.white);
            else
                SetStatus("Select a syphon, then click a slot  |  Click filled slot to remove",
                    new Color(0.8f, 0.8f, 0.8f));
        }
    }

    void SetStatus(string msg, Color col)
    {
        if (_statusText == null) return;
        _statusText.text = msg;
        _statusText.color = col;
    }

    // -- UI Builder ------------------------------------------------------------

    void RebuildTrayCards()
    {
        if (_trayButtons == null) return;
        int n = Mathf.Min(4, _labels.Length);
        for (int i = 0; i < n; i++)
        {
            _trayImages[i].color = _colors[i];
            _trayTexts[i].text = _labels[i];
        }
    }

    void BuildUI()
    {
        var cgo = new GameObject("PianoPuzzleCanvas");
        var cv = cgo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 40;
        cgo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        cgo.AddComponent<GraphicRaycaster>();
        _canvas = cgo;

        _panel = MakeGO("Panel", cgo);
        var panelRT = _panel.AddComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(680f, 440f);
        _panel.AddComponent<Image>().color = new Color(0.07f, 0.05f, 0.03f, 0.96f);

        // Top border line
        var line = MakeGO("Line", _panel);
        var lineRT = line.AddComponent<RectTransform>();
        lineRT.anchorMin = new Vector2(0f, 1f); lineRT.anchorMax = new Vector2(1f, 1f);
        lineRT.sizeDelta = new Vector2(0f, 2f); lineRT.anchoredPosition = new Vector2(0f, -56f);
        line.AddComponent<Image>().color = new Color(0.9f, 0.75f, 0.4f, 0.5f);

        // Title (mode-aware, updated at runtime)
        _modeText = AddText(_panel, "Title", "ARRANGE THE SYPHONS",
            20, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(600f, 44f),
            new Vector2(0f, -30f), new Color(0.9f, 0.75f, 0.4f), TextAnchor.MiddleCenter);

        // Slots label
        AddText(_panel, "SlotsLabel", "SLOTS  (correct order goes here)",
            13, FontStyle.Normal, new Vector2(0.5f, 1f), new Vector2(600f, 28f),
            new Vector2(0f, -78f), new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleCenter);

        _slotButtons = new Button[4];
        _slotImages = new Image[4];
        _slotTexts = new Text[4];

        float cardW = 120f, cardH = 90f, gap = 18f;
        float totalW = 4 * cardW + 3 * gap;
        float startX = -totalW / 2f + cardW / 2f;

        for (int i = 0; i < 4; i++)
        {
            float x = startX + i * (cardW + gap);
            var card = MakeGO($"Slot{i}", _panel);
            var rt = card.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(cardW, cardH);
            rt.anchoredPosition = new Vector2(x, -115f);
            var img = card.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            _slotImages[i] = img;
            var btn = card.AddComponent<Button>();
            btn.targetGraphic = img;
            int idx = i;
            btn.onClick.AddListener(() => OnSlotClick(idx));
            _slotButtons[i] = btn;
            AddText(card, "Num", $"{i + 1}", 11, FontStyle.Normal,
                new Vector2(0.5f, 1f), new Vector2(cardW, 20f), new Vector2(0f, -4f),
                new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleCenter);
            var lbl = AddText(card, "Lbl", $"Slot {i + 1}", 16, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(cardW - 8f, cardH - 24f),
                new Vector2(0f, -6f), new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleCenter);
            _slotTexts[i] = lbl;
        }

        // Tray divider
        var div2 = MakeGO("Div2", _panel);
        var div2RT = div2.AddComponent<RectTransform>();
        div2RT.anchorMin = new Vector2(0f, 1f); div2RT.anchorMax = new Vector2(1f, 1f);
        div2RT.sizeDelta = new Vector2(0f, 2f); div2RT.anchoredPosition = new Vector2(0f, -218f);
        div2.AddComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        // Tray label
        AddText(_panel, "TrayLabel", "SYPHONS  (click to select, then click a slot)",
            13, FontStyle.Normal, new Vector2(0.5f, 1f), new Vector2(600f, 28f),
            new Vector2(0f, -230f), new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleCenter);

        _trayButtons = new Button[4];
        _trayImages = new Image[4];
        _trayTexts = new Text[4];

        // Use placeholder colors/labels -- RebuildTrayCards() updates at runtime
        Color[] placeholderColors = normalSymphonyColors.Length >= 4 ? normalSymphonyColors
            : new Color[] { Color.red, Color.blue, Color.green, Color.yellow };
        string[] placeholderLabels = normalSymphonyLabels.Length >= 4 ? normalSymphonyLabels
            : new string[] { "I", "II", "III", "IV" };

        for (int i = 0; i < 4; i++)
        {
            float x = startX + i * (cardW + gap);
            var card = MakeGO($"Tray{i}", _panel);
            var rt = card.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(cardW, cardH);
            rt.anchoredPosition = new Vector2(x, -268f);
            var img = card.AddComponent<Image>();
            img.color = placeholderColors[i];
            _trayImages[i] = img;
            var btn = card.AddComponent<Button>();
            btn.targetGraphic = img;
            int idx = i;
            btn.onClick.AddListener(() => OnTrayClick(idx));
            _trayButtons[i] = btn;
            var lbl = AddText(card, "Lbl", placeholderLabels[i], 18, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(cardW - 8f, cardH - 8f),
                Vector2.zero, Color.white, TextAnchor.MiddleCenter);
            _trayTexts[i] = lbl;
        }

        // Status
        _statusText = AddText(_panel, "Status", "",
            14, FontStyle.Normal, new Vector2(0.5f, 0f), new Vector2(620f, 36f),
            new Vector2(0f, 18f), new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleCenter);

        // Close hint
        AddText(_panel, "Hint", "[Esc]  Close",
            12, FontStyle.Normal, new Vector2(1f, 0f), new Vector2(160f, 28f),
            new Vector2(-14f, 14f), new Color(0.4f, 0.4f, 0.4f), TextAnchor.MiddleRight);
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