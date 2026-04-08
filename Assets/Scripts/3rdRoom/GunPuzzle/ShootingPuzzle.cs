using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ShootingPuzzle -- Unity 6000.3.9f1
///
/// Scene has: 3 stars, 3 circles, 5 X targets.
///
/// NORMAL solution:  exactly 2 stars + 2 circles + 1 X hit.
///                   If player hits a 3rd star or 3rd circle or 2nd X -> that type resets.
///
/// SECRET condition: ONLY the 5 X targets hit, nothing else at all.
///                   Hitting any star or circle invalidates the secret condition forever.
///                   Hitting more than 5 X is impossible since there are only 5.
///
/// Assign all targets in Inspector with correct TargetType on each ShootingTarget.
/// </summary>
public class ShootingPuzzle : MonoBehaviour
{
    [Header("All Targets in Scene")]
    [Tooltip("All 3 star targets")]
    public ShootingTarget[] allStars = new ShootingTarget[3];
    [Tooltip("All 3 circle targets")]
    public ShootingTarget[] allCircles = new ShootingTarget[3];
    [Tooltip("All 5 X targets")]
    public ShootingTarget[] allXs = new ShootingTarget[5];

    [Header("Normal Solution Requirements")]
    public int requiredStars = 2;
    public int requiredCircles = 2;
    public int requiredXs = 1;

    [Header("On Normal Solved")]
    public UnityEvent onSolved;
    public GameObject rewardObject;

    [Header("On Secret Solved (only Xs hit, all 5)")]
    public UnityEvent onSecretSolved;
    public GameObject secretRewardObject;

    // -- State -----------------------------------------------------------------

    private bool _solved = false;
    private bool _secretFailed = false; // true the moment any star or circle is hit

    // how many of each type are currently "down"
    private int _starsHit = 0;
    private int _circlesHit = 0;
    private int _xsHit = 0;

    public bool IsSolved => _solved;

    // -- Unity -----------------------------------------------------------------

    void Start()
    {
        if (rewardObject != null) rewardObject.SetActive(false);
        if (secretRewardObject != null) secretRewardObject.SetActive(false);
    }

    // -- Called by every ShootingTarget on hit --------------------------------

    public void OnTargetHit(ShootingTarget target)
    {
        if (_solved) return;

        TargetCategory cat = GetCategory(target);

        switch (cat)
        {
            case TargetCategory.Star:
                _secretFailed = true;
                _starsHit++;
                if (_starsHit > requiredStars)
                {
                    Debug.Log("[ShootingPuzzle] Too many stars hit -- resetting stars.");
                    ResetTargets(allStars, TargetCategory.Star);
                    return;
                }
                break;

            case TargetCategory.Circle:
                _secretFailed = true;
                _circlesHit++;
                if (_circlesHit > requiredCircles)
                {
                    Debug.Log("[ShootingPuzzle] Too many circles hit -- resetting circles.");
                    ResetTargets(allCircles, TargetCategory.Circle);
                    return;
                }
                break;

            case TargetCategory.X:
                _xsHit++;
                // No reset needed for X -- there are exactly 5 and max required is 5
                break;

            default:
                return;
        }

        CheckSolution();
    }

    // -- Solution check --------------------------------------------------------

    void CheckSolution()
    {
        // Secret: all 5 Xs hit and no stars/circles ever touched
        if (!_secretFailed && _xsHit == allXs.Length)
        {
            _solved = true;
            BarRoomState.OnGunPuzzleCompleted(secretCondition: true);
            ReturnGunToWorld();
            onSolved?.Invoke();
            onSecretSolved?.Invoke();
            if (rewardObject != null) rewardObject.SetActive(true);
            if (secretRewardObject != null) secretRewardObject.SetActive(true);
            Debug.Log("[ShootingPuzzle] SOLVED -- SECRET condition met!");
            return;
        }

        // Normal: exactly required stars + circles + Xs hit
        if (_starsHit == requiredStars &&
            _circlesHit == requiredCircles &&
            _xsHit >= requiredXs)
        {
            _solved = true;
            BarRoomState.OnGunPuzzleCompleted(secretCondition: false);
            ReturnGunToWorld();
            onSolved?.Invoke();
            if (rewardObject != null) rewardObject.SetActive(true);
            Debug.Log("[ShootingPuzzle] SOLVED -- normal condition met.");
        }
    }

    void ReturnGunToWorld()
    {
        var inventory = FindFirstObjectByType<Inventory>();
        if (inventory == null) return;

        // Find the GunItem in inventory
        GunItem gun = null;
        foreach (var item in inventory.Items)
        {
            if (item is GunItem g) { gun = g; break; }
        }

        if (gun != null)
            gun.ReturnToWorld(inventory);
        else
            Debug.LogWarning("[ShootingPuzzle] Gun not found in inventory on solve.");
    }

    // -- Reset a target type ---------------------------------------------------

    void ResetTargets(ShootingTarget[] targets, TargetCategory cat)
    {
        // Zero the counter immediately so new hits count fresh
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
        {
            if (t == null) continue;
            t.ResetTarget();
        }
        Debug.Log("[ShootingPuzzle] Targets reset and back up.");
    }

    // -- Category helper -------------------------------------------------------

    enum TargetCategory { Star, Circle, X, Unknown }

    TargetCategory GetCategory(ShootingTarget t)
    {
        foreach (var s in allStars) if (s == t) return TargetCategory.Star;
        foreach (var c in allCircles) if (c == t) return TargetCategory.Circle;
        foreach (var x in allXs) if (x == t) return TargetCategory.X;
        return TargetCategory.Unknown;
    }
}