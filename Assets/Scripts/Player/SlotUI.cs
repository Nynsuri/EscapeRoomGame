using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SlotUI — Unity 6000.3.9f1
/// Lives on each inventory slot prefab.
/// Holds references to the icon image, highlight border, and optional label.
///
/// SLOT PREFAB STRUCTURE (create this prefab in your project):
///   SlotRoot  — Panel + Button + SlotUI.cs
///   ├── Icon       — Image component  (displays item icon)
///   ├── Highlight  — Image component  (colored border when selected)
///   └── Label      — TextMeshProUGUI  (optional small item name)
/// </summary>
public class SlotUI : MonoBehaviour
{
    [Tooltip("Image that displays the item icon")]
    public Image iconImage;

    [Tooltip("Image used as a selection highlight / border")]
    public Image highlightImage;

    [Tooltip("Optional small label under the icon")]
    public TextMeshProUGUI labelText;

    void Awake()
    {
        // Auto-find children if not assigned in Inspector
        if (iconImage == null || highlightImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            if (images.Length >= 1 && iconImage      == null) iconImage      = images[0];
            if (images.Length >= 2 && highlightImage == null) highlightImage = images[1];
        }

        if (labelText == null)
            labelText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    /// <summary>Fill this slot with an item's icon and name.</summary>
    public void SetItem(Sprite icon, string label)
    {
        if (iconImage != null)
        {
            iconImage.sprite  = icon;
            iconImage.enabled = icon != null;
            iconImage.color   = Color.white;
        }

        if (labelText != null)
            labelText.text = label;
    }

    /// <summary>Clear the slot (empty slot).</summary>
    public void ClearItem()
    {
        if (iconImage != null)
        {
            iconImage.sprite  = null;
            iconImage.enabled = false;
        }

        if (labelText != null)
            labelText.text = "";

        SetHighlight(false, Color.yellow, Color.white);
    }

    /// <summary>Show or hide the selection highlight.</summary>
    public void SetHighlight(bool selected, Color onColor, Color offColor)
    {
        if (highlightImage != null)
            highlightImage.color = selected ? onColor : offColor;
    }
}
