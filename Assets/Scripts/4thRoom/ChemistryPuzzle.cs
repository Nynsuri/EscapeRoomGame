using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// ChemistryPuzzle — Unity 6000.3.9f1
/// Attach to the lab table GameObject (must have a Collider).
///
/// When the player interacts (E), the camera zooms in and a full-screen UI
/// opens showing 6 chemicals.  The player picks two to combine.
/// Only the correct pair ("Hydrochloric Acid" + "Sulfonic Compound") produces
/// "Super Acid".  Wrong combos show a failure animation and reset.
///
/// INTERACTION: Mouse clicks on slots + Combine button, OR keyboard (1–6 + Enter).
/// Hover highlights on all interactive elements.
///
/// On success the Super Acid InventoryItem is spawned, added to the player's
/// Inventory, and the puzzle locks out (one-time solve).
///
/// Requires:
///   • SuperAcidItem prefab (InventoryItem subclass) assigned in Inspector
///   • Inventory component on the Player
///   • PlayerController component on the Player (disabled while puzzle is open)
/// </summary>
public class ChemistryPuzzle : BasePuzzle
{
    // ── Inspector ────────────────────────────────────────────────────────────────

    [Header("Camera")]
    public Camera playerCamera;
    public Transform puzzleCameraPosition;
    public float cameraZoomSpeed = 3f;

    [Header("Interaction")]
    public float interactRange = 4f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Reward")]
    [Tooltip("Prefab of the SuperAcidItem (must derive from InventoryItem)")]
    public InventoryItem superAcidPrefab;

    [Header("Sound Effects")]
    public AudioClip completedSound;

    [Header("Audio Mixer")]
    public UnityEngine.Audio.AudioMixerGroup audioMixerGroup;

    [Header("On Solved")]
    public UnityEngine.Events.UnityEvent onSolved;

    // ── Chemical data ────────────────────────────────────────────────────────────

    [System.Serializable]
    public struct Chemical
    {
        public string name;
        public string formula;
        public Color color;
    }

    private Chemical[] _chemicals = new Chemical[]
    {
        new Chemical { name = "Hydrochloric Acid",  formula = "HCl",         color = new Color(0.15f, 0.85f, 0.35f) },
        new Chemical { name = "Sulfonic Compound",   formula = "R-SO₃H",     color = new Color(0.90f, 0.55f, 0.10f) },
        new Chemical { name = "Sodium Bicarbonate",  formula = "NaHCO₃",     color = new Color(0.55f, 0.55f, 0.90f) },
        new Chemical { name = "Ethanol Solution",    formula = "C₂H₅OH",     color = new Color(0.70f, 0.85f, 0.95f) },
        new Chemical { name = "Potassium Nitrate",   formula = "KNO₃",       color = new Color(0.95f, 0.35f, 0.35f) },
        new Chemical { name = "Distilled Water",     formula = "H₂O",        color = new Color(0.40f, 0.75f, 0.95f) },
    };

    private const string CorrectA = "Hydrochloric Acid";
    private const string CorrectB = "Sulfonic Compound";

    // ── State machine ────────────────────────────────────────────────────────────

    private enum State { Idle, ZoomingIn, Active, ZoomingOut, Solved }
    private State _state = State.Idle;

    private Vector3 _camOrigPos;
    private Quaternion _camOrigRot;

    private struct DetachedChild { public Transform child; public Transform parent; public Vector3 pos; public Quaternion rot; }
    private List<DetachedChild> _detached = new List<DetachedChild>();

    // ── Selection ────────────────────────────────────────────────────────────────

    private int _selectedSlotA = -1;
    private int _selectedSlotB = -1;
    private bool _combining = false;

    // ── Hover tracking ───────────────────────────────────────────────────────────

    private int _hoveredSlot = -1;
    private bool _hoveredCombine = false;

    // ── UI references ────────────────────────────────────────────────────────────

    private GameObject _canvasGO;
    private GameObject _panel;
    private GameObject[] _slotGOs;
    private Image[] _slotBgs;
    private Image[] _slotInners;
    private Image[] _slotVials;
    private Text[] _slotNames;
    private Text[] _slotFormulas;
    private Image[] _slotBorders;

    private Text _titleText;
    private Text _hintText;
    private Text _statusText;
    private Text _feedbackText;
    private float _feedbackTimer;

    private GameObject _combineButtonGO;
    private Image _combineButtonBg;
    private Text _combineButtonText;

    private GUIStyle _promptStyle;

    // ── Colors ───────────────────────────────────────────────────────────────────

    private static readonly Color ColPanelBg = new Color(0.015f, 0.03f, 0.09f, 0.97f);
    private static readonly Color ColSlotDefault = new Color(0.06f, 0.11f, 0.18f);
    private static readonly Color ColSlotHovered = new Color(0.08f, 0.16f, 0.26f);
    private static readonly Color ColSlotSelected = new Color(0.06f, 0.25f, 0.45f);
    private static readonly Color ColBorderDefault = new Color(0.10f, 0.18f, 0.30f);
    private static readonly Color ColBorderHovered = new Color(0.15f, 0.40f, 0.65f);
    private static readonly Color ColBorderSelected = new Color(0f, 0.70f, 1f);
    private static readonly Color ColAccent = new Color(0f, 0.85f, 1f);
    private static readonly Color ColSuccess = new Color(0.2f, 1f, 0.5f);
    private static readonly Color ColFail = new Color(1f, 0.15f, 0.25f);
    private static readonly Color ColWarn = new Color(1f, 0.55f, 0.1f);

    // ─────────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main ?? FindFirstObjectByType<Camera>();

        EnsureEventSystem();
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
        if (_feedbackText != null)
            _feedbackText.color = new Color(
                _feedbackText.color.r, _feedbackText.color.g,
                _feedbackText.color.b, Mathf.Min(1f, _feedbackTimer));
    }

    // ── EventSystem ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Unity UI buttons / EventTriggers require an EventSystem in the scene.
    /// If one doesn't exist yet, create it automatically.
    /// </summary>
    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            DontDestroyOnLoad(esGO);
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }
    }

    // ── State: Idle ──────────────────────────────────────────────────────────────

    void UpdateIdle()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            if (hit.collider.gameObject == gameObject && Input.GetKeyDown(interactKey))
                StartPuzzle();
    }

    void OnGUI()
    {
        if (_state != State.Idle) return;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange) ||
            hit.collider.gameObject != gameObject) return;

        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.cyan }
            };

        string msg = $"[{interactKey}] EXAMINE LAB TABLE";
        float pw = 460f, ph = 50f;
        float px = (Screen.width - pw) / 2f;
        float py = (Screen.height - ph) / 2f + 140f;

        GUI.color = Color.black;
        GUI.Label(new Rect(px + 3, py + 3, pw, ph), msg, _promptStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(px, py, pw, ph), msg, _promptStyle);
    }

    // ── State: ZoomingIn ─────────────────────────────────────────────────────────

    void UpdateZoomIn()
    {
        if (puzzleCameraPosition == null) { ActivatePuzzle(); return; }

        playerCamera.transform.position = Vector3.Lerp(
            playerCamera.transform.position,
            puzzleCameraPosition.position,
            Time.deltaTime * cameraZoomSpeed);
        playerCamera.transform.rotation = Quaternion.Slerp(
            playerCamera.transform.rotation,
            puzzleCameraPosition.rotation,
            Time.deltaTime * cameraZoomSpeed);

        if (Vector3.Distance(playerCamera.transform.position, puzzleCameraPosition.position) < 0.01f)
            ActivatePuzzle();
    }

    void ActivatePuzzle()
    {
        if (puzzleCameraPosition != null)
            playerCamera.transform.SetPositionAndRotation(
                puzzleCameraPosition.position, puzzleCameraPosition.rotation);

        _state = State.Active;
        _panel.SetActive(true);
        ResetPuzzleState();
    }

    // ── State: Active ────────────────────────────────────────────────────────────

    void UpdateActive()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            StartCoroutine(ExitPuzzle(false));
            return;
        }

        // Keyboard shortcuts still work alongside mouse
        if (!_combining)
        {
            for (int i = 0; i < 6; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    ToggleSlot(i);
                    break;
                }
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                TryCombine();
        }
    }

    // ── Puzzle logic ─────────────────────────────────────────────────────────────

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

    void ResetPuzzleState()
    {
        _selectedSlotA = -1;
        _selectedSlotB = -1;
        _combining = false;
        _hoveredSlot = -1;
        _hoveredCombine = false;

        ShuffleChemicals();

        SetStatus("SELECT 2 CHEMICALS TO COMBINE", ColAccent);
        RefreshSlots();
        UpdateCombineButton();
    }

    void ShuffleChemicals()
    {
        for (int i = _chemicals.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var tmp = _chemicals[i];
            _chemicals[i] = _chemicals[j];
            _chemicals[j] = tmp;
        }
    }

    // ── Mouse click callbacks (assigned to Button.onClick) ───────────────────────

    void OnSlotClicked(int index)
    {
        if (_combining || _state != State.Active) return;
        ToggleSlot(index);
    }

    void OnCombineClicked()
    {
        if (_combining || _state != State.Active) return;
        TryCombine();
    }

    // ── Hover callbacks (assigned via EventTrigger) ──────────────────────────────

    void OnSlotEnter(int index)
    {
        _hoveredSlot = index;
        RefreshSlots();
    }

    void OnSlotExit(int index)
    {
        if (_hoveredSlot == index) _hoveredSlot = -1;
        RefreshSlots();
    }

    void OnCombineEnter()
    {
        _hoveredCombine = true;
        UpdateCombineButton();
    }

    void OnCombineExit()
    {
        _hoveredCombine = false;
        UpdateCombineButton();
    }

    // ── Selection logic ──────────────────────────────────────────────────────────

    void ToggleSlot(int index)
    {
        if (index < 0 || index >= 6) return;

        if (_selectedSlotA == index)
        {
            _selectedSlotA = _selectedSlotB;
            _selectedSlotB = -1;
        }
        else if (_selectedSlotB == index)
        {
            _selectedSlotB = -1;
        }
        else if (_selectedSlotA == -1)
        {
            _selectedSlotA = index;
        }
        else if (_selectedSlotB == -1)
        {
            _selectedSlotB = index;
        }
        else
        {
            _selectedSlotB = index;
        }

        RefreshSlots();
        UpdateCombineButton();
    }

    void TryCombine()
    {
        if (_selectedSlotA < 0 || _selectedSlotB < 0)
        {
            ShowFeedback("SELECT 2 CHEMICALS FIRST", ColWarn);
            return;
        }

        string nameA = _chemicals[_selectedSlotA].name;
        string nameB = _chemicals[_selectedSlotB].name;

        bool correct = (nameA == CorrectA && nameB == CorrectB) ||
                       (nameA == CorrectB && nameB == CorrectA);

        if (correct)
            StartCoroutine(CombineSuccess());
        else
            StartCoroutine(CombineFail(nameA, nameB));
    }

    IEnumerator CombineSuccess()
    {
        _combining = true;
        SetStatus("COMBINING...", ColWarn);
        ShowFeedback("REACTION IN PROGRESS", ColWarn);

        yield return StartCoroutine(FlashSlots(_selectedSlotA, _selectedSlotB, ColSuccess, 1.2f));

        SetStatus("SUPER ACID SYNTHESIZED", ColSuccess);
        ShowFeedback("SUPER ACID CREATED — ADDED TO INVENTORY", ColSuccess);

        yield return new WaitForSeconds(1.5f);

        SpawnAndAddReward();

        yield return StartCoroutine(ExitPuzzle(true));
    }

    IEnumerator CombineFail(string nameA, string nameB)
    {
        _combining = true;
        SetStatus("COMBINING...", ColWarn);

        yield return StartCoroutine(FlashSlots(_selectedSlotA, _selectedSlotB, ColFail, 0.8f));

        SetStatus("REACTION FAILED — TRY AGAIN", ColFail);
        ShowFeedback($"{nameA} + {nameB} = UNSTABLE MIXTURE", ColFail);

        yield return new WaitForSeconds(1.0f);

        _selectedSlotA = -1;
        _selectedSlotB = -1;
        _combining = false;

        SetStatus("SELECT 2 CHEMICALS TO COMBINE", ColAccent);
        RefreshSlots();
        UpdateCombineButton();
    }

    IEnumerator FlashSlots(int a, int b, Color flashColor, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.PingPong(elapsed * 6f, 1f);
            Color c = Color.Lerp(ColSlotSelected, flashColor, t);

            if (a >= 0 && a < 6) _slotBgs[a].color = c;
            if (b >= 0 && b < 6) _slotBgs[b].color = c;

            yield return null;
        }
    }

    void SpawnAndAddReward()
    {
        if (superAcidPrefab == null)
        {
            Debug.LogWarning("[ChemistryPuzzle] No superAcidPrefab assigned!");
            return;
        }

        var inv = FindFirstObjectByType<Inventory>();
        if (inv == null)
        {
            Debug.LogWarning("[ChemistryPuzzle] No Inventory found on player!");
            return;
        }

        InventoryItem item = Instantiate(superAcidPrefab, Vector3.zero, Quaternion.identity);
        if (!inv.AddItem(item))
        {
            Debug.LogWarning("[ChemistryPuzzle] Inventory full — couldn't add Super Acid!");
            Destroy(item.gameObject);
        }
    }

    IEnumerator ExitPuzzle(bool solved)
    {
        _state = State.ZoomingOut;
        _panel.SetActive(false);
        ClosePuzzle();

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
            if (completedSound != null)
                AudioHelper.Play(completedSound, transform.position, audioMixerGroup);
            onSolved?.Invoke();
        }
    }

    // ── UI Refresh ───────────────────────────────────────────────────────────────

    void RefreshSlots()
    {
        for (int i = 0; i < 6; i++)
        {
            bool selected = (i == _selectedSlotA || i == _selectedSlotB);
            bool hovered = (i == _hoveredSlot && !selected);

            // Slot background + inner fill
            Color bgCol = selected ? ColSlotSelected
                        : hovered ? ColSlotHovered
                        : ColSlotDefault;
            _slotBgs[i].color = bgCol;
            _slotInners[i].color = bgCol;

            // Border
            Color borderCol = selected ? ColBorderSelected
                            : hovered ? ColBorderHovered
                            : ColBorderDefault;
            _slotBorders[i].color = borderCol;

            // Text content
            _slotNames[i].text = _chemicals[i].name;
            _slotFormulas[i].text = _chemicals[i].formula;
            _slotNames[i].color = selected ? Color.white
                                   : hovered ? new Color(0.85f, 0.90f, 0.95f)
                                   : new Color(0.7f, 0.75f, 0.85f);
            _slotFormulas[i].color = _chemicals[i].color;

            // Vial colour indicator
            Color vialCol = _chemicals[i].color;
            if (!selected && !hovered) vialCol *= 0.55f;
            else if (hovered && !selected) vialCol *= 0.75f;
            vialCol.a = 1f;
            _slotVials[i].color = vialCol;
        }
    }

    void UpdateCombineButton()
    {
        bool ready = _selectedSlotA >= 0 && _selectedSlotB >= 0;

        if (ready && _hoveredCombine)
        {
            _combineButtonBg.color = new Color(0.08f, 0.32f, 0.55f);
            _combineButtonText.color = Color.white;
        }
        else if (ready)
        {
            _combineButtonBg.color = new Color(0.06f, 0.25f, 0.45f);
            _combineButtonText.color = ColAccent;
        }
        else if (_hoveredCombine)
        {
            _combineButtonBg.color = new Color(0.08f, 0.14f, 0.22f, 0.6f);
            _combineButtonText.color = new Color(0.4f, 0.5f, 0.6f);
        }
        else
        {
            _combineButtonBg.color = new Color(0.06f, 0.11f, 0.18f, 0.4f);
            _combineButtonText.color = new Color(0.3f, 0.4f, 0.5f);
        }

        _combineButtonText.text = ready ? "COMBINE" : "SELECT 2 CHEMICALS";
    }

    // ── Status / Feedback helpers ────────────────────────────────────────────────

    void SetStatus(string msg, Color col)
    {
        if (_statusText != null) { _statusText.text = msg; _statusText.color = col; }
    }

    void ShowFeedback(string msg, Color col)
    {
        if (_feedbackText != null)
        {
            _feedbackText.text = msg;
            _feedbackText.color = col;
            _feedbackTimer = 3f;
        }
    }

    // ── UI Builder ───────────────────────────────────────────────────────────────

    void BuildUI()
    {
        // ── Canvas ──────────────────────────────────────────────────────────────
        _canvasGO = new GameObject("ChemPuzzleCanvas");
        DontDestroyOnLoad(_canvasGO);
        var cv = _canvasGO.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 30;
        _canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        _canvasGO.AddComponent<GraphicRaycaster>();

        // ── Main panel ──────────────────────────────────────────────────────────
        _panel = MakeGO("ChemPanel", _canvasGO);
        var panelRT = _panel.AddComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(920f, 620f);
        _panel.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        _panel.AddComponent<Image>().color = ColPanelBg;

        // Top accent line
        var line = MakeGO("Line", _panel);
        var lineRT = line.AddComponent<RectTransform>();
        lineRT.anchorMin = new Vector2(0f, 1f);
        lineRT.anchorMax = new Vector2(1f, 1f);
        lineRT.sizeDelta = new Vector2(0f, 3f);
        lineRT.anchoredPosition = new Vector2(0f, -22f);
        line.AddComponent<Image>().color = new Color(0f, 0.7f, 1f, 0.7f);

        // Title
        _titleText = AddText(_panel, "Title", "// CHEMICAL SYNTHESIS LAB", 19, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(450f, 40f), new Vector2(40f, -48f),
            ColAccent, TextAnchor.MiddleLeft);
        _titleText.raycastTarget = false;

        // Hint (updated to mention mouse)
        _hintText = AddText(_panel, "Hint",
            "Click chemicals or press 1-6 to select  |  Click Combine or Enter  |  Esc to leave",
            12, FontStyle.Normal, new Vector2(0f, 1f), new Vector2(700f, 30f), new Vector2(40f, -78f),
            new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);
        _hintText.raycastTarget = false;

        // ── Chemical slot grid (2 rows x 3 cols) ────────────────────────────────

        _slotGOs = new GameObject[6];
        _slotBgs = new Image[6];
        _slotInners = new Image[6];
        _slotVials = new Image[6];
        _slotNames = new Text[6];
        _slotFormulas = new Text[6];
        _slotBorders = new Image[6];

        float slotW = 250f, slotH = 160f;
        float gapX = 18f, gapY = 18f;
        float gridStartY = 65f;

        for (int i = 0; i < 6; i++)
        {
            int col = i % 3;
            int row = i / 3;

            float totalW = 3 * slotW + 2 * gapX;
            float x = -totalW / 2f + col * (slotW + gapX) + slotW / 2f;
            float y = gridStartY - row * (slotH + gapY);

            // ── Slot root (receives clicks + hover) ─────────────────────────────
            var slot = MakeGO($"Slot{i}", _panel);
            _slotGOs[i] = slot;
            var rt = slot.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(slotW, slotH);
            rt.anchoredPosition = new Vector2(x, y);
            _slotBgs[i] = slot.AddComponent<Image>();
            _slotBgs[i].color = ColSlotDefault;

            // Button — makes the whole slot clickable
            var btn = slot.AddComponent<Button>();
            btn.targetGraphic = _slotBgs[i];
            btn.transition = Selectable.Transition.None; // we handle visuals ourselves
            int idx = i; // capture for lambda closures
            btn.onClick.AddListener(() => OnSlotClicked(idx));

            // EventTrigger — hover enter / exit
            AddHoverEvents(slot, () => OnSlotEnter(idx), () => OnSlotExit(idx));

            // ── Border (extends 2px beyond slot, drawn behind everything) ───────
            var border = MakeGO("Border", slot);
            var brt = border.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(-2f, -2f);
            brt.offsetMax = new Vector2(2f, 2f);
            brt.anchoredPosition = Vector2.zero;
            _slotBorders[i] = border.AddComponent<Image>();
            _slotBorders[i].color = ColBorderDefault;
            _slotBorders[i].raycastTarget = false;
            brt.SetAsFirstSibling();

            // ── Inner fill (so the border peeks around the edges) ───────────────
            var inner = MakeGO("Inner", slot);
            var irt = inner.AddComponent<RectTransform>();
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.sizeDelta = Vector2.zero;
            irt.anchoredPosition = Vector2.zero;
            _slotInners[i] = inner.AddComponent<Image>();
            _slotInners[i].color = ColSlotDefault;
            _slotInners[i].raycastTarget = false; // clicks pass through to parent Button

            // ── Number badge (top-left) ─────────────────────────────────────────
            var numTxt = AddText(slot, "Num", $"[{i + 1}]", 13, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(40f, 25f), new Vector2(12f, -8f),
                new Color(0.4f, 0.55f, 0.7f), TextAnchor.MiddleLeft);
            numTxt.raycastTarget = false;

            // ── Vial colour indicator ───────────────────────────────────────────
            var vial = MakeGO("Vial", slot);
            var vrt = vial.AddComponent<RectTransform>();
            vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(0.5f, 0.5f);
            vrt.sizeDelta = new Vector2(28f, 60f);
            vrt.anchoredPosition = new Vector2(0f, 10f);
            _slotVials[i] = vial.AddComponent<Image>();
            _slotVials[i].color = Color.green;
            _slotVials[i].raycastTarget = false;

            // ── Chemical name ───────────────────────────────────────────────────
            _slotNames[i] = AddText(slot, "Name", "", 14, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(slotW - 16f, 28f), new Vector2(0f, 38f),
                Color.white, TextAnchor.MiddleCenter);
            _slotNames[i].raycastTarget = false;

            // ── Formula ─────────────────────────────────────────────────────────
            _slotFormulas[i] = AddText(slot, "Formula", "", 12, FontStyle.Normal,
                new Vector2(0.5f, 0f), new Vector2(slotW - 16f, 22f), new Vector2(0f, 14f),
                ColAccent, TextAnchor.MiddleCenter);
            _slotFormulas[i].raycastTarget = false;
        }

        // ── Combine button (bottom centre) ──────────────────────────────────────

        _combineButtonGO = MakeGO("CombineBtn", _panel);
        var btnRT = _combineButtonGO.AddComponent<RectTransform>();
        btnRT.anchorMin = btnRT.anchorMax = btnRT.pivot = new Vector2(0.5f, 0f);
        btnRT.sizeDelta = new Vector2(320f, 48f);
        btnRT.anchoredPosition = new Vector2(0f, 55f);
        _combineButtonBg = _combineButtonGO.AddComponent<Image>();
        _combineButtonBg.color = new Color(0.06f, 0.11f, 0.18f, 0.4f);

        // Button component — clickable combine
        var combineBtn = _combineButtonGO.AddComponent<Button>();
        combineBtn.targetGraphic = _combineButtonBg;
        combineBtn.transition = Selectable.Transition.None;
        combineBtn.onClick.AddListener(OnCombineClicked);

        // Hover for combine button
        AddHoverEvents(_combineButtonGO, OnCombineEnter, OnCombineExit);

        _combineButtonText = AddText(_combineButtonGO, "BtnText", "SELECT 2 CHEMICALS", 16, FontStyle.Bold,
            new Vector2(0.5f, 0.5f), new Vector2(300f, 40f), Vector2.zero,
            new Color(0.3f, 0.4f, 0.5f), TextAnchor.MiddleCenter);
        _combineButtonText.raycastTarget = false;

        // ── Status text ─────────────────────────────────────────────────────────

        _statusText = AddText(_panel, "Status", "", 16, FontStyle.Bold,
            new Vector2(0.5f, 0f), new Vector2(800f, 36f), new Vector2(0f, 22f),
            ColAccent, TextAnchor.MiddleCenter);
        _statusText.raycastTarget = false;

        // ── Feedback text (fades out) ───────────────────────────────────────────

        var fbGO = MakeGO("Feedback", _panel);
        var fbRT = fbGO.AddComponent<RectTransform>();
        fbRT.anchorMin = new Vector2(0.5f, 0f);
        fbRT.anchorMax = new Vector2(0.5f, 0f);
        fbRT.sizeDelta = new Vector2(700f, 48f);
        fbRT.anchoredPosition = new Vector2(0f, -30f);
        _feedbackText = fbGO.AddComponent<Text>();
        _feedbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _feedbackText.fontSize = 22;
        _feedbackText.fontStyle = FontStyle.Bold;
        _feedbackText.alignment = TextAnchor.MiddleCenter;
        _feedbackText.color = Color.clear;
        _feedbackText.raycastTarget = false;
    }

    // ── Hover helper ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds PointerEnter / PointerExit callbacks to a GameObject via EventTrigger.
    /// This avoids needing a separate MonoBehaviour for each hoverable element.
    /// </summary>
    void AddHoverEvents(GameObject go, UnityEngine.Events.UnityAction onEnter, UnityEngine.Events.UnityAction onExit)
    {
        var trigger = go.AddComponent<EventTrigger>();

        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((_) => onEnter());
        trigger.triggers.Add(enterEntry);

        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((_) => onExit());
        trigger.triggers.Add(exitEntry);
    }

    // ── Utility ──────────────────────────────────────────────────────────────────

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