using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ShootingPuzzle -- Unity 6000.3.9f1
///
/// 3 stars, 3 circles, 5 X targets.
/// NORMAL:  exactly 2 stars + 2 circles + 1 X
/// SECRET:  exactly all 5 Xs, no stars or circles ever hit
///
/// Per-type overflow resets that type only.
/// If after a per-type reset the remaining combination can never reach
/// the normal solution, everything resets.
/// </summary>
public class ShootingPuzzle : MonoBehaviour
{
    [Header("All Targets in Scene")]
    public ShootingTarget[] allStars = new ShootingTarget[3];
    public ShootingTarget[] allCircles = new ShootingTarget[3];
    public ShootingTarget[] allXs = new ShootingTarget[5];

    [Header("Normal Solution Requirements")]
    public int requiredStars = 2;
    public int requiredCircles = 2;
    public int requiredXs = 1;

    [Header("On Normal Solved")]
    public UnityEvent onSolved;
    public GameObject rewardObject;

    [Header("On Secret Solved")]
    public UnityEvent onSecretSolved;
    public GameObject secretRewardObject;

    [Header("Symphony Note")]
    [Tooltip("Assign the SymphonyNote (Secret) GameObject — hidden at start, shown on secret solve")]
    public SymphonyNote secretSymphonyNote;

    // -- State -----------------------------------------------------------------
    private bool _solved = false;
    private bool _secretFailed = false;
    private int _starsHit = 0;
    private int _circlesHit = 0;
    private int _xsHit = 0;

    public bool IsSolved => _solved;

    void Start()
    {
        if (rewardObject != null) rewardObject.SetActive(false);
        if (secretRewardObject != null) secretRewardObject.SetActive(false);
    }

    // -- Called by every ShootingTarget on hit --------------------------------

    public void OnTargetHit(ShootingTarget target)
    {
        if (_solved) return;

        switch (GetCategory(target))
        {
            case TargetCategory.Star:
                _secretFailed = true;
                _starsHit++;
                if (_starsHit > requiredStars)
                {
                    ResetType(allStars, TargetCategory.Star);
                    CheckIfHopeless();
                    return;
                }
                // Hitting a star while already having too many Xs down = hopeless
                if (_xsHit > requiredXs)
                {
                    StartCoroutine(ResetAll());
                    return;
                }
                break;

            case TargetCategory.Circle:
                _secretFailed = true;
                _circlesHit++;
                if (_circlesHit > requiredCircles)
                {
                    ResetType(allCircles, TargetCategory.Circle);
                    CheckIfHopeless();
                    return;
                }
                // Hitting a circle while already having too many Xs down = hopeless
                if (_xsHit > requiredXs)
                {
                    StartCoroutine(ResetAll());
                    return;
                }
                break;

            case TargetCategory.X:
                _xsHit++;
                // If secret already failed, normal needs exactly requiredXs — more than that is hopeless
                if (_secretFailed && _xsHit > requiredXs)
                {
                    StartCoroutine(ResetAll());
                    return;
                }
                break;

            default:
                return;
        }

        CheckSolution();
    }

    // -- After resetting a type, check if normal solution is still reachable --
    void CheckIfHopeless()
    {
        // If secret failed AND we can no longer reach 2s+2c+1x, reset everything
        // (e.g. hit 3 stars, reset stars — but also already shot 2 circles and 2 Xs,
        //  now need 2 more stars but secret is gone too)
        if (_secretFailed)
        {
            // Normal still needs: requiredStars - starsHit more stars, etc.
            // But since we just reset a type to 0, it's still reachable unless
            // we have TOO MANY of another type that can't be undone
            bool xsAlreadyOverNormal = _xsHit > requiredXs;
            if (xsAlreadyOverNormal)
            {
                StartCoroutine(ResetAll());
            }
        }
    }

    // -- Solution check --------------------------------------------------------
    void CheckSolution()
    {
        // Secret: all 5 Xs, no stars or circles ever touched
        if (!_secretFailed && _xsHit == allXs.Length)
        {
            _solved = true;
            BarRoomState.OnGunPuzzleCompleted(secretCondition: true);
            ReturnGunToWorld();
            onSolved?.Invoke();
            onSecretSolved?.Invoke();
            if (rewardObject != null) rewardObject.SetActive(true);
            if (secretRewardObject != null) secretRewardObject.SetActive(true);
            if (secretSymphonyNote != null) secretSymphonyNote.Activate();
            Debug.Log("[ShootingPuzzle] SOLVED — SECRET! Note spawned.");
            return;
        }

        // Normal: EXACTLY 2 stars + 2 circles + 1 X
        if (_starsHit == requiredStars &&
            _circlesHit == requiredCircles &&
            _xsHit == requiredXs)
        {
            _solved = true;
            BarRoomState.OnGunPuzzleCompleted(secretCondition: false);
            ReturnGunToWorld();
            onSolved?.Invoke();
            if (rewardObject != null) rewardObject.SetActive(true);
            Debug.Log("[ShootingPuzzle] SOLVED — normal!");
            // Normal gun solve: no note here, bottle room handles normal syphons
        }
    }

    // -- Reset one type --------------------------------------------------------
    void ResetType(ShootingTarget[] targets, TargetCategory cat)
    {
        switch (cat)
        {
            case TargetCategory.Star: _starsHit = 0; break;
            case TargetCategory.Circle: _circlesHit = 0; break;
            case TargetCategory.X: _xsHit = 0; break;
        }
        StartCoroutine(ResetAfterDelay(targets));
    }

    IEnumerator ResetAfterDelay(ShootingTarget[] targets)
    {
        yield return new WaitForSeconds(0.3f);
        foreach (var t in targets)
            if (t != null) t.ResetTarget();
    }

    // -- Reset everything ------------------------------------------------------
    IEnumerator ResetAll()
    {
        _starsHit = 0;
        _circlesHit = 0;
        _xsHit = 0;
        _secretFailed = false;

        yield return new WaitForSeconds(0.4f);

        foreach (var t in allStars) if (t != null) { t.gameObject.SetActive(true); t.ResetTarget(); }
        foreach (var t in allCircles) if (t != null) { t.gameObject.SetActive(true); t.ResetTarget(); }
        foreach (var t in allXs) if (t != null) { t.gameObject.SetActive(true); t.ResetTarget(); }

        Debug.Log("[ShootingPuzzle] Full reset.");
    }

    // -- Gun return ------------------------------------------------------------
    void ReturnGunToWorld()
    {
        var inventory = FindFirstObjectByType<Inventory>();
        if (inventory == null) return;
        foreach (var item in inventory.Items)
            if (item is GunItem g) { g.ReturnToWorld(inventory); return; }
        Debug.LogWarning("[ShootingPuzzle] Gun not found in inventory.");
    }

    // -- Helpers ---------------------------------------------------------------
    enum TargetCategory { Star, Circle, X, Unknown }

    TargetCategory GetCategory(ShootingTarget t)
    {
        foreach (var s in allStars) if (s == t) return TargetCategory.Star;
        foreach (var c in allCircles) if (c == t) return TargetCategory.Circle;
        foreach (var x in allXs) if (x == t) return TargetCategory.X;
        return TargetCategory.Unknown;
    }
}