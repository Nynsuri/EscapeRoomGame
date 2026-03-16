using System.Collections;
using UnityEngine;

/// <summary>
/// ItemPickup.cs — attach to a world item alongside an InventoryItem subclass.
/// Optionally assign a drawer that closes when this item is picked up.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [Header("Drawer (optional — closes when item is picked up)")]
    public Transform drawerToClose;
    public Vector3 drawerCloseDirection = new Vector3(-1f, 0f, 0f);
    public float drawerCloseDistance = 0.3f;
    public float drawerCloseSpeed = 1.2f;

    private InventoryItem _item;

    void Awake()
    {
        _item = GetComponent<InventoryItem>();
        if (_item == null)
            Debug.LogWarning($"[ItemPickup] No InventoryItem on {gameObject.name}!");
    }

    public void TryPickup(Inventory inventory)
    {
        if (_item == null || inventory == null) return;

        bool added = inventory.AddItem(_item);
        if (!added) return;

        // Close drawer if assigned
        if (drawerToClose != null)
            StartCoroutine(CloseDrawer());
    }

    IEnumerator CloseDrawer()
    {
        Vector3 start = drawerToClose.localPosition;
        Vector3 end = start + drawerCloseDirection.normalized * drawerCloseDistance;
        float elapsed = 0f, dur = drawerCloseDistance / drawerCloseSpeed;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            drawerToClose.localPosition = Vector3.Lerp(start, end,
                Mathf.SmoothStep(0f, 1f, elapsed / dur));
            yield return null;
        }
        drawerToClose.localPosition = end;
    }
}