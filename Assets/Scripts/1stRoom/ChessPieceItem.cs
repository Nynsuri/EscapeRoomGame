using UnityEngine;

public class ChessPieceItem : InventoryItem
{
    public enum PieceType { Knight, Queen, Bishop, Rook }

    [Header("Chess Piece")]
    public PieceType pieceType;

    // Uses serialized fields from InventoryItem base: itemName, description, icon
    // Set itemName = "Knight" etc. in Inspector

    public override void OnInventoryUpdate() { }

    public override void OnSelect()
    {
        // Could highlight the piece or show placement hint
    }

    // Called by ChessBoard — no hotkey needed, board interaction handles placement
}