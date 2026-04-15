using System.Collections;
using UnityEngine;

/// <summary>
/// BarRoomDoorUnlocker — Unity 6000.3.9f1
///
/// Opens a swing door when ShootingPuzzle, BottlePlacementZone,
/// and PianoPuzzle are all solved.
///
/// ─── Setup ───────────────────────────────────────────────────────
///  1. Attach to the DOOR MESH GameObject (pivot at hinge edge).
///     If pivot is centered, wrap in empty parent at hinge and
///     attach here instead.
///  2. Set Open Angle (-90 or 90 depending on swing direction).
///  3. Wire each puzzle's onSolved UnityEvent:
///       ShootingPuzzle    → onSolved      → OnShootingSolved()
///       BottlePlacementZone→ onSolved     → OnBottleSolved()
///       PianoPuzzle       → onNormalSolved→ OnPianoSolved()
///                         → onSecretSolved→ OnPianoSolved()  (wire both)
///  4. Optionally create a CloseTrigger child:
///       BoxCollider (IsTrigger=true)
///       Rigidbody (IsKinematic=true, UseGravity=false)
///       BarDoorCloseTrigger script
///     Place just past door on room side — closes after player enters.
/// ─────────────────────────────────────────────────────────────────
/// </summary>
public class BarRoomDoorUnlocker : MonoBehaviour
{
    [Header("Swing")]
    [Tooltip("Degrees on Y axis. -90 or 90 depending on which way door opens.")]
    public float openAngle = -90f;
    public float swingDuration = 0.8f;

    [Header("Close Trigger Reference")]
    [Tooltip("Assign the CloseTrigger GameObject so it can be disabled after use")]
    public GameObject closeTriggerObject;

    [Header("HUD Message")]
    public string unlockedMessage = "The way is open.";
    public float messageDuration = 3f;

    [Header("Audio (optional)")]
    public AudioClip openSound;
    public AudioClip closeSound;

    [Header("Audio Mixer")]
    public UnityEngine.Audio.AudioMixerGroup audioMixerGroup;

    // ── State ─────────────────────────────────────────────────────
    private bool _shootingSolved = false;
    private bool _bottleSolved   = false;
    private bool _pianoSolved    = false;
    private bool _doorOpen       = false;
    private bool _done           = false;

    private Quaternion  _closedRot;
    private Quaternion  _openRot;
    private Coroutine   _swingCoroutine;
    private AudioSource _audio;

    private string   _hudMessage      = "";
    private float    _hudMessageTimer = 0f;
    private GUIStyle _hudStyle;

    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _audio     = GetComponent<AudioSource>();
        AudioHelper.Configure(_audio, audioMixerGroup);
        _closedRot = transform.localRotation;
        _openRot   = _closedRot * Quaternion.Euler(0f, openAngle, 0f);
    }

    void Update()
    {
        if (_hudMessageTimer > 0f) _hudMessageTimer -= Time.deltaTime;
    }

    // ── Wire these to puzzle onSolved events ─────────────────────

    public void OnShootingSolved()
    {
        if (_shootingSolved) return;
        _shootingSolved = true;
        ShowProgress();
        CheckAllSolved();
    }

    public void OnBottleSolved()
    {
        if (_bottleSolved) return;
        _bottleSolved = true;
        ShowProgress();
        CheckAllSolved();
    }

    public void OnPianoSolved()
    {
        if (_pianoSolved) return;
        _pianoSolved = true;
        ShowProgress();
        CheckAllSolved();
    }

    // ─────────────────────────────────────────────────────────────

    void ShowProgress()
    {
        int count = (_shootingSolved ? 1 : 0) + (_bottleSolved ? 1 : 0) + (_pianoSolved ? 1 : 0);
        if (count < 3)
            ShowMessage($"Puzzles solved: {count} / 3");
    }

    void CheckAllSolved()
    {
        if (_doorOpen || _done) return;
        if (!_shootingSolved || !_bottleSolved || !_pianoSolved) return;

        _doorOpen = true;
        ShowMessage(unlockedMessage);
        Swing(open: true);
    }

    // ── Called by BarDoorCloseTrigger ─────────────────────────────

    public void TriggerClose()
    {
        if (_done || !_doorOpen) return;
        StartCoroutine(CloseAndShutdown());
    }

    IEnumerator CloseAndShutdown()
    {
        _done = true;

        while (_swingCoroutine != null && _doorOpen)
            yield return null;

        Swing(open: false);
        yield return new WaitForSeconds(swingDuration + 0.1f);

        if (closeTriggerObject != null)
            closeTriggerObject.SetActive(false);

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
        PlaySound(open ? openSound : closeSound);

        Quaternion startRot = transform.localRotation;
        Quaternion endRot   = open ? _openRot : _closedRot;
        float elapsed = 0f;

        while (elapsed < swingDuration)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(startRot, endRot,
                Mathf.SmoothStep(0f, 1f, elapsed / swingDuration));
            yield return null;
        }

        transform.localRotation = endRot;
        _swingCoroutine = null;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (_audio != null) _audio.PlayOneShot(clip);
        else AudioHelper.Play(clip, transform.position, audioMixerGroup);
    }

    void ShowMessage(string msg)
    {
        _hudMessage      = msg;
        _hudMessageTimer = messageDuration;
    }

    void OnGUI()
    {
        if (_hudMessageTimer <= 0f || string.IsNullOrEmpty(_hudMessage)) return;
        if (_hudStyle == null)
            _hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.green }
            };

        const float w = 500f, h = 44f;
        float x = (Screen.width  - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f - 80f;
        GUI.color = Color.black;
        GUI.Label(new Rect(x + 2, y + 2, w, h), _hudMessage, _hudStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(x,     y,     w, h), _hudMessage, _hudStyle);
    }

    void OnDrawGizmosSelected()
    {
        Quaternion closed = transform.localRotation;
        Quaternion open   = closed * Quaternion.Euler(0f, openAngle, 0f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, closed * Vector3.forward * 1.2f);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, open   * Vector3.forward * 1.2f);
    }
}
