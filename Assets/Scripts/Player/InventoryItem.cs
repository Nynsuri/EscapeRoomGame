using UnityEngine;

/// <summary>
/// InventoryItem — BASE CLASS only.
/// On pickup: disables renderer + collider so the world object is invisible and unclickable.
/// On drop: re-enables both and moves back to world position.
/// </summary>
public abstract class InventoryItem : MonoBehaviour
{
    [Header("Item Info")]
    public string itemName = "Unnamed Item";
    [TextArea] public string description = "";
    public Sprite icon;

    public virtual void OnPickup()
    {
        // Hide visuals and disable collider — keep GameObject active
        // so components (like TorchItem) keep running
        SetWorldVisible(false);
    }

    public virtual void OnDrop(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        SetWorldVisible(true);
    }

    void SetWorldVisible(bool visible)
    {
        // Toggle all renderers
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = visible;

        // Toggle all colliders so the pickup raycast works again when dropped
        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = visible;
    }

    public abstract void OnInventoryUpdate();
    public virtual void OnSelect() { }
    public virtual void OnDeselect() { }
}