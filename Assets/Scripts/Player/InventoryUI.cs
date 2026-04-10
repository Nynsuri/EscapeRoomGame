using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// InventoryUI — Unity 6000.3.9f1
/// Fully self-building inventory panel using Unity's built-in UI.
/// NO prefab or TextMeshPro required — everything is created in code.
///
/// SETUP: Just add this component to any GameObject in your scene
/// (e.g. the Player or a Manager object). It finds the Inventory
/// automatically and builds the Canvas itself.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Inventory Reference (auto-found if empty)")]
    public Inventory inventory;

    [Header("Layout")]
    public int columns = 4;
    public float slotSize = 80f;
    public float slotPadding = 8f;

    [Header("Colors")]
    public Color panelColor = new Color(0.1f, 0.1f, 0.1f, 0.92f);
    public Color slotColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    public Color selectedColor = new Color(0.9f, 0.7f, 0.1f, 1f);
    public Color unselectedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    public Color textColor = Color.white;

    // ── Runtime refs ────────────────────────────────────────────
    private Canvas _canvas;
    private GameObject _panel;
    private List<SlotWidget> _slots = new();

    private Text _itemNameText;
    private Text _itemDescText;
    private Text _hintText;

    private bool _built = false;

    // ── Slot data class ─────────────────────────────────────────
    private class SlotWidget
    {
        public GameObject root;
        public Image background;
        public Image icon;
        public Text label;
    }

    // ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>();

        if (inventory == null)
        {
            Debug.LogError("[InventoryUI] No Inventory found in scene!");
            return;
        }

        BuildUI();

        inventory.OnInventoryChanged += Refresh;
        inventory.OnInventoryToggled += SetVisible;
        inventory.OnSelectionChanged += HighlightSlot;

        SetVisible(false);
    }

    void OnDestroy()
    {
        if (inventory == null) return;
        inventory.OnInventoryChanged -= Refresh;
        inventory.OnInventoryToggled -= SetVisible;
        inventory.OnSelectionChanged -= HighlightSlot;
    }

    // ── Build entire UI in code ──────────────────────────────────

    void BuildUI()
    {
        // ── Canvas ──
        var canvasGO = new GameObject("InventoryCanvas");
        DontDestroyOnLoad(canvasGO);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 10;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Background panel ──
        int rows = Mathf.CeilToInt(inventory.maxSlots / (float)columns);
        float panelW = columns * (slotSize + slotPadding) + slotPadding;
        float infoH = 100f;
        float panelH = rows * (slotSize + slotPadding) + slotPadding + infoH + 40f;

        _panel = MakeImage(canvasGO, "InventoryPanel", panelColor,
                           new Vector2(0.5f, 0.5f), new Vector2(panelW, panelH), Vector2.zero);

        // ── Title ──
        var title = MakeText(_panel, "Title", "INVENTORY",
                             new Vector2(0.5f, 1f), new Vector2(panelW - 20f, 36f),
                             new Vector2(0f, -18f), 18, FontStyle.Bold);
        title.alignment = TextAnchor.MiddleCenter;

        // ── Slots ──
        for (int i = 0; i < inventory.maxSlots; i++)
        {
            int col = i % columns;
            int row = i / columns;

            float x = slotPadding + col * (slotSize + slotPadding) + slotSize / 2f - panelW / 2f;
            float y = -(40f + slotPadding + row * (slotSize + slotPadding) + slotSize / 2f);

            // Slot background
            var slotGO = MakeImage(_panel, $"Slot_{i}", slotColor,
                                   new Vector2(0.5f, 1f), new Vector2(slotSize, slotSize),
                                   new Vector2(x, y));

            // Add a thin border (slightly larger darker image behind)
            var border = MakeImage(slotGO, "Border", new Color(0, 0, 0, 0.6f),
                                   new Vector2(0.5f, 0.5f), new Vector2(slotSize + 4f, slotSize + 4f),
                                   Vector2.zero);
            border.transform.SetAsFirstSibling();

            // Icon image (transparent until an item is there)
            var iconGO = MakeImage(slotGO, "Icon", Color.white,
                                   new Vector2(0.5f, 0.5f), new Vector2(slotSize - 16f, slotSize - 16f),
                                   Vector2.zero);
            iconGO.GetComponent<Image>().preserveAspect = true;
            iconGO.GetComponent<Image>().color = new Color(1, 1, 1, 0);

            // Slot number label
            var numLabel = MakeText(slotGO, "Num", (i + 1).ToString(),
                                    new Vector2(0f, 1f), new Vector2(20f, 20f),
                                    new Vector2(12f, -10f), 11, FontStyle.Normal);
            numLabel.color = new Color(1, 1, 1, 0.4f);

            // Item name label
            var nameLabel = MakeText(slotGO, "Label", "",
                                     new Vector2(0.5f, 0f), new Vector2(slotSize - 4f, 18f),
                                     new Vector2(0f, 4f), 10, FontStyle.Normal);
            nameLabel.alignment = TextAnchor.MiddleCenter;

            // Slot click → select
            int captured = i;
            var btn = slotGO.AddComponent<Button>();
            btn.onClick.AddListener(() => inventory.SelectSlot(captured));
            btn.targetGraphic = slotGO.GetComponent<Image>();

            _slots.Add(new SlotWidget
            {
                root = slotGO,
                background = slotGO.GetComponent<Image>(),
                icon = iconGO.GetComponent<Image>(),
                label = nameLabel
            });
        }

        // ── Info area ──
        float infoY = -(40f + slotPadding + rows * (slotSize + slotPadding));

        _itemNameText = MakeText(_panel, "ItemName", "",
                                 new Vector2(0.5f, 1f), new Vector2(panelW - 20f, 28f),
                                 new Vector2(0f, infoY - 8f), 16, FontStyle.Bold);
        _itemNameText.alignment = TextAnchor.MiddleCenter;

        _itemDescText = MakeText(_panel, "ItemDesc", "",
                                 new Vector2(0.5f, 1f), new Vector2(panelW - 20f, 50f),
                                 new Vector2(0f, infoY - 40f), 12, FontStyle.Normal);
        _itemDescText.alignment = TextAnchor.UpperCenter;

        // ── Hint bar ──
        _hintText = MakeText(_panel, "Hint", "[TAB] Close   [G] Drop   [1-8] Select   [Scroll] Cycle",
                             new Vector2(0.5f, 0f), new Vector2(panelW - 10f, 22f),
                             new Vector2(0f, 10f), 10, FontStyle.Normal);
        _hintText.alignment = TextAnchor.MiddleCenter;
        _hintText.color = new Color(1, 1, 1, 0.45f);

        _built = true;
        Refresh();
    }

    // ── Refresh slot visuals ─────────────────────────────────────

    void Refresh()
    {
        if (!_built) return;
        var items = inventory.Items;

        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (i < items.Count)
            {
                s.label.text = items[i].itemName;
                if (items[i].icon != null)
                {
                    s.icon.sprite = items[i].icon;
                    s.icon.color = Color.white;
                }
                else
                {
                    s.icon.sprite = null;
                    s.icon.color = new Color(1, 1, 1, 0);
                }
            }
            else
            {
                s.label.text = "";
                s.icon.sprite = null;
                s.icon.color = new Color(1, 1, 1, 0);
            }
        }

        HighlightSlot(inventory.SelectedIndex);
        UpdateInfo();
    }

    void HighlightSlot(int idx)
    {
        if (!_built) return;
        for (int i = 0; i < _slots.Count; i++)
            _slots[i].background.color = i == idx ? selectedColor : slotColor;
        UpdateInfo();
    }

    void UpdateInfo()
    {
        if (!_built) return;
        int idx = inventory.SelectedIndex;
        var items = inventory.Items;

        if (idx >= 0 && idx < items.Count)
        {
            _itemNameText.text = items[idx].itemName;
            _itemDescText.text = items[idx].description;
        }
        else
        {
            _itemNameText.text = "";
            _itemDescText.text = "";
        }
    }

    void SetVisible(bool visible) => _panel?.SetActive(visible);

    // ── UI helper builders ───────────────────────────────────────

    static GameObject MakeImage(GameObject parent, string name, Color color,
                                Vector2 anchor, Vector2 size, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        go.GetComponent<Image>().color = color;
        return go;
    }

    static Text MakeText(GameObject parent, string name, string content,
                         Vector2 anchor, Vector2 size, Vector2 anchoredPos,
                         int fontSize, FontStyle style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        var t = go.GetComponent<Text>();
        t.text = content;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = Color.white;
        t.supportRichText = true;
        return t;
    }
}