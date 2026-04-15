using System.Collections;
using UnityEngine;

/// <summary>
/// ProximitySlidingDoors — Unity 6000.3.9f1
///
/// Uses two child trigger GameObjects:
///   • OpenTrigger  — corridor side, opens the door when player enters
///   • CloseTrigger — room side, closes the door behind player and
///                    then PERMANENTLY disables this entire script
///
/// ─── Hierarchy ───────────────────────────────────────────────────
///   SlidingDoors            ← ProximitySlidingDoors (this script)
///   ├── PanelLeft
///   ├── PanelRight
///   ├── OpenTrigger         ← BoxCollider (IsTrigger=true)
///   │                          Rigidbody (IsKinematic=true, no gravity)
///   │                          DoorOpenTrigger script
///   └── CloseTrigger        ← BoxCollider (IsTrigger=true)
///                              Rigidbody (IsKinematic=true, no gravity)
///                              DoorCloseTrigger script
///
/// OpenTrigger  — place on CORRIDOR side in front of door
/// CloseTrigger — place just PAST the door on the ROOM side
/// ─────────────────────────────────────────────────────────────────
/// </summary>
public class ProximitySlidingDoors : MonoBehaviour
{
    [Header("Door Panels")]
    public Transform panelLeft;
    public Transform panelRight;

    [Header("Panel Positions — LOCAL SPACE")]
    [Tooltip("Auto-captured on Awake from current panel positions")]
    public Vector3 leftClosedPosition;
    public Vector3 leftOpenPosition;
    public Vector3 rightClosedPosition;
    public Vector3 rightOpenPosition;

    [Tooltip("Re-capture closed positions from panel transforms on next Awake")]
    public bool captureClosedOnAwake = true;

    [Header("Slide Settings")]
    public float slideDuration = 0.5f;

    [Header("Audio (optional)")]
    public AudioClip openSound;
    public AudioClip closeSound;

    [Header("Audio Mixer")]
    public UnityEngine.Audio.AudioMixerGroup audioMixerGroup;

    // ── State ─────────────────────────────────────────────────────
    public enum DoorState { Closed, Opening, Open, Closing }
    public DoorState State { get; private set; } = DoorState.Closed;

    private Coroutine _slideCoroutine;
    private AudioSource _audio;
    private bool _done = false;  // permanently shut down after close

    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        AudioHelper.Configure(_audio, audioMixerGroup);

        if (captureClosedOnAwake)
        {
            if (panelLeft != null) leftClosedPosition = panelLeft.localPosition;
            if (panelRight != null) rightClosedPosition = panelRight.localPosition;
        }
    }

    // ── Called by DoorOpenTrigger child ──────────────────────────

    public void TriggerOpen()
    {
        if (_done) return;
        if (State == DoorState.Closed || State == DoorState.Closing)
            SetSlide(open: true);
    }

    // ── Called by DoorCloseTrigger child ─────────────────────────

    public void TriggerClose()
    {
        if (_done) return;
        StartCoroutine(CloseAndShutdown());
    }

    IEnumerator CloseAndShutdown()
    {
        _done = true;

        // Wait for door to fully open first if still animating
        while (State == DoorState.Opening)
            yield return null;

        SetSlide(open: false);

        // Wait for close animation to finish
        while (State == DoorState.Closing)
            yield return null;

        // Disable child trigger objects so they never fire again
        foreach (var trigger in GetComponentsInChildren<DoorOpenTrigger>())
            trigger.gameObject.SetActive(false);
        foreach (var trigger in GetComponentsInChildren<DoorCloseTrigger>())
            trigger.gameObject.SetActive(false);

        // Disable this script entirely
        enabled = false;

        Debug.Log($"[ProximitySlidingDoors] '{gameObject.name}' shut down permanently.");
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

        Vector3 leftStart = panelLeft != null ? panelLeft.localPosition : Vector3.zero;
        Vector3 rightStart = panelRight != null ? panelRight.localPosition : Vector3.zero;
        Vector3 leftEnd = open ? leftOpenPosition : leftClosedPosition;
        Vector3 rightEnd = open ? rightOpenPosition : rightClosedPosition;

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            if (panelLeft != null) panelLeft.localPosition = Vector3.Lerp(leftStart, leftEnd, t);
            if (panelRight != null) panelRight.localPosition = Vector3.Lerp(rightStart, rightEnd, t);
            yield return null;
        }

        if (panelLeft != null) panelLeft.localPosition = leftEnd;
        if (panelRight != null) panelRight.localPosition = rightEnd;

        State = open ? DoorState.Open : DoorState.Closed;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (_audio != null) _audio.PlayOneShot(clip);
        else AudioHelper.Play(clip, transform.position, audioMixerGroup);
    }

    void OnDrawGizmosSelected()
    {
        if (panelLeft != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.TransformPoint(leftClosedPosition),
                            transform.TransformPoint(leftOpenPosition));
            Gizmos.DrawWireSphere(transform.TransformPoint(leftOpenPosition), 0.08f);
        }
        if (panelRight != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.TransformPoint(rightClosedPosition),
                            transform.TransformPoint(rightOpenPosition));
            Gizmos.DrawWireSphere(transform.TransformPoint(rightOpenPosition), 0.08f);
        }
    }
}


// DoorOpenTrigger and DoorCloseTrigger are in their own .cs files.