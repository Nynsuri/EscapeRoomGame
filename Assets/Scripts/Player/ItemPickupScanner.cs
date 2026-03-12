using UnityEngine;

/// <summary>
/// ItemPickupScanner.cs — attach to the Player GameObject.
/// Raycasts from the camera each frame. When an ItemPickup is in range
/// and the player looks at it, shows a prompt and handles the E key.
/// </summary>
public class ItemPickupScanner : MonoBehaviour
{
    [Tooltip("Camera child transform — auto-found if left empty")]
    public Transform cameraTransform;
    [Tooltip("Max pickup distance in units")]
    public float pickupRange = 3f;
    [Tooltip("Key to pick up item")]
    public KeyCode pickupKey = KeyCode.E;

    private Inventory _inventory;
    private ItemPickup _focused;
    private GUIStyle _promptStyle;

    void Awake()
    {
        _inventory = GetComponent<Inventory>();
        if (_inventory == null)
            Debug.LogError("[ItemPickupScanner] No Inventory found on this GameObject!");

        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null)
                cameraTransform = cam.transform;
            else
                Debug.LogError("[ItemPickupScanner] No Camera found as child of Player!");
        }
    }

    void Update()
    {
        ScanForItem();

        if (_focused != null && Input.GetKeyDown(pickupKey))
            _focused.TryPickup(_inventory);
    }

    void ScanForItem()
    {
        _focused = null;
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        Debug.DrawRay(ray.origin, ray.direction * pickupRange, Color.green); // visible in Scene view

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange))
        {
            _focused = hit.collider.GetComponent<ItemPickup>();
        }
    }

    void OnGUI()
    {
        if (_focused == null) return;

        if (_promptStyle == null)
        {
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
        }

        string itemName = _focused.GetComponent<InventoryItem>()?.itemName ?? "Item";
        string msg = $"[{pickupKey}]  Pick up  {itemName}";

        float w = 400f, h = 40f;
        float x = (Screen.width  - w) / 2f;
        float y = (Screen.height - h) / 2f + 60f;

        // Shadow
        GUI.color = Color.black;
        GUI.Label(new Rect(x + 2, y + 2, w, h), msg, _promptStyle);
        // Text
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, w, h), msg, _promptStyle);
    }
}
