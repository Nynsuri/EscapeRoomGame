using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// BottlePlacementZone -- Unity 6000.3.9f1
/// Fully self-contained bottle placement puzzle. No singleton manager needed.
///
/// BAR  : zoneType = Bar.   Requires 2 specific bottles. Each gets 1 glass slot.
/// TABLE: zoneType = Table. Requires 1 specific bottle. Gets 4 glass slots.
///
/// Attach to each table / bar parent GameObject (collider can be on any child).
/// Player looks at it and presses E. The selected InventoryItem is checked;
/// if it is a BottleItem with a matching bottleID it is consumed and the bottle
/// + glass prefab(s) are spawned. onSolved fires when all slots on THIS zone are filled.
/// </summary>
public class BottlePlacementZone : MonoBehaviour
{
    public enum ZoneType { Table, Bar }

    [Header("Zone Setup")]
    public ZoneType zoneType = ZoneType.Table;

    // --- BAR -----------------------------------------------------------------

    [Header("Bar - Bottle Slot A")]
    [Tooltip("bottleID that must be placed in slot A (must match BottleItem.bottleID on the pickup)")]
    public string barBottleID_A = "Bottle_A";
    [Tooltip("Empty GO marking where the bottle prefab spawns")]
    public Transform barBottleSlot_A;
    [Tooltip("Prefab of the placed bottle visual")]
    public GameObject barBottlePrefab_A;
    [Tooltip("Empty GO marking where the glass spawns next to bottle A")]
    public Transform barGlassSlot_A;
    [Tooltip("Glass prefab spawned when bottle A is placed")]
    public GameObject barGlassPrefab_A;

    [Header("Bar - Bottle Slot B")]
    public string barBottleID_B = "Bottle_B";
    public Transform barBottleSlot_B;
    public GameObject barBottlePrefab_B;
    public Transform barGlassSlot_B;
    public GameObject barGlassPrefab_B;

    // --- TABLE ----------------------------------------------------------------

    [Header("Table - Bottle Slot")]
    [Tooltip("bottleID required for this table (must match BottleItem.bottleID on the pickup)")]
    public string tableBottleID = "Bottle_C";
    [Tooltip("Empty GO marking where the bottle prefab spawns on the table")]
    public Transform tableBottleSlot;
    [Tooltip("Prefab of the placed bottle visual")]
    public GameObject tableBottlePrefab;

    [Header("Table - Glass Slots (4 seats around the table)")]
    public Transform tableGlassSlot_1;
    public Transform tableGlassSlot_2;
    public Transform tableGlassSlot_3;
    public Transform tableGlassSlot_4;
    [Tooltip("Glass prefab -- same prefab reused for all 4 seats")]
    public GameObject tableGlassPrefab;

    [Header("Spawn Scale")]
    [Tooltip("World-space scale applied to every spawned bottle.")]
    public Vector3 spawnScale = Vector3.one;
    [Tooltip("World-space scale applied to every spawned glass. Tweak independently from the bottle.")]
    public Vector3 glassScale = Vector3.one;

    [Header("Interaction")]
    public float interactRange = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Sound Effects")]
    public AudioClip placeSound;

    [Header("Audio Mixer")]
    public UnityEngine.Audio.AudioMixerGroup audioMixerGroup;

    [Header("On Solved - fires when all slots on this zone are filled")]
    public UnityEvent onSolved;

    // --- State ----------------------------------------------------------------

    private bool _slotA_filled = false;
    private bool _slotB_filled = false;
    private bool _tableFilled = false;
    private bool _solved = false;

    public bool IsSolved => _solved;

    private Camera _cam;
    private GUIStyle _promptStyle;

    // --- Unity ----------------------------------------------------------------

    void Start()
    {
        _cam = Camera.main ?? FindFirstObjectByType<Camera>();
    }

    void Update()
    {
        if (_solved) return;
        if (!IsLookingAtZone()) return;

        if (Input.GetKeyDown(interactKey))
        {
            var inventory = FindFirstObjectByType<Inventory>();
            if (inventory == null) return;
            BottleItem bottle = GetSelectedBottle(inventory) ?? FindAcceptableBottle(inventory);
            if (bottle != null) TryPlace(bottle, inventory);
        }
    }

    // Walks UP from the hit collider's transform so this works even when the
    // collider lives on a deeply nested child mesh (e.g. Object006_glassbilel_0.001).
    bool IsLookingAtZone()
    {
        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange)) return false;
        Transform t = hit.collider.transform;
        while (t != null)
        {
            if (t.gameObject == gameObject) return true;
            t = t.parent;
        }
        return false;
    }

    // --- Placement ------------------------------------------------------------

    void TryPlace(BottleItem bottle, Inventory inventory)
    {
        if (zoneType == ZoneType.Bar)
        {
            if (!_slotA_filled && bottle.bottleID == barBottleID_A)
                PlaceBarSlot(bottle, inventory, isSlotA: true);
            else if (!_slotB_filled && bottle.bottleID == barBottleID_B)
                PlaceBarSlot(bottle, inventory, isSlotA: false);
        }
        else
        {
            if (!_tableFilled && bottle.bottleID == tableBottleID)
                PlaceTableSlot(bottle, inventory);
        }

        CheckSolved();
    }

    void PlaceBarSlot(BottleItem bottle, Inventory inventory, bool isSlotA)
    {
        if (placeSound != null)
            AudioHelper.Play(placeSound, transform.position, audioMixerGroup);
        inventory.RemoveItem(bottle);
        if (isSlotA)
        {
            SpawnAtSlot(barBottlePrefab_A, barBottleSlot_A);
            SpawnAtSlot(barGlassPrefab_A, barGlassSlot_A, isGlass: true);
            _slotA_filled = true;
        }
        else
        {
            SpawnAtSlot(barBottlePrefab_B, barBottleSlot_B);
            SpawnAtSlot(barGlassPrefab_B, barGlassSlot_B, isGlass: true);
            _slotB_filled = true;
        }
    }

    void PlaceTableSlot(BottleItem bottle, Inventory inventory)
    {
        if (placeSound != null)
            AudioHelper.Play(placeSound, transform.position, audioMixerGroup);
        inventory.RemoveItem(bottle);
        SpawnAtSlot(tableBottlePrefab, tableBottleSlot);
        SpawnAtSlot(tableGlassPrefab, tableGlassSlot_1, isGlass: true);
        SpawnAtSlot(tableGlassPrefab, tableGlassSlot_2, isGlass: true);
        SpawnAtSlot(tableGlassPrefab, tableGlassSlot_3, isGlass: true);
        SpawnAtSlot(tableGlassPrefab, tableGlassSlot_4, isGlass: true);
        _tableFilled = true;
    }

    void CheckSolved()
    {
        bool done = zoneType == ZoneType.Bar ? (_slotA_filled && _slotB_filled) : _tableFilled;
        if (!done) return;
        _solved = true;
        onSolved?.Invoke();
    }

    // --- Spawn helper ---------------------------------------------------------
    // Uses worldPositionStays:false so the prefab's authored local rotation and
    // scale are preserved exactly -- no more giant/wrong-rotation spawns.

    void SpawnAtSlot(GameObject prefab, Transform slot, bool isGlass = false)
    {
        if (prefab == null || slot == null) return;

        var go = Instantiate(prefab, slot.position, slot.rotation * prefab.transform.localRotation, slot);

        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = prefab.transform.localRotation;

        Vector3 desired = isGlass ? glassScale : spawnScale;
        if (desired == Vector3.zero) desired = Vector3.one;
        Vector3 parent = slot.lossyScale;
        go.transform.localScale = new Vector3(
            parent.x != 0f ? desired.x / parent.x : desired.x,
            parent.y != 0f ? desired.y / parent.y : desired.y,
            parent.z != 0f ? desired.z / parent.z : desired.z);

        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        foreach (var p in go.GetComponentsInChildren<ItemPickup>(true)) p.enabled = false;
    }

    // --- Inventory helpers ----------------------------------------------------

    BottleItem GetSelectedBottle(Inventory inventory)
    {
        int idx = inventory.SelectedIndex;
        if (idx < 0 || idx >= inventory.Items.Count) return null;
        var b = inventory.Items[idx] as BottleItem;
        return (b != null && Accepts(b)) ? b : null;
    }

    BottleItem FindAcceptableBottle(Inventory inventory)
    {
        foreach (var item in inventory.Items)
            if (item is BottleItem b && Accepts(b)) return b;
        return null;
    }

    bool Accepts(BottleItem b)
    {
        if (zoneType == ZoneType.Bar)
            return (!_slotA_filled && b.bottleID == barBottleID_A) ||
                   (!_slotB_filled && b.bottleID == barBottleID_B);
        return !_tableFilled && b.bottleID == tableBottleID;
    }

    // --- OnGUI prompt ---------------------------------------------------------

    void OnGUI()
    {
        if (_solved) return;
        if (!IsLookingAtZone()) return;

        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

        var inventory = FindFirstObjectByType<Inventory>();
        string surface = zoneType == ZoneType.Bar ? "bar" : gameObject.name;
        string msg;

        BottleItem bottle = inventory != null
            ? (GetSelectedBottle(inventory) ?? FindAcceptableBottle(inventory))
            : null;

        if (bottle != null)
        {
            msg = $"[{interactKey}]  Place {bottle.bottleLabel} on {surface}";
            _promptStyle.normal.textColor = Color.white;
        }
        else
        {
            msg = $"Needs: {GetNeededLabel()}";
            _promptStyle.normal.textColor = new Color(1f, 0.75f, 0.2f);
        }

        float pw = 500f, ph = 40f;
        float px = (Screen.width - pw) / 2f;
        float py = (Screen.height - ph) / 2f + 60f;
        GUI.color = Color.black; GUI.Label(new Rect(px + 2, py + 2, pw, ph), msg, _promptStyle);
        GUI.color = Color.white; GUI.Label(new Rect(px, py, pw, ph), msg, _promptStyle);
    }

    string GetNeededLabel()
    {
        if (zoneType == ZoneType.Bar)
        {
            var needed = new List<string>();
            if (!_slotA_filled) needed.Add(barBottleID_A);
            if (!_slotB_filled) needed.Add(barBottleID_B);
            return string.Join(" & ", needed);
        }
        return tableBottleID;
    }
}