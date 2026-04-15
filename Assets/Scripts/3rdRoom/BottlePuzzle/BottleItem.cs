using UnityEngine;

/// <summary>
/// BottleItem -- attach to the bottle mesh object alongside ItemPickup.
/// On pickup, hides this object AND all siblings under the same parent
/// (covers Object006_etiketks_0.001 and Object006_kriwki_0.001 that share
/// the same parent as the bottle but are separate GameObjects).
/// </summary>
public class BottleItem : InventoryItem
{
    [Header("Bottle Identity")]
    [Tooltip("Unique bottle ID -- must match the 'bottleID' set on the target BottlePlacementZone")]
    public string bottleID = "Bottle_A";

    [Tooltip("Display label shown in the placement prompt, e.g. 'Red Wine'")]
    public string bottleLabel = "Bottle";

    [Header("Sound Effects")]
    public AudioClip pickupSound;

    [Header("Audio Mixer")]
    public UnityEngine.Audio.AudioMixerGroup audioMixerGroup;

    // --- InventoryItem overrides ----------------------------------------------

    public override void OnPickup()
    {
        if (pickupSound != null)
            AudioHelper.Play(pickupSound, transform.position, audioMixerGroup);

        // Hide this object normally via the base class
        base.OnPickup();

        // Also hide every sibling under the same parent so the label and cap
        // (Object006_etiketks, Object006_kriwki, etc.) disappear with the bottle
        HideSiblings(visible: false);
    }

    public override void OnDrop(Vector3 worldPosition)
    {
        base.OnDrop(worldPosition);
        HideSiblings(visible: true);
    }

    public override void OnConsume()
    {
        // Consume the whole parent group, not just this object
        HideSiblings(visible: false);
        base.OnConsume();
    }

    // --- Helpers --------------------------------------------------------------

    void HideSiblings(bool visible)
    {
        if (transform.parent == null) return;

        foreach (Transform sibling in transform.parent)
        {
            if (sibling == transform) continue; // base class already handles self

            foreach (var r in sibling.GetComponentsInChildren<Renderer>(true))
                r.enabled = visible;

            foreach (var c in sibling.GetComponentsInChildren<Collider>(true))
                c.enabled = visible;
        }
    }

    // --- InventoryItem contract -----------------------------------------------

    public override void OnInventoryUpdate() { }
    public override void OnSelect() { }
    public override void OnDeselect() { }
}