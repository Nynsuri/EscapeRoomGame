using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HeldItemDisplay.cs — attach to the Player GameObject.
/// Shows the currently selected inventory item in the bottom-right corner,
/// Minecraft-style: a box with the item icon and name beneath it.
///
/// Fully self-building — no prefab or Canvas setup needed.
/// </summary>
public class HeldItemDisplay : MonoBehaviour
{
    [Header("Display Settings")]
    [Tooltip("Size of the item box in pixels")]
    public Vector2 boxSize = new Vector2(90f, 90f);
    [Tooltip("Padding from screen edge")]
    public Vector2 screenPadding = new Vector2(20f, 20f);
    [Tooltip("Background color of the box")]
    public Color boxColor = new Color(0.08f, 0.08f, 0.08f, 0.82f);
    [Tooltip("Border/highlight color")]
    public Color borderColor = new Color(0.9f, 0.8f, 0.3f, 1f);
    [Tooltip("How fast the box bobs in and out when item changes")]
    public float popAnimSpeed = 8f;

    private Inventory _inventory;

    // UI elements
    private Canvas    _canvas;
    private GameObject _root;
    private Image     _border;
    private Image     _background;
    private Image     _icon;
    private Text      _nameLabel;

    // Pop animation
    private float _popScale = 1f;
    private float _popTarget = 1f;

    // ────────────────────────────────────────────────────────────

    void Awake()
    {
        _inventory = GetComponent<Inventory>();
        if (_inventory == null)
        {
            Debug.LogError("[HeldItemDisplay] No Inventory on this GameObject!");
            return;
        }

        BuildUI();

        _inventory.OnSelectionChanged += _ => TriggerPop();
        _inventory.OnInventoryChanged += Refresh;
    }

    void OnDestroy()
    {
        if (_inventory == null) return;
        _inventory.OnSelectionChanged -= _ => TriggerPop();
        _inventory.OnInventoryChanged -= Refresh;
    }

    // ── Build UI ─────────────────────────────────────────────────

    void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("HeldItemCanvas");
        DontDestroyOnLoad(canvasGO);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Root anchored to bottom-right
        _root = new GameObject("HeldItemRoot", typeof(RectTransform));
        _root.transform.SetParent(canvasGO.transform, false);
        var rootRT = _root.GetComponent<RectTransform>();
        rootRT.anchorMin = rootRT.anchorMax = rootRT.pivot = new Vector2(1f, 0f);
        rootRT.anchoredPosition = new Vector2(-screenPadding.x, screenPadding.y);
        float totalH = boxSize.y + 26f;
        rootRT.sizeDelta = new Vector2(boxSize.x + 8f, totalH);

        // Border (slightly larger than background)
        _border = MakeImage(_root, "Border", borderColor,
                            new Vector2(0.5f, 1f),
                            new Vector2(boxSize.x + 8f, boxSize.y + 8f),
                            new Vector2(0f, 0f));

        // Background box
        _background = MakeImage(_root, "Background", boxColor,
                                new Vector2(0.5f, 1f),
                                boxSize,
                                new Vector2(0f, -4f));

        // Icon inside box
        _icon = MakeImage(_root, "Icon", Color.white,
                          new Vector2(0.5f, 1f),
                          boxSize - new Vector2(14f, 14f),
                          new Vector2(0f, -11f));
        _icon.preserveAspect = true;
        _icon.color = new Color(1, 1, 1, 0);

        // Name label below box
        var labelGO = new GameObject("NameLabel", typeof(RectTransform), typeof(Text));
        labelGO.transform.SetParent(_root.transform, false);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = labelRT.anchorMax = new Vector2(0.5f, 1f);
        labelRT.pivot     = new Vector2(0.5f, 1f);
        labelRT.sizeDelta = new Vector2(boxSize.x + 30f, 24f);
        labelRT.anchoredPosition = new Vector2(0f, -(boxSize.y + 10f));
        _nameLabel = labelGO.GetComponent<Text>();
        _nameLabel.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _nameLabel.fontSize  = 13;
        _nameLabel.fontStyle = FontStyle.Bold;
        _nameLabel.alignment = TextAnchor.UpperCenter;
        _nameLabel.color     = Color.white;

        _root.SetActive(false);
        Refresh();
    }

    // ── Refresh ──────────────────────────────────────────────────

    void Refresh()
    {
        if (_inventory == null) return;

        int idx   = _inventory.SelectedIndex;
        var items = _inventory.Items;

        if (idx >= 0 && idx < items.Count)
        {
            var item = items[idx];
            _nameLabel.text = item.itemName;

            if (item.icon != null)
            {
                _icon.sprite = item.icon;
                _icon.color  = Color.white;
            }
            else
            {
                _icon.sprite = null;
                // No icon — show a simple colored placeholder
                _icon.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            }

            _root.SetActive(true);
        }
        else
        {
            _root.SetActive(false);
        }
    }

    // ── Pop animation ────────────────────────────────────────────

    void TriggerPop()
    {
        _popScale  = 1.25f;
        _popTarget = 1f;
        Refresh();
    }

    void Update()
    {
        if (_root == null) return;

        // Smoothly return scale to 1
        _popScale = Mathf.Lerp(_popScale, _popTarget, Time.deltaTime * popAnimSpeed);
        _root.transform.localScale = Vector3.one * _popScale;
    }

    // ── Helper ───────────────────────────────────────────────────

    static Image MakeImage(GameObject parent, string name, Color color,
                           Vector2 anchor, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }
}
