using System.Collections;
using UnityEngine;

/// <summary>
/// ShootingTarget -- Unity 6000.3.9f1
/// On hit: flashes red then disappears.
/// On reset (called by ShootingPuzzle when too many of a type are hit):
///   fades back in so the player can try again.
/// </summary>
public class ShootingTarget : MonoBehaviour
{
    [Header("Hit Reaction")]
    public float disappearDelay = 0.12f;

    [Header("Reset Reaction")]
    [Tooltip("How long the fade-in takes when target resets")]
    public float resetFadeDuration = 0.4f;

    [Header("Hit Colour Flash")]
    public Color hitColour = Color.red;
    public float hitColourDuration = 0.12f;

    [Header("Sound Effects")]
    public AudioClip hitSound;

    [Header("Auto Collider (added at runtime if none exists)")]
    public Vector3 colliderSize = new Vector3(0.5f, 0.5f, 0.5f);
    public Vector3 colliderCenter = new Vector3(0f, 0f, 0f);

    // -- State -----------------------------------------------------------------

    private bool _isHit = false;
    public bool IsHit => _isHit;

    private Renderer _renderer;
    private Collider _col;
    private Color _originalColour;

    // -- Unity -----------------------------------------------------------------

    void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null)
            _originalColour = _renderer.material.color;

        if (GetComponentInChildren<Collider>() == null)
        {
            _col = gameObject.AddComponent<BoxCollider>();
            ((BoxCollider)_col).size = colliderSize;
            ((BoxCollider)_col).center = colliderCenter;
        }
        else
        {
            _col = GetComponentInChildren<Collider>();
        }
    }

    // -- Public API ------------------------------------------------------------

    public void RegisterHit()
    {
        if (_isHit) return;
        _isHit = true;

        if (hitSound != null)
            AudioSource.PlayClipAtPoint(hitSound, transform.position);

        StopAllCoroutines();
        StartCoroutine(HitAndDisappear());

        foreach (var puzzle in FindObjectsByType<ShootingPuzzle>(FindObjectsSortMode.None))
            puzzle.OnTargetHit(this);

        Debug.Log($"[ShootingTarget] '{gameObject.name}' hit!");
    }

    /// Called by ShootingPuzzle when this type was overshot -- brings target back.
    public void ResetTarget()
    {
        _isHit = false;
        StopAllCoroutines();
        StartCoroutine(FadeBackIn());
        Debug.Log($"[ShootingTarget] '{gameObject.name}' reset.");
    }

    // -- Animations ------------------------------------------------------------

    IEnumerator HitAndDisappear()
    {
        if (_renderer != null)
            _renderer.material.color = hitColour;

        yield return new WaitForSeconds(disappearDelay);

        gameObject.SetActive(false);
    }

    IEnumerator FadeBackIn()
    {
        // Re-enable first
        gameObject.SetActive(true);
        if (_col != null) _col.enabled = false; // disable collider during fade so player can't re-hit mid-fade

        if (_renderer != null)
        {
            // Start transparent and fade to original colour
            Color startCol = new Color(_originalColour.r, _originalColour.g, _originalColour.b, 0f);
            _renderer.material.color = startCol;

            float t = 0f;
            while (t < resetFadeDuration)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / resetFadeDuration);
                _renderer.material.color = new Color(
                    _originalColour.r, _originalColour.g, _originalColour.b, a);
                yield return null;
            }

            _renderer.material.color = _originalColour;
        }

        if (_col != null) _col.enabled = true; // re-enable collider after fade
    }
}