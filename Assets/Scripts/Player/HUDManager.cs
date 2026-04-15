using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    [Header("Crosshair")]
    public float crosshairArmLength = 9f;
    public float crosshairThickness = 2f;
    public float crosshairGap = 5f;
    public float crosshairDotSize = 3f;
    public Color crosshairColor = new Color(1f, 1f, 1f, 0.90f);
    public Color crosshairShadowColor = new Color(0f, 0f, 0f, 0.45f);

    [Header("Item Hints Panel")]
    [Tooltip("Fade-out duration (seconds) when no item is held")]
    public float hintsFadeDuration = 0.6f;
    public Color hintsBgColor = new Color(0f, 0f, 0f, 0.55f);
    public Color hintsTextColor = new Color(0.93f, 0.93f, 0.93f, 1f);
    public Color keyBadgeColor = new Color(0.18f, 0.18f, 0.18f, 0.92f);
    public Color keyBadgeBorderColor = new Color(0.70f, 0.65f, 0.25f, 1f);

    private static HUDManager _instance;

    private Canvas _canvas;
    private GameObject _crosshairRoot;
    private GameObject _hintsRoot;
    private CanvasGroup _hintsGroup;
    private RectTransform _hintsRT;

    private Inventory _inventory;
    private PlayerController _player;

    private Coroutine _fadeCoroutine;
    private bool _hintsVisible;
    private float _popScale = 1f;

    private static readonly (string key, string desc)[] HintsEmpty =
        System.Array.Empty<(string, string)>();

    private (string key, string desc)[] _currentHints = HintsEmpty;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        _inventory = GetComponent<Inventory>();
        _player = GetComponent<PlayerController>();

        BuildCanvas();

        if (_inventory != null)
        {
            _inventory.OnSelectionChanged += OnSelectionChanged;
            _inventory.OnInventoryChanged += OnInventoryChanged;
        }

        _hintsGroup.alpha = 0f;
        _hintsVisible = false;
        _hintsRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;

        if (_inventory != null)
        {
            _inventory.OnSelectionChanged -= OnSelectionChanged;
            _inventory.OnInventoryChanged -= OnInventoryChanged;
        }

        if (_canvas != null)
            Destroy(_canvas.gameObject);
    }

    void Update()
    {
        if (_hintsRoot.activeSelf && Mathf.Abs(_popScale - 1f) > 0.001f)
        {
            _popScale = Mathf.Lerp(_popScale, 1f, Time.deltaTime * 14f);
            _hintsRoot.transform.localScale = Vector3.one * _popScale;
        }
    }

    void OnSelectionChanged(int index) => RefreshForIndex(index);
    void OnInventoryChanged() => RefreshForIndex(_inventory.SelectedIndex);

    void RefreshForIndex(int index)
    {
        if (_inventory == null || index < 0 || index >= _inventory.Items.Count)
        {
            ShowHints(HintsEmpty, fadeOut: true);
            return;
        }

        var item = _inventory.Items[index];
        (string key, string desc)[] hints;

        if (item is GunItem gun)
        {
            hints = new[]
            {
                (KeyLabel(gun.shootKey), "Shoot"),
            };
        }
        else if (item is TorchItem torch)
        {
            hints = new[]
            {
                (KeyLabel(torch.toggleKey), "Toggle flashlight"),
            };
        }
        else if (item is KeycardItem keycard)
        {
            hints = new[]
            {
                (KeyLabel(keycard.useKey), "Use KeyCard"),
            };
        }
        else if (item is SuperAcidItem superAcid)
        {
            hints = new[]
            {
                (KeyLabel(superAcid.useKey), "Use SuperAcid"),
            };
        }
        else
        {
            hints = string.IsNullOrWhiteSpace(item.description)
                ? HintsEmpty
                : new[] { ("E", item.description) };
        }

        ShowHints(hints, fadeOut: hints.Length == 0);
    }

    void ShowHints((string key, string desc)[] hints, bool fadeOut)
    {
        _currentHints = hints;

        if (_fadeCoroutine != null) { StopCoroutine(_fadeCoroutine); _fadeCoroutine = null; }

        if (fadeOut || hints == null || hints.Length == 0)
        {
            if (_hintsVisible) _fadeCoroutine = StartCoroutine(FadeOut());
            return;
        }

        RebuildHintsContent();
        _hintsRoot.SetActive(true);
        _hintsGroup.alpha = 1f;
        _hintsVisible = true;
        _popScale = 1.18f;
    }

    IEnumerator FadeOut()
    {
        float start = _hintsGroup.alpha;
        float t = 0f;
        while (t < hintsFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _hintsGroup.alpha = Mathf.Lerp(start, 0f, t / hintsFadeDuration);
            yield return null;
        }
        _hintsGroup.alpha = 0f;
        _hintsVisible = false;
        _hintsRoot.SetActive(false);
        _fadeCoroutine = null;
    }

    void RebuildHintsContent()
    {
        for (int i = _hintsRoot.transform.childCount - 1; i >= 0; i--)
            Destroy(_hintsRoot.transform.GetChild(i).gameObject);

        if (_currentHints == null || _currentHints.Length == 0) return;

        const float rowH = 22f;
        const float panelW = 175f;
        const float pad = 8f;

        string title = "Item";
        if (_inventory != null)
        {
            int idx = _inventory.SelectedIndex;
            if (idx >= 0 && idx < _inventory.Items.Count)
                title = _inventory.Items[idx].itemName;
        }

        float y = 0f;
        y = AddTitle(_hintsRoot, title, panelW, y);
        y -= 2f;

        foreach (var (key, desc) in _currentHints)
        {
            if (string.IsNullOrEmpty(key)) { y -= 4f; continue; }
            y = AddHintRow(_hintsRoot, key, desc, panelW, y, rowH);
        }

        float totalH = -y + pad;

        var bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(_hintsRoot.transform, false);
        bg.transform.SetAsFirstSibling();
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = hintsBgColor;
        bgImg.raycastTarget = false;
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = bgRT.pivot = new Vector2(0f, 1f);
        bgRT.anchorMax = new Vector2(0f, 1f);
        bgRT.sizeDelta = new Vector2(panelW + pad * 2f, totalH);
        bgRT.anchoredPosition = new Vector2(-pad, pad);

        _hintsRT.sizeDelta = new Vector2(panelW, totalH);
    }

    float AddTitle(GameObject parent, string text, float width, float yOffset)
    {
        var go = new GameObject("Title", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent.transform, false);
        SetTL(go.GetComponent<RectTransform>(), width, 18f, 0f, yOffset);
        var t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = text.ToUpper();
        t.fontSize = 10;
        t.fontStyle = FontStyle.Bold;
        t.color = new Color(keyBadgeBorderColor.r, keyBadgeBorderColor.g,
                                keyBadgeBorderColor.b, 0.85f);
        t.alignment = TextAnchor.MiddleLeft;
        t.raycastTarget = false;
        return yOffset - 20f;
    }

    float AddHintRow(GameObject parent, string key, string desc,
                     float width, float yOffset, float rowH)
    {
        const float badgeW = 40f;
        const float gap = 6f;

        MakeImage(parent, "Border_" + key, keyBadgeBorderColor,
                  badgeW + 2f, rowH, -1f, yOffset + 1f).raycastTarget = false;

        MakeImage(parent, "Badge_" + key, keyBadgeColor,
                  badgeW, rowH - 2f, 0f, yOffset).raycastTarget = false;

        var keyGO = new GameObject("Key_" + key, typeof(RectTransform), typeof(Text));
        keyGO.transform.SetParent(parent.transform, false);
        SetTL(keyGO.GetComponent<RectTransform>(), badgeW, rowH - 2f, 0f, yOffset);
        var kt = keyGO.GetComponent<Text>();
        kt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        kt.text = key; kt.fontSize = 10; kt.fontStyle = FontStyle.Bold;
        kt.color = Color.white; kt.alignment = TextAnchor.MiddleCenter;
        kt.raycastTarget = false;

        var descGO = new GameObject("Desc_" + key, typeof(RectTransform), typeof(Text));
        descGO.transform.SetParent(parent.transform, false);
        SetTL(descGO.GetComponent<RectTransform>(), width - badgeW - gap, rowH - 2f,
              badgeW + gap, yOffset);
        var dt = descGO.GetComponent<Text>();
        dt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        dt.text = desc; dt.fontSize = 10;
        dt.color = hintsTextColor; dt.alignment = TextAnchor.MiddleLeft;
        dt.raycastTarget = false;

        return yOffset - rowH;
    }

    Image MakeImage(GameObject parent, string name, Color color,
                    float w, float h, float x, float y)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        SetTL(go.GetComponent<RectTransform>(), w, h, x, y);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static void SetTL(RectTransform rt, float w, float h, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    void BuildCanvas()
    {
        var existing = GameObject.Find("HUDCanvas");
        if (existing != null)
            Destroy(existing);

        var go = new GameObject("HUDCanvas");
        DontDestroyOnLoad(go);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 10;
        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        go.AddComponent<GraphicRaycaster>();

        BuildCrosshair(go);
        BuildHintsPanelShell(go);
    }

    void BuildCrosshair(GameObject canvasGO)
    {
        _crosshairRoot = new GameObject("Crosshair");
        _crosshairRoot.transform.SetParent(canvasGO.transform, false);

        if (crosshairDotSize > 0f)
            CrosshairPiece("Dot",
                new Vector2(crosshairDotSize * 2f, crosshairDotSize * 2f), Vector2.zero);

        float h = crosshairGap + crosshairArmLength * 0.5f;
        CrosshairPiece("L", new Vector2(crosshairArmLength, crosshairThickness), new Vector2(-h, 0f));
        CrosshairPiece("R", new Vector2(crosshairArmLength, crosshairThickness), new Vector2(h, 0f));
        CrosshairPiece("U", new Vector2(crosshairThickness, crosshairArmLength), new Vector2(0f, h));
        CrosshairPiece("D", new Vector2(crosshairThickness, crosshairArmLength), new Vector2(0f, -h));
    }

    void CrosshairPiece(string name, Vector2 size, Vector2 pos)
    {
        var s = MakeCentredImage(_crosshairRoot, name + "_S", crosshairShadowColor,
                                 size, pos + new Vector2(1f, -1f));
        s.raycastTarget = false;

        var f = MakeCentredImage(_crosshairRoot, name, crosshairColor, size, pos);
        f.raycastTarget = false;
    }

    static Image MakeCentredImage(GameObject parent, string name, Color color,
                                  Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    void BuildHintsPanelShell(GameObject canvasGO)
    {
        _hintsRoot = new GameObject("ItemHints");
        _hintsRoot.transform.SetParent(canvasGO.transform, false);
        _hintsGroup = _hintsRoot.AddComponent<CanvasGroup>();
        _hintsRT = _hintsRoot.AddComponent<RectTransform>();
        _hintsRT.anchorMin = _hintsRT.pivot = Vector2.zero;
        _hintsRT.anchorMax = Vector2.zero;
        _hintsRT.anchoredPosition = new Vector2(18f, 18f);
    }

    static string KeyLabel(KeyCode key) => key switch
    {
        KeyCode.Mouse0 => "LMB",
        KeyCode.Mouse1 => "RMB",
        KeyCode.Mouse2 => "MMB",
        KeyCode.LeftShift => "Shift",
        KeyCode.RightShift => "Shift",
        KeyCode.LeftControl => "Ctrl",
        KeyCode.RightControl => "Ctrl",
        KeyCode.LeftAlt => "Alt",
        KeyCode.RightAlt => "Alt",
        KeyCode.Return => "Enter",
        KeyCode.Backspace => "Bksp",
        KeyCode.Space => "Space",
        KeyCode.Tab => "Tab",
        KeyCode.Escape => "Esc",
        _ => key.ToString().Replace("Alpha", "").Replace("Keypad", "KP"),
    };
}