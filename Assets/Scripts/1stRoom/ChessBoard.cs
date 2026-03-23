using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ChessBoard.cs — Unity 6000.3.9f1
/// Player places chess pieces on the board by looking at it and pressing E.
/// Pieces are spawned from prefabs at the designated slots.
/// </summary>
public class ChessBoard : MonoBehaviour
{
    [Header("Piece Prefabs (assign the chess piece prefabs)")]
    public GameObject knightPrefab;
    public GameObject queenPrefab;
    public GameObject bishopPrefab;
    public GameObject rookPrefab;

    [Header("Board Slots (empty GOs placed on the board)")]
    public Transform knightSlot;
    public Transform queenSlot;
    public Transform bishopSlot;
    public Transform rookSlot;

    [Header("Reward — Normal (Knight + Queen + Bishop/Rook placed)")]
    public Transform normalRewardDrawer;
    public float normalDrawerOpenDistance = 0.3f;
    public float normalDrawerOpenSpeed = 1.2f;
    public Vector3 normalDrawerDirection = new Vector3(1f, 0f, 0f);
    public GameObject roomKey;

    [Header("Reward — Secret (Rook placed = straight cable solve)")]
    public Transform secretRewardDrawer;
    public float secretDrawerOpenDistance = 0.3f;
    public float secretDrawerOpenSpeed = 1.2f;
    public Vector3 secretDrawerDirection = new Vector3(1f, 0f, 0f);
    public GameObject specialItem;

    [Header("Interaction")]
    public float interactRange = 3f;
    public KeyCode interactKey = KeyCode.E;

    private bool _knightPlaced = false;
    private bool _queenPlaced = false;
    private bool _bishopPlaced = false;
    private bool _rookPlaced = false;
    private bool _solved = false;

    private Camera _cam;
    private GUIStyle _promptStyle;

    void Start()
    {
        _cam = Camera.main ?? FindFirstObjectByType<Camera>();
        if (roomKey) roomKey.SetActive(false);
        if (specialItem) specialItem.SetActive(false);
    }

    void Update()
    {
        if (_solved) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject &&
            !IsChildOf(hit.collider.gameObject, gameObject)) return;

        if (Input.GetKeyDown(interactKey))
        {
            var inventory = FindFirstObjectByType<Inventory>();
            if (inventory == null) return;
            ChessPieceItem piece = FindPlaceablePiece(inventory);
            if (piece != null) PlacePiece(piece, inventory);
        }
    }

    void PlacePiece(ChessPieceItem piece, Inventory inventory)
    {
        if (IsAlreadyPlaced(piece.pieceType)) return;

        Transform slot = GetSlot(piece.pieceType);
        GameObject prefab = GetPrefab(piece.pieceType);
        if (slot == null || prefab == null) return;

        // Remove from inventory
        inventory.RemoveItem(piece);

        // Spawn prefab at slot
        var spawned = Instantiate(prefab, slot.position, slot.rotation, slot);
        spawned.transform.localPosition = Vector3.zero;
        spawned.transform.localRotation = Quaternion.identity;

        // Make sure all renderers are visible
        foreach (var r in spawned.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;

        // Disable any pickup components on spawned piece
        foreach (var p in spawned.GetComponentsInChildren<ItemPickup>(true))
            p.enabled = false;

        MarkPlaced(piece.pieceType);
        CheckCompletion();
    }

    void CheckCompletion()
    {
        if (!_knightPlaced || !_queenPlaced) return;
        if (!_bishopPlaced && !_rookPlaced) return;

        _solved = true;
        StartCoroutine(OpenRewardDrawers());
    }

    IEnumerator OpenRewardDrawers()
    {
        yield return new WaitForSeconds(0.5f);

        if (normalRewardDrawer != null)
            yield return StartCoroutine(OpenDrawer(normalRewardDrawer,
                normalDrawerDirection, normalDrawerOpenDistance, normalDrawerOpenSpeed));

        if (roomKey != null) roomKey.SetActive(true);

        if (_rookPlaced && secretRewardDrawer != null)
        {
            yield return new WaitForSeconds(0.3f);
            yield return StartCoroutine(OpenDrawer(secretRewardDrawer,
                secretDrawerDirection, secretDrawerOpenDistance, secretDrawerOpenSpeed));
            if (specialItem != null) specialItem.SetActive(true);
        }
    }

    IEnumerator OpenDrawer(Transform drawer, Vector3 direction, float distance, float speed)
    {
        Vector3 start = drawer.localPosition;
        Vector3 end = start + direction.normalized * distance;
        float elapsed = 0f, dur = distance / speed;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            drawer.localPosition = Vector3.Lerp(start, end,
                Mathf.SmoothStep(0f, 1f, elapsed / dur));
            yield return null;
        }
        drawer.localPosition = end;
    }

    // ── Helpers ───────────────────────────────────────────────────

    Transform GetSlot(ChessPieceItem.PieceType t)
    {
        switch (t)
        {
            case ChessPieceItem.PieceType.Knight: return knightSlot;
            case ChessPieceItem.PieceType.Queen: return queenSlot;
            case ChessPieceItem.PieceType.Bishop: return bishopSlot;
            case ChessPieceItem.PieceType.Rook: return rookSlot;
        }
        return null;
    }

    GameObject GetPrefab(ChessPieceItem.PieceType t)
    {
        switch (t)
        {
            case ChessPieceItem.PieceType.Knight: return knightPrefab;
            case ChessPieceItem.PieceType.Queen: return queenPrefab;
            case ChessPieceItem.PieceType.Bishop: return bishopPrefab;
            case ChessPieceItem.PieceType.Rook: return rookPrefab;
        }
        return null;
    }

    bool IsAlreadyPlaced(ChessPieceItem.PieceType t)
    {
        switch (t)
        {
            case ChessPieceItem.PieceType.Knight: return _knightPlaced;
            case ChessPieceItem.PieceType.Queen: return _queenPlaced;
            case ChessPieceItem.PieceType.Bishop: return _bishopPlaced;
            case ChessPieceItem.PieceType.Rook: return _rookPlaced;
        }
        return false;
    }

    void MarkPlaced(ChessPieceItem.PieceType t)
    {
        switch (t)
        {
            case ChessPieceItem.PieceType.Knight: _knightPlaced = true; break;
            case ChessPieceItem.PieceType.Queen: _queenPlaced = true; break;
            case ChessPieceItem.PieceType.Bishop: _bishopPlaced = true; break;
            case ChessPieceItem.PieceType.Rook: _rookPlaced = true; break;
        }
    }

    ChessPieceItem FindPlaceablePiece(Inventory inventory)
    {
        foreach (var item in inventory.Items)
            if (item is ChessPieceItem cp && !IsAlreadyPlaced(cp.pieceType))
                return cp;
        return null;
    }

    void OnGUI()
    {
        if (_solved) return;
        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject &&
            !IsChildOf(hit.collider.gameObject, gameObject)) return;

        var inventory = FindFirstObjectByType<Inventory>();
        if (inventory == null) return;
        var piece = FindPlaceablePiece(inventory);
        if (piece == null) return;

        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

        string msg = $"[{interactKey}]  Place {piece.pieceType} on board";
        float pw = 460f, ph = 40f;
        float px = (Screen.width - pw) / 2f;
        float py = (Screen.height - ph) / 2f + 60f;
        GUI.color = Color.black; GUI.Label(new Rect(px + 2, py + 2, pw, ph), msg, _promptStyle);
        GUI.color = Color.white; GUI.Label(new Rect(px, py, pw, ph), msg, _promptStyle);
    }

    static bool IsChildOf(GameObject child, GameObject parent)
    {
        Transform t = child.transform;
        while (t != null) { if (t.gameObject == parent) return true; t = t.parent; }
        return false;
    }
}