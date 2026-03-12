using UnityEngine;

/// <summary>
/// FlickerLight.cs — Unity 6000.3.9f1
/// Attach to the same GameObject as your Light component.
/// Simulates a broken/dying light with random flickers, stutters, and outages.
/// </summary>
public class FlickerLight : MonoBehaviour
{
    [Header("Base Settings")]
    [Tooltip("Normal brightness when light is stable")]
    public float baseIntensity = 1.5f;

    [Header("Flicker")]
    [Tooltip("How often random small flickers happen (per second)")]
    public float flickerRate = 0.05f;
    [Tooltip("How much intensity randomly varies each flicker")]
    public float flickerStrength = 0.4f;

    [Header("Stutter — quick rapid blinks")]
    [Tooltip("Chance per second of triggering a stutter burst")]
    [Range(0f, 1f)] public float stutterChance = 0.3f;
    [Tooltip("How many rapid blinks in one stutter burst")]
    public int stutterCount = 4;
    [Tooltip("Speed of each blink during a stutter")]
    public float stutterSpeed = 0.04f;

    [Header("Outage — light fully cuts out")]
    [Tooltip("Chance per second of a full outage")]
    [Range(0f, 1f)] public float outageChance = 0.08f;
    [Tooltip("Min/max seconds the light stays off during outage")]
    public Vector2 outageDuration = new Vector2(0.1f, 0.8f);

    // ── Private ───────────────────────────────────────────────────
    private Light _light;
    private float _flickerTimer;
    private bool  _isBusy; // true during stutter or outage coroutines

    void Awake()
    {
        _light = GetComponent<Light>();
        if (_light == null)
            _light = GetComponentInChildren<Light>();

        if (_light == null)
        {
            Debug.LogError("[FlickerLight] No Light component found!");
            enabled = false;
            return;
        }

        baseIntensity = _light.intensity; // inherit whatever is set in Inspector
    }

    void Update()
    {
        if (_isBusy) return;

        // ── Random outage ──
        if (Random.value < outageChance * Time.deltaTime)
        {
            StartCoroutine(DoOutage());
            return;
        }

        // ── Random stutter burst ──
        if (Random.value < stutterChance * Time.deltaTime)
        {
            StartCoroutine(DoStutter());
            return;
        }

        // ── Continuous small flicker ──
        _flickerTimer -= Time.deltaTime;
        if (_flickerTimer <= 0f)
        {
            _flickerTimer = flickerRate + Random.Range(-flickerRate * 0.5f, flickerRate * 0.5f);
            _light.intensity = baseIntensity + Random.Range(-flickerStrength, flickerStrength * 0.5f);
        }
    }

    // ── Full outage ───────────────────────────────────────────────
    System.Collections.IEnumerator DoOutage()
    {
        _isBusy = true;
        _light.intensity = 0f;

        float duration = Random.Range(outageDuration.x, outageDuration.y);
        yield return new WaitForSeconds(duration);

        // Flicker back on with a few quick blinks
        for (int i = 0; i < 3; i++)
        {
            _light.intensity = baseIntensity;
            yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));
            _light.intensity = 0f;
            yield return new WaitForSeconds(Random.Range(0.02f, 0.06f));
        }

        _light.intensity = baseIntensity;
        _isBusy = false;
    }

    // ── Rapid stutter burst ───────────────────────────────────────
    System.Collections.IEnumerator DoStutter()
    {
        _isBusy = true;

        for (int i = 0; i < stutterCount; i++)
        {
            _light.intensity = 0f;
            yield return new WaitForSeconds(stutterSpeed * Random.Range(0.5f, 1.5f));
            _light.intensity = baseIntensity + Random.Range(0f, flickerStrength);
            yield return new WaitForSeconds(stutterSpeed * Random.Range(0.5f, 1.5f));
        }

        _light.intensity = baseIntensity;
        _isBusy = false;
    }
}
