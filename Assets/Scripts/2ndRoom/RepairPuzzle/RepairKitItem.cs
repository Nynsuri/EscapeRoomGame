using UnityEngine;

/// <summary>
/// RepairKitItem.cs — inventory item found in the repair box.
/// Required to start the CircuitRepairPuzzle.
/// </summary>
public class RepairKitItem : InventoryItem
{
    void Awake()
    {
        if (string.IsNullOrEmpty(itemName) || itemName == "Unnamed Item")
            itemName = "Repair Kit";
        if (string.IsNullOrEmpty(description))
            description = "A toolkit for repairing circuit boards.";
    }

    public override void OnInventoryUpdate() { }
}
