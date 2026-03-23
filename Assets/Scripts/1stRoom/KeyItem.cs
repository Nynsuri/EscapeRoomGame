using UnityEngine;

/// <summary>
/// KeyItem.cs — passive inventory item. Used by KeyLock and KeyDoor.
/// Set keyID to match the lock/door it opens.
/// </summary>
public class KeyItem : InventoryItem
{
    public enum KeyType { Normal, Special, BoxKey }

    [Header("Key Settings")]
    [Tooltip("Must match requiredKeyID on the KeyLock or KeyDoor this key opens")]
    public string keyID = "room_key";
    public KeyType keyType = KeyType.Normal;

    void Awake()
    {
        if (string.IsNullOrEmpty(itemName) || itemName == "Unnamed Item")
            itemName = "Key";
        if (string.IsNullOrEmpty(description))
            description = "Opens a lock somewhere...";
    }

    public override void OnInventoryUpdate() { }

    public override void OnPickup()
    {
        // Tell AlwaysVisible to stop forcing renderers on
        var av = GetComponent<AlwaysVisible>();
        if (av != null) av.AllowHide();
        base.OnPickup();
    }

    public override void OnConsume()
    {
        gameObject.SetActive(false);
    }
}