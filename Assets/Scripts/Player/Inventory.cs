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

    [Tooltip("Key to drop the currently selected item")]
    public KeyCode dropKey = KeyCode.G;

    // Public read-only access for the UI
    public IReadOnlyList<InventoryItem> Items => _items;

    private readonly List<InventoryItem> _items = new();
    private int _selectedIndex = -1;

    // ─── Events (InventoryUI subscribes to these) ──────────────

    public event System.Action OnInventoryChanged;
    public event System.Action<bool> OnInventoryToggled;      // true = opened
    public event System.Action<int> OnSelectionChanged;

    private bool _isOpen = false;

    // ───────────────────────────────────────────────────────────

    void Update()
    {
        // Toggle UI
        if (Input.GetKeyDown(toggleKey))
            ToggleInventory();

        // Drop selected item
        if (Input.GetKeyDown(dropKey))
            DropSelected();

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

    /// <summary>Remove item from inventory (e.g. after use).</summary>
    public void RemoveItem(InventoryItem item)
    {
        if (!_items.Contains(item)) return;

        int idx = _items.IndexOf(item);
        _items.Remove(item);
        Destroy(item.gameObject);  // or pool it

        // Clamp selection
        if (_selectedIndex >= _items.Count)
            SelectSlot(_items.Count - 1);

        OnInventoryChanged?.Invoke();
    }

    /// <summary>Drop selected item into the world.</summary>
    public void DropSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _items.Count) return;

        var item = _items[_selectedIndex];
        _items.RemoveAt(_selectedIndex);

        Vector3 dropPos = transform.position + transform.forward * 1.5f;
        item.OnDrop(dropPos);

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
        Cursor.visible   = _isOpen;
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
}
