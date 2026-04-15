using System.Collections;
using UnityEngine;

/// <summary>
/// DissolvableBox — Attach to the box that the Super Acid dissolves.
///
/// When BeginDissolve() is called (by SuperAcidItem), the box:
///   1. Turns green / acidic tint
///   2. Bubbles (jittery scale oscillation)
///   3. Shrinks and fades out
///   4. Reveals a keycard (or any item) hidden inside
///   5. Destroys itself
///
/// Setup:
///   1. Attach this to the black box GameObject.
///   2. Tag the box as "Dissolvable" (create the tag if needed).
///   3. Place the Keycard item as a CHILD of the box (or nearby) and assign
///      it to keycardInside in the Inspector.
///   4. The keycard should start DISABLED (SetActive false) so it's hidden.
///   5. The keycard needs a KeycardItem + ItemPickup so the player can pick it up.
/// </summary>
public class DissolvableBox : MonoBehaviour
{
    [Header("Keycard Inside")]
    [Tooltip("The keycard GameObject hidden inside. Starts disabled; enabled after dissolve.")]
    public GameObject keycardInside;

    [Header("Dissolve Visuals")]
    [Tooltip("Acid tint colour applied during dissolve")]
    public Color acidTint = new Color(0.1f, 0.9f, 0.15f, 1f);

    [Tooltip("How violently the box shakes during dissolve (0 = none)")]
    public float shakeIntensity = 0.03f;

    [Header("Audio (optional)")]
    public AudioClip dissolveSFX;

    [Header("Audio Mixer")]
    public UnityEngine.Audio.AudioMixerGroup audioMixerGroup;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onDissolveStart;
    public UnityEngine.Events.UnityEvent onDissolveComplete;

    private bool _dissolved = false;

    /// <summary>
    /// Called by SuperAcidItem when the player uses acid on this box.
    /// </summary>
    public void BeginDissolve(float duration)
    {
        if (_dissolved) return;
        _dissolved = true;

        // Unparent the keycard FIRST so it:
        //   - won't be affected by box scale/fade/shake
        //   - won't be destroyed when the box is destroyed
        //   - stays visible the entire time
        if (keycardInside != null)
        {
            keycardInside.transform.SetParent(null, true);
            keycardInside.SetActive(true);

            // Enable keycard collider so it can be picked up once the box is gone
            foreach (var col in keycardInside.GetComponentsInChildren<Collider>())
                col.enabled = true;
        }

        // Disable ONLY the box's own colliders (not the keycard's)
        foreach (var col in GetComponents<Collider>())
            col.enabled = false;
        // Also disable colliders on box children (mesh children etc.)
        // but keycard is already unparented so this is safe
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        onDissolveStart?.Invoke();

        if (dissolveSFX != null)
            AudioHelper.Play(dissolveSFX, transform.position, audioMixerGroup);

        StartCoroutine(DissolveRoutine(duration));
    }

    private IEnumerator DissolveRoutine(float duration)
    {
        Vector3 origScale = transform.localScale;
        Vector3 origPos   = transform.position;

        // Cache renderers and original colours
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        Color[] origColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            origColors[i] = renderers[i].material.color;
            SetMaterialTransparent(renderers[i].material);
        }

        float elapsed = 0f;

        // ── Phase 1: Acid reaction (first 40% of duration) ──────────────────
        // Box turns green, starts bubbling/shaking
        float phase1End = duration * 0.4f;

        while (elapsed < phase1End)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / phase1End; // 0 → 1 over phase 1

            // Tint towards acid green
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].material.color = Color.Lerp(origColors[i], acidTint, t);
            }

            // Shake / bubble
            float shake = shakeIntensity * t;
            transform.position = origPos + new Vector3(
                Random.Range(-shake, shake),
                Random.Range(-shake, shake) * 0.5f,
                Random.Range(-shake, shake));

            // Slight scale pulse (bubbling)
            float pulse = 1f + Mathf.Sin(elapsed * 25f) * 0.02f * t;
            transform.localScale = origScale * pulse;

            yield return null;
        }

        // ── Phase 2: Melt away (remaining 60% of duration) ─────────────────
        // Shrink + fade to nothing
        float phase2Duration = duration - phase1End;
        float phase2Elapsed = 0f;
        Vector3 phase2StartScale = transform.localScale;

        while (phase2Elapsed < phase2Duration)
        {
            phase2Elapsed += Time.deltaTime;
            float t = phase2Elapsed / phase2Duration; // 0 → 1 over phase 2

            // Shrink — squash vertically faster for a "melting" look
            float scaleXZ = Mathf.Lerp(1f, 0f, t);
            float scaleY  = Mathf.Lerp(1f, 0f, Mathf.Pow(t, 0.6f)); // melts down faster
            transform.localScale = new Vector3(
                origScale.x * scaleXZ,
                origScale.y * scaleY,
                origScale.z * scaleXZ);

            // Keep position grounded (don't float up as it shrinks)
            transform.position = origPos;

            // Fade out
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = acidTint;
                c.a = 1f - t;
                renderers[i].material.color = c;
            }

            // Continue shaking but diminishing
            float shake = shakeIntensity * (1f - t);
            transform.position = origPos + new Vector3(
                Random.Range(-shake, shake),
                0f,
                Random.Range(-shake, shake));

            yield return null;
        }

        // Keycard is already visible and unparented — nothing to reveal.
        onDissolveComplete?.Invoke();

        // Destroy the box (keycard was unparented in BeginDissolve, so it survives)
        Destroy(gameObject);
    }

    /// <summary>Switch Standard Shader to transparent mode at runtime.</summary>
    private void SetMaterialTransparent(Material mat)
    {
        // Standard Shader (Built-in)
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

        // URP Lit Shader
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // 0 = Opaque, 1 = Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha blend
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
        }
    }
}
