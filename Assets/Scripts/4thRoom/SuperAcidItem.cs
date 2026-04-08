using UnityEngine;

/// <summary>
/// SuperAcidItem — InventoryItem subclass.
/// Produced by the ChemistryPuzzle.  When selected and used (left-click),
/// it dissolves any GameObject tagged "Dissolvable" that the player is
/// looking at within range.
///
/// Setup:
///   1. Create a prefab with a visible mesh (e.g. a small vial model).
///   2. Attach this script.
///   3. Set itemName, description, and icon in the Inspector.
///   4. Tag the object you want to dissolve as "Dissolvable".
///   5. Assign this prefab to ChemistryPuzzle.superAcidPrefab.
/// </summary>
public class SuperAcidItem : InventoryItem
{
    [Header("Super Acid Settings")]
    [Tooltip("How far the player can be to dissolve an object")]
    public float useRange = 5f;

    [Tooltip("Key to use the acid (when selected)")]
    public KeyCode useKey = KeyCode.Mouse0;

    [Tooltip("Tag on objects that can be dissolved")]
    public string dissolvableTag = "Dissolvable";

    [Tooltip("Time in seconds for the dissolve effect")]
    public float dissolveTime = 2f;

    [Header("Optional")]
    [Tooltip("Particle effect to spawn at dissolve location")]
    public GameObject dissolveVFXPrefab;

    private bool _isSelected = false;
    private Camera _cam;
    private GUIStyle _promptStyle;

    void Awake()
    {
        if (string.IsNullOrEmpty(itemName)) itemName = "Super Acid";
        if (string.IsNullOrEmpty(description)) description = "A dangerously potent acid. Use on corroded barriers to dissolve them.";
    }

    public override void OnSelect()
    {
        base.OnSelect();
        _isSelected = true;
        _cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
    }

    public override void OnDeselect()
    {
        base.OnDeselect();
        _isSelected = false;
    }

    /// <summary>
    /// Called every frame by Inventory.Update().
    /// When selected, checks for use-key input and raycasts to find a Dissolvable target.
    /// </summary>
    public override void OnInventoryUpdate()
    {
        if (!_isSelected || _cam == null) return;

        if (Input.GetKeyDown(useKey))
        {
            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, useRange))
            {
                if (hit.collider.CompareTag(dissolvableTag))
                {
                    Dissolve(hit.collider.gameObject);
                }
                else
                {
                    Debug.Log("[SuperAcidItem] This object can't be dissolved.");
                }
            }
        }
    }

    /// <summary>
    /// Show a prompt when aiming at a dissolvable object while acid is selected.
    /// </summary>
    void OnGUI()
    {
        if (!_isSelected || _cam == null) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, useRange)) return;
        if (!hit.collider.CompareTag(dissolvableTag)) return;

        if (_promptStyle == null)
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 1f, 0.5f) }
            };

        string msg = "[LMB] USE SUPER ACID";
        float pw = 400f, ph = 45f;
        float px = (Screen.width - pw) / 2f;
        float py = (Screen.height - ph) / 2f + 60f;

        GUI.color = Color.black;
        GUI.Label(new Rect(px + 2, py + 2, pw, ph), msg, _promptStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(px, py, pw, ph), msg, _promptStyle);
    }

    private void Dissolve(GameObject target)
    {
        Debug.Log($"[SuperAcidItem] Dissolving: {target.name}");

        // Spawn optional VFX
        if (dissolveVFXPrefab != null)
        {
            var vfx = Instantiate(dissolveVFXPrefab, target.transform.position, Quaternion.identity);
            Destroy(vfx, 5f);
        }

        // KEY FIX: Get or add a DissolvableBox on the TARGET so the coroutine
        // runs on the target object (which stays alive during dissolve).
        // If there's no DissolvableBox, add a generic helper.
        var dissolvable = target.GetComponent<DissolvableBox>();
        if (dissolvable != null)
        {
            // Use the DissolvableBox's own dissolve method (handles keycard reveal etc.)
            dissolvable.BeginDissolve(dissolveTime);
        }
        else
        {
            // Fallback: add a simple dissolve helper at runtime
            var helper = target.AddComponent<DissolveHelper>();
            helper.StartDissolve(dissolveTime);
        }

        // NOW consume this item from inventory (safe — coroutine runs on target, not on us)
        var inv = Object.FindFirstObjectByType<Inventory>();
        if (inv != null)
            inv.RemoveItem(this);
    }
}

/// <summary>
/// Lightweight runtime-added component for dissolving objects that
/// don't have a DissolvableBox script. Shrinks + fades + destroys.
/// </summary>
public class DissolveHelper : MonoBehaviour
{
    public void StartDissolve(float duration)
    {
        StartCoroutine(DissolveRoutine(duration));
    }

    private System.Collections.IEnumerator DissolveRoutine(float duration)
    {
        Vector3 origScale = transform.localScale;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        Color[] origColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            origColors[i] = renderers[i].material.color;
            SetMaterialTransparent(renderers[i].material);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.localScale = Vector3.Lerp(origScale, Vector3.zero, t);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = origColors[i];
                c.a = 1f - t;
                renderers[i].material.color = c;
            }
            yield return null;
        }

        Destroy(gameObject);
    }

    private void SetMaterialTransparent(Material mat)
    {
        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
    }
}