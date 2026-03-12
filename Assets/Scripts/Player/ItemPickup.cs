using UnityEngine;

/// <summary>
/// ItemPickup.cs — attach to a world item alongside an InventoryItem subclass.
/// The actual scanning/raycasting is done by ItemPickupScanner on the Player.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    private InventoryItem _item;

    void Awake()
    {
        _item = GetComponent<InventoryItem>();
        if (_item == null)
            Debug.LogWarning($"[ItemPickup] No InventoryItem component on {gameObject.name}!");
    }

    public void TryPickup(Inventory inventory)
    {
        if (_item == null || inventory == null) return;

        bool added = inventory.AddItem(_item);
        if (added)
            Debug.Log($"[ItemPickup] Picked up {_item.itemName}");
        else
            Debug.Log("[ItemPickup] Inventory full!");

        // DO NOT destroy this component — item may be dropped and picked up again
    }
}