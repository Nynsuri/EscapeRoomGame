using UnityEngine;

/// <summary>
/// CollectibleItem — Unity 6000.3.9f1
///
/// Attach alongside ItemPickup on any world collectible.
/// Does NOT enter the player inventory — registers with CollectibleManager
/// and marks itself collected so pedestals can check for it.
///
/// Set the CollectibleID to match the corresponding ItemPedestal in the scene.
/// </summary>
public class CollectibleItem : MonoBehaviour
{
    [Header("Collectible Identity")]
    [Tooltip("Must match the ID set on the corresponding ItemPedestal")]
    public CollectibleID id = CollectibleID.None;

    [Header("Display")]
    public string collectibleName = "Mysterious Relic";

    [Header("Pickup FX (optional)")]
    public GameObject pickupFXPrefab;
    public AudioClip pickupSound;

    // ─────────────────────────────────────────────────────────────

    private bool _collected = false;
    public bool IsCollected => _collected;

    /// <summary>
    /// Call this from your interaction / raycast script.
    /// Returns true if successfully collected.
    /// </summary>
    public bool TryCollect()
    {
        if (_collected) return false;

        if (id == CollectibleID.None)
        {
            Debug.LogWarning($"[CollectibleItem] '{gameObject.name}' has no CollectibleID assigned!");
            return false;
        }

        if (CollectibleManager.Instance == null)
        {
            Debug.LogWarning("[CollectibleItem] No CollectibleManager found in scene!");
            return false;
        }

        _collected = true;

        // ── FX ───────────────────────────────────────────────────
        if (pickupFXPrefab != null)
            Instantiate(pickupFXPrefab, transform.position, Quaternion.identity);

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        // ── Hide in world ─────────────────────────────────────────
        SetWorldVisible(false);

        // ── Notify manager ────────────────────────────────────────
        CollectibleManager.Instance.RegisterCollected(this);

        Debug.Log($"[CollectibleItem] Collected: {collectibleName} (ID: {id})");
        return true;
    }

    /// <summary>
    /// Called by ItemPedestal to make the item visible on the mount point.
    /// Moves this GameObject to the pedestal's mount transform.
    /// </summary>
    public void PlaceOnPedestal(Transform mountPoint)
    {
        transform.SetParent(mountPoint);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        SetWorldVisible(true);
    }

    // ─────────────────────────────────────────────────────────────

    void SetWorldVisible(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = visible;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = visible;
    }
}