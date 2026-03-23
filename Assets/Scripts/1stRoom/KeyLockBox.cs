using System.Collections;
using UnityEngine;

public class KeyLockBox : MonoBehaviour
{
    [Header("Box Opening")]
    public Transform boxDoor;
    public bool openByRotation = true;
    public Vector3 openRotation = new Vector3(0f, -110f, 0f);
    public Vector3 openSlideOffset = new Vector3(0.3f, 0f, 0f);
    public float openSpeed = 2f;

    [Header("Inner Reward")]
    [Tooltip("The key inside the box — visible always, but ItemPickup disabled until opened")]
    public GameObject innerKey;

    [Header("Interaction")]
    public float interactRange = 3f;
    public KeyCode interactKey = KeyCode.E;

    private Camera _cam;
    private GUIStyle _promptStyle;
    private bool _unlocked = false;
    private bool _opening = false;

    void Start()
    {
        _cam = Camera.main ?? FindFirstObjectByType<Camera>();

        // Key is visible but ItemPickup and Collider disabled until box opens
        if (innerKey != null)
        {
            var pickup = innerKey.GetComponent<ItemPickup>();
            if (pickup != null) pickup.enabled = false;
            foreach (var c in innerKey.GetComponentsInChildren<Collider>())
                c.enabled = false;
        }
    }

    void Update()
    {
        if (_unlocked || _opening) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject &&
            !IsChildOf(hit.collider.gameObject, gameObject)) return;

        if (Input.GetKeyDown(interactKey))
        {
            var inventory = FindFirstObjectByType<Inventory>();
            if (inventory == null) return;
            KeyItem key = FindBoxKey(inventory);
            if (key == null) return;

            inventory.RemoveItem(key);
            key.OnConsume();
            StartCoroutine(OpenBox());
        }
    }

    IEnumerator OpenBox()
    {
        _opening = true;

        if (boxDoor != null)
        {
            if (openByRotation)
            {
                Quaternion startRot = boxDoor.localRotation;
                Quaternion endRot = Quaternion.Euler(openRotation);
                float elapsed = 0f, dur = 1f / openSpeed;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    boxDoor.localRotation = Quaternion.Slerp(startRot, endRot,
                        Mathf.SmoothStep(0f, 1f, elapsed / dur));
                    yield return null;
                }
                boxDoor.localRotation = endRot;
            }
            else
            {
                Vector3 startPos = boxDoor.localPosition;
                Vector3 endPos = startPos + openSlideOffset;
                float elapsed = 0f, dur = 1f / openSpeed;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    boxDoor.localPosition = Vector3.Lerp(startPos, endPos,
                        Mathf.SmoothStep(0f, 1f, elapsed / dur));
                    yield return null;
                }
                boxDoor.localPosition = endPos;
            }
        }

        // Enable pickup and collider now that box is open
        if (innerKey != null)
        {
            var pickup = innerKey.GetComponent<ItemPickup>();
            if (pickup != null) pickup.enabled = true;
            foreach (var c in innerKey.GetComponentsInChildren<Collider>())
                c.enabled = true;
        }

        _unlocked = true;
        _opening = false;
    }

    KeyItem FindBoxKey(Inventory inventory)
    {
        foreach (var item in inventory.Items)
            if (item is KeyItem k && k.keyType == KeyItem.KeyType.BoxKey)
                return k;
        return null;
    }

    void OnGUI()
    {
        if (_unlocked || _opening) return;

        var inventory = FindFirstObjectByType<Inventory>();
        if (inventory == null) return;
        if (FindBoxKey(inventory) == null) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return;
        if (hit.collider.gameObject != gameObject &&
            !IsChildOf(hit.collider.gameObject, gameObject)) return;

        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

        string msg = $"[{interactKey}]  Use Key";
        float pw = 360f, ph = 40f;
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