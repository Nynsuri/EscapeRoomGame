using System.Collections;
using UnityEngine;

/// <summary>
/// VerticalSlidingDoor — Unity 6000.3.9f1
///
/// Upper panel slides UP, lower panel slides DOWN.
/// Trigger-based — uses two child trigger GameObjects.
///
/// ─── Hierarchy ───────────────────────────────────────────────────
///   DoorRoot                ← VerticalSlidingDoor (this script)
///   ├── PanelUpper          ← upper door mesh
///   ├── PanelLower          ← lower door mesh
///   ├── OpenTrigger         ← BoxCollider (IsTrigger=true)
///   │                          Rigidbody (IsKinematic=true, UseGravity=false)
///   │                          VerticalDoorOpenTrigger script
///   │                          Place on CORRIDOR side
///   └── CloseTrigger        ← BoxCollider (IsTrigger=true)
///                              Rigidbody (IsKinematic=true, UseGravity=false)
///                              VerticalDoorCloseTrigger script
///                              Place just past door on ROOM side
/// ─────────────────────────────────────────────────────────────────
/// </summary>
public class VerticalSlidingDoor : MonoBehaviour
{
    [Header("Door Panels")]
    public Transform panelUpper;
    public Transform panelLower;

    [Header("Panel Positions — LOCAL SPACE")]
    [Tooltip("Auto-captured on Awake from current panel positions")]
    public Vector3 upperClosedPosition;
    public Vector3 upperOpenPosition;
    public Vector3 lowerClosedPosition;
    public Vector3 lowerOpenPosition;

    [Tooltip("Re-capture closed positions from panel transforms on Awake")]
    public bool captureClosedOnAwake = true;

    [Header("Slide Settings")]
    public float slideDuration = 0.6f;

    [Header("Audio (optional)")]
    public AudioClip openSound;
    public AudioClip closeSound;

    // ── State ─────────────────────────────────────────────────────
    public enum DoorState { Closed, Opening, Open, Closing }
    public DoorState State { get; private set; } = DoorState.Closed;

    private Coroutine   _slideCoroutine;
    private AudioSource _audio;
    private bool        _done = false;

    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _audio = GetComponent<AudioSource>();

        if (captureClosedOnAwake)
        {
            if (panelUpper != null) upperClosedPosition = panelUpper.localPosition;
            if (panelLower != null) lowerClosedPosition = panelLower.localPosition;
        }
    }

    // ── Called by VerticalDoorOpenTrigger ─────────────────────────

    public void TriggerOpen()
    {
        if (_done) return;
        if (State == DoorState.Closed || State == DoorState.Closing)
            SetSlide(open: true);
    }

    // ── Called by VerticalDoorCloseTrigger ────────────────────────

    public void TriggerClose()
    {
        if (_done) return;
        StartCoroutine(CloseAndShutdown());
    }

    IEnumerator CloseAndShutdown()
    {
        _done = true;

        while (State == DoorState.Opening)
            yield return null;

        SetSlide(open: false);
        yield return new WaitForSeconds(slideDuration + 0.1f);

        foreach (var t in GetComponentsInChildren<VerticalDoorOpenTrigger>())
            t.gameObject.SetActive(false);
        foreach (var t in GetComponentsInChildren<VerticalDoorCloseTrigger>())
            t.gameObject.SetActive(false);

        enabled = false;
    }

    // ─────────────────────────────────────────────────────────────

    void SetSlide(bool open)
    {
        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(AnimateSlide(open));
    }

    IEnumerator AnimateSlide(bool open)
    {
        State = open ? DoorState.Opening : DoorState.Closing;
        PlaySound(open ? openSound : closeSound);

        Vector3 upperStart = panelUpper != null ? panelUpper.localPosition : Vector3.zero;
        Vector3 lowerStart = panelLower != null ? panelLower.localPosition : Vector3.zero;
        Vector3 upperEnd   = open ? upperOpenPosition  : upperClosedPosition;
        Vector3 lowerEnd   = open ? lowerOpenPosition  : lowerClosedPosition;

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            if (panelUpper != null) panelUpper.localPosition = Vector3.Lerp(upperStart, upperEnd, t);
            if (panelLower != null) panelLower.localPosition = Vector3.Lerp(lowerStart, lowerEnd, t);
            yield return null;
        }

        if (panelUpper != null) panelUpper.localPosition = upperEnd;
        if (panelLower != null) panelLower.localPosition = lowerEnd;

        State = open ? DoorState.Open : DoorState.Closed;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (_audio != null) _audio.PlayOneShot(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position);
    }

    void OnDrawGizmosSelected()
    {
        if (panelUpper != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.TransformPoint(upperClosedPosition),
                            transform.TransformPoint(upperOpenPosition));
            Gizmos.DrawWireSphere(transform.TransformPoint(upperOpenPosition), 0.08f);
        }
        if (panelLower != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.TransformPoint(lowerClosedPosition),
                            transform.TransformPoint(lowerOpenPosition));
            Gizmos.DrawWireSphere(transform.TransformPoint(lowerOpenPosition), 0.08f);
        }
    }
}
