using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// CollectibleManager — Unity 6000.3.9f1
///
/// Singleton. Attach to an empty GameObject named "CollectibleManager".
///
/// Tracks two things:
///   1. Which CollectibleItems the player has PICKED UP  (by ID)
///   2. How many pedestals have been FILLED
///
/// The reward fires when filledCount == requiredCount (all pedestals filled).
/// </summary>
public class CollectibleManager : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────
    public static CollectibleManager Instance { get; private set; }

    // ── Settings ─────────────────────────────────────────────────
    [Header("Settings")]
    [Tooltip("Number of pedestals that must be filled to trigger the reward")]
    public int requiredCount = 3;

    [Header("HUD Counter (optional)")]
    [Tooltip("TextMeshProUGUI or UI.Text that shows collected progress")]
    public GameObject hudCounterObject;

    // ── Events ───────────────────────────────────────────────────
    /// <summary>Fires when all pedestals are filled.</summary>
    public event System.Action OnAllPlaced;

    /// <summary>Inspector-wirable version of OnAllPlaced.</summary>
    public UnityEvent onAllPlacedUnityEvent;

    /// <summary>Fires every time a collectible is picked up (not placed).</summary>
    public event System.Action<CollectibleItem> OnItemCollected;

    // ── State ────────────────────────────────────────────────────

    // Items the player has picked up, keyed by their ID
    private readonly Dictionary<CollectibleID, CollectibleItem> _collected
        = new Dictionary<CollectibleID, CollectibleItem>();

    // IDs that have been placed on a pedestal
    private readonly HashSet<CollectibleID> _placed = new HashSet<CollectibleID>();

    public int CollectedCount => _collected.Count;
    public int PlacedCount => _placed.Count;
    public bool IsComplete => _placed.Count >= requiredCount;

    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─────────────────────────────────────────────────────────────
    // Called by CollectibleItem.TryCollect()
    // ─────────────────────────────────────────────────────────────

    public void RegisterCollected(CollectibleItem item)
    {
        if (_collected.ContainsKey(item.id))
        {
            Debug.LogWarning($"[CollectibleManager] Duplicate collect for ID {item.id} — ignored.");
            return;
        }

        _collected[item.id] = item;
        Debug.Log($"[CollectibleManager] Picked up: {item.collectibleName} (ID: {item.id}) — {_collected.Count} in pocket");

        UpdateHUD();
        OnItemCollected?.Invoke(item);
    }

    // ─────────────────────────────────────────────────────────────
    // Called by ItemPedestal.TryPlace()
    // ─────────────────────────────────────────────────────────────

    public void RegisterPlaced(CollectibleID id)
    {
        if (_placed.Contains(id)) return;

        _placed.Add(id);
        Debug.Log($"[CollectibleManager] Pedestal filled: {id} — {_placed.Count}/{requiredCount}");

        UpdateHUD();

        if (_placed.Count >= requiredCount)
        {
            Debug.Log("[CollectibleManager] All pedestals filled — triggering reward!");
            OnAllPlaced?.Invoke();
            onAllPlacedUnityEvent?.Invoke();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Queries used by ItemPedestal
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the CollectibleItem the player is carrying with this ID,
    /// or null if they haven't picked it up yet.
    /// </summary>
    public CollectibleItem GetCollectedItem(CollectibleID id)
    {
        _collected.TryGetValue(id, out CollectibleItem item);
        return item;
    }

    /// <summary>
    /// Returns the collectibleName for an item that has been collected,
    /// or an empty string if not yet picked up.
    /// </summary>
    public string GetCollectibleName(CollectibleID id)
    {
        return _collected.TryGetValue(id, out var item) ? item.collectibleName : "";
    }

    public bool HasCollected(CollectibleID id) => _collected.ContainsKey(id);
    public bool HasPlaced(CollectibleID id) => _placed.Contains(id);

    // ─────────────────────────────────────────────────────────────

    void UpdateHUD()
    {
        if (hudCounterObject == null) return;
        string text = $"Relics placed: {_placed.Count} / {requiredCount}";

        var tmp = hudCounterObject.GetComponent<TMPro.TextMeshProUGUI>();
        if (tmp != null) { tmp.text = text; return; }
        var legacy = hudCounterObject.GetComponent<UnityEngine.UI.Text>();
        if (legacy != null) { legacy.text = text; }
    }
}