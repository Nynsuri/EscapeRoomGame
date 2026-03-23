using UnityEngine;

/// <summary>
/// CablePieceItem.cs — a single cable piece found in the cabinet.
/// Player needs 4 of these to complete the CableBoxPuzzle.
/// Set cableColor to match which cable this piece is.
/// </summary>
public class CablePieceItem : InventoryItem
{
    public CableBoxPuzzle.CableColor cableColor;

    public override void OnInventoryUpdate() { }

    void Awake()
    {
        if (string.IsNullOrEmpty(itemName) || itemName == "Unnamed Item")
            itemName = $"{cableColor} Cable";
        if (string.IsNullOrEmpty(description))
            description = $"A {cableColor.ToString().ToLower()} cable. Find the rest and connect them.";
    }
}
