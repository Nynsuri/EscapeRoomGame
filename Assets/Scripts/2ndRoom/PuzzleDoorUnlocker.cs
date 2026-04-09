using System.Collections;
using UnityEngine;

/// <summary>
/// PuzzleDoorUnlocker — Unity 6000.3.9f1
///
/// Listens to the onSolved UnityEvents on all three puzzles.
/// When all three are solved the door opens automatically.
/// After the player passes through the CloseTrigger the door
/// closes and this script shuts down permanently.
///
/// ─── Setup ───────────────────────────────────────────────────────
///  1. Attach to the empty SlidingDoors parent GameObject.
///  2. Assign PanelLeft and PanelRight.
///  3. Set Left/Right Open Positions (local space).
///  4. Assign all three puzzles in the inspector.
///  5. On each puzzle's "On Solved" UnityEvent in the inspector,
///     add this GameObject and call:
///       PuzzleDoorUnlocker → OnPuzzleSolved()
///  6. Create a child "CloseTrigger" with:
///       BoxCollider (IsTrigger=true)
///       Rigidbody (IsKinematic=true, UseGravity=false)
///       PuzzleDoorCloseTrigger script
///     Place it just past the door on the room side.
/// ─────────────────────────────────────────────────────────────────
/// </summary>
public class PuzzleDoorUnlocker : MonoBehaviour
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

    [Tooltip("Re-capture closed positions from panel transforms on Awake")]
    public bool captureClosedOnAwake = true;

    [Header("Slide Settings")]
    public float slideDuration = 0.6f;

    [Header("Audio (optional)")]
    public AudioClip openSound;
    public AudioClip closeSound;

    [Header("HUD Message")]
    [Tooltip("Message shown when all puzzles are solved and door opens")]
    public string unlockedMessage = "All systems online — door unlocked!";
    public float messageDuration = 3f;

    // ── State ─────────────────────────────────────────────────────
    private bool _circuitSolved = false;
    private bool _hackingSolved = false;
    private bool _engineSolved = false;
    private bool _doorOpen = false;
    private bool _done = false;

    private Coroutine _slideCoroutine;
    private AudioSource _audio;

    private string _hudMessage = "";
    private float _hudMessageTimer = 0f;
    private GUIStyle _hudStyle;

    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _audio = GetComponent<AudioSource>();

        if (captureClosedOnAwake)
        {
            if (panelLeft != null) leftClosedPosition = panelLeft.localPosition;
            if (panelRight != null) rightClosedPosition = panelRight.localPosition;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Wire each of these to the matching puzzle's "On Solved" event:
    //   CircuitRepairPuzzle   → OnCircuitSolved
    //   HackingPuzzle         → OnHackingSolved
    //   EngineCalibratorPuzzle→ OnEngineSolved
    // ─────────────────────────────────────────────────────────────

    public void OnCircuitSolved()
    {
        if (_circuitSolved) return;
        _circuitSolved = true;
        ShowProgress();
        CheckAllSolved();
    }

    public void OnHackingSolved()
    {
        if (_hackingSolved) return;
        _hackingSolved = true;
        ShowProgress();
        CheckAllSolved();
    }

    public void OnEngineSolved()
    {
        if (_engineSolved) return;
        _engineSolved = true;
        ShowProgress();
        CheckAllSolved();
    }

    void ShowProgress()
    {
        int count = (_circuitSolved ? 1 : 0) + (_hackingSolved ? 1 : 0) + (_engineSolved ? 1 : 0);
        if (count < 3)
            ShowMessage($"Systems online: {count} / 3");
    }

    // ─────────────────────────────────────────────────────────────

    void CheckAllSolved()
    {
        if (_doorOpen || _done) return;
        if (!_circuitSolved || !_hackingSolved || !_engineSolved) return;

        Debug.Log("[PuzzleDoorUnlocker] All puzzles solved — opening door!");
        ShowMessage(unlockedMessage);
        SetSlide(open: true);
        _doorOpen = true;
    }

    // ── Called by PuzzleDoorCloseTrigger child ────────────────────

    public void TriggerClose()
    {
        if (_done || !_doorOpen) return;
        StartCoroutine(CloseAndShutdown());
    }

    IEnumerator CloseAndShutdown()
    {
        _done = true;

        SetSlide(open: false);

        // Wait for close to finish
        yield return new WaitForSeconds(slideDuration + 0.1f);

        // Disable close trigger
        foreach (var t in GetComponentsInChildren<PuzzleDoorCloseTrigger>())
            t.gameObject.SetActive(false);

        enabled = false;
        Debug.Log("[PuzzleDoorUnlocker] Shut down permanently.");
    }

    // ─────────────────────────────────────────────────────────────

    void SetSlide(bool open)
    {
        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(AnimateSlide(open));
    }

    IEnumerator AnimateSlide(bool open)
    {
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
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (_audio != null) _audio.PlayOneShot(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position);
    }

    // ── HUD ──────────────────────────────────────────────────────

    void ShowMessage(string msg)
    {
        _hudMessage = msg;
        _hudMessageTimer = messageDuration;
    }

    void Update()
    {
        if (_hudMessageTimer > 0f) _hudMessageTimer -= Time.deltaTime;
    }

    void OnGUI()
    {
        if (_hudMessageTimer <= 0f || string.IsNullOrEmpty(_hudMessage)) return;

        if (_hudStyle == null)
            _hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.green }
            };

        const float w = 600f, h = 44f;
        float x = (Screen.width - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f - 80f;

        GUI.color = Color.black;
        GUI.Label(new Rect(x + 2, y + 2, w, h), _hudMessage, _hudStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, w, h), _hudMessage, _hudStyle);
    }

    // ── Gizmos ────────────────────────────────────────────────────

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