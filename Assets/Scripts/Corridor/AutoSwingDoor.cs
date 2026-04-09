using System.Collections;
using UnityEngine;

/// <summary>
/// AutoSwingDoor — Unity 6000.3.9f1
///
/// Triggers are on a SEPARATE sibling GameObject that never rotates.
///
/// ─── Hierarchy ───────────────────────────────────────────────────
///   DoorRoot    (empty, static — place at hinge position)
///   ├── DoorMesh            ← AutoSwingDoor script here, this rotates
///   └── DoorTriggers        ← empty static GameObject (never moves)
///       ├── OpenTrigger     ← BoxCollider (IsTrigger=true)
///       │                      Rigidbody (IsKinematic=true, UseGravity=false)
///       │                      SwingDoorOpenTrigger (assign DoorMesh)
///       └── CloseTrigger    ← BoxCollider (IsTrigger=true)
///                              Rigidbody (IsKinematic=true, UseGravity=false)
///                              SwingDoorCloseTrigger (assign DoorMesh)
/// ─────────────────────────────────────────────────────────────────
/// </summary>
public class AutoSwingDoor : MonoBehaviour
{
    [Header("Swing")]
    [Tooltip("Degrees to rotate on Y when open. -90 = opens into room.")]
    public float openAngle = -90f;

    [Tooltip("Seconds the swing animation takes")]
    public float swingDuration = 0.7f;

    [Header("Trigger References")]
    [Tooltip("Assign the OpenTrigger GameObject (sibling, not child)")]
    public GameObject openTriggerObject;
    [Tooltip("Assign the CloseTrigger GameObject (sibling, not child)")]
    public GameObject closeTriggerObject;

    [Header("Audio (optional)")]
    public AudioClip openSound;
    public AudioClip closeSound;

    // ── State ─────────────────────────────────────────────────────
    public enum DoorState { Closed, Opening, Open, Closing }
    public DoorState State { get; private set; } = DoorState.Closed;

    private Quaternion _closedRot;
    private Quaternion _openRot;
    private Coroutine _swingCoroutine;
    private AudioSource _audio;
    private bool _done = false;

    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        _closedRot = transform.localRotation;
        _openRot = _closedRot * Quaternion.Euler(0f, openAngle, 0f);
    }

    // ── Called by SwingDoorOpenTrigger ────────────────────────────

    public void TriggerOpen()
    {
        if (_done) return;
        if (State == DoorState.Closed || State == DoorState.Closing)
            Swing(open: true);
    }

    // ── Called by SwingDoorCloseTrigger ───────────────────────────

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

        Swing(open: false);

        yield return new WaitForSeconds(swingDuration + 0.1f);

        if (openTriggerObject != null) openTriggerObject.SetActive(false);
        if (closeTriggerObject != null) closeTriggerObject.SetActive(false);

        enabled = false;
    }

    // ─────────────────────────────────────────────────────────────

    void Swing(bool open)
    {
        if (_swingCoroutine != null) StopCoroutine(_swingCoroutine);
        _swingCoroutine = StartCoroutine(AnimateSwing(open));
    }

    IEnumerator AnimateSwing(bool open)
    {
        State = open ? DoorState.Opening : DoorState.Closing;
        PlaySound(open ? openSound : closeSound);

        Quaternion startRot = transform.localRotation;
        Quaternion endRot = open ? _openRot : _closedRot;

        float elapsed = 0f;
        while (elapsed < swingDuration)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(startRot, endRot,
                Mathf.SmoothStep(0f, 1f, elapsed / swingDuration));
            yield return null;
        }

        transform.localRotation = endRot;
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
        Quaternion closed = transform.localRotation;
        Quaternion open = closed * Quaternion.Euler(0f, openAngle, 0f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, closed * Vector3.forward * 1.2f);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, open * Vector3.forward * 1.2f);
    }
}