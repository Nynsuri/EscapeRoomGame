using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inventory — Unity 6000.3.9f1
/// Manages a list of InventoryItem references and broadcasts per-frame
/// updates to each item so items can poll their own hotkeys.
///
/// Attach to the Player GameObject (alongside PlayerController).
/// </summary>
public class Inventory : MonoBehaviour
{
    [Header("Inventory Settings")]
    [Tooltip("Maximum number of items the player can carry")]
    public int maxSlots = 8;

    [Tooltip("Key to open / close the inventory UI")]
    public KeyCode toggleKey = KeyCode.Tab;


    // Public read-only access for the UI
    public IReadOnlyList<InventoryItem> Items => _items;

    private readonly List<InventoryItem> _items = new();
    private int _selectedIndex = -1;

    // ─── Events (InventoryUI subscribes to these) ──────────────

    public event System.Action OnInventoryChanged;
    public event System.Action<bool> OnInventoryToggled;      // true = opened
    public event System.Action<int> OnSelectionChanged;

    private bool _isOpen = false;

    /// <summary>True when the inventory panel is visible.</summary>
    public bool IsOpen => _isOpen;

    // ───────────────────────────────────────────────────────────

    void Update()
    {
        // Toggle UI
        if (Input.GetKeyDown(toggleKey))
            ToggleInventory();

        // Scroll wheel / number keys to change selection
        HandleSelectionInput();

        // Tick all items so they can respond to their own hotkeys
        foreach (var item in _items)
            item.OnInventoryUpdate();
    }

    // ─── Public API ────────────────────────────────────────────

    /// <summary>Add item to inventory. Returns true on success.</summary>
    public bool AddItem(InventoryItem item)
    {
        if (_items.Count >= maxSlots)
        {
            Debug.Log("[Inventory] Full!");
            return false;
        }

        _items.Add(item);
        item.OnPickup();
        OnInventoryChanged?.Invoke();

        if (_items.Count == 1) SelectSlot(0);
        return true;
    }


    public void RemoveItem(InventoryItem item)
    {
        if (!_items.Contains(item)) return;

        _items.Remove(item);
        Destroy(item.gameObject);

        if (_selectedIndex >= _items.Count)
            SelectSlot(_items.Count - 1);

        OnInventoryChanged?.Invoke();
    }

    /// <summary>Remove an item from inventory without destroying it (used by GunItem.ReturnToWorld).</summary>
    public void RemoveItemNoDestroy(InventoryItem item)
    {
        if (!_items.Contains(item)) return;

        _items.Remove(item);

        if (_selectedIndex >= _items.Count)
            SelectSlot(_items.Count - 1);

        OnInventoryChanged?.Invoke();
    }


    public void SelectSlot(int index)
    {
        if (_items.Count == 0) { _selectedIndex = -1; return; }

        // Deselect previous
        if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
            _items[_selectedIndex].OnDeselect();

        _selectedIndex = Mathf.Clamp(index, 0, _items.Count - 1);
        _items[_selectedIndex].OnSelect();
        OnSelectionChanged?.Invoke(_selectedIndex);
    }

    public int SelectedIndex => _selectedIndex;

    // ─── Private helpers ───────────────────────────────────────

    void ToggleInventory()
    {
        _isOpen = !_isOpen;
        Cursor.lockState = _isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = _isOpen;

        // Block player movement and camera look while inventory is open
        var pc = GetComponent<PlayerController>();
        if (pc != null) pc.enabled = !_isOpen;

        OnInventoryToggled?.Invoke(_isOpen);
    }

    void HandleSelectionInput()
    {
        // Number keys 1–8
        for (int i = 0; i < maxSlots; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectSlot(i);
                return;
            }
        }

        // Scroll wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) SelectSlot(_selectedIndex - 1 < 0 ? _items.Count - 1 : _selectedIndex - 1);
        if (scroll < 0f) SelectSlot((_selectedIndex + 1) % Mathf.Max(1, _items.Count));
    }

    /// <summary>Close the inventory from an external script (e.g. PauseMenuManager).</summary>
    public void ForceClose()
    {
        if (!_isOpen) return;
        _isOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var pc = GetComponent<PlayerController>();
        if (pc != null) pc.enabled = true;

        OnInventoryToggled?.Invoke(false);
    }
}