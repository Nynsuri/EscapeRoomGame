using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// BarBottleRoomManager -- attach to any GameObject in the bar room.
/// Wire each BottlePlacementZone's onSolved event to OnZoneSolved() here.
/// When all zones are done, notifies BarRoomState and grants normal syphons.
/// </summary>
public class BarBottleRoomManager : MonoBehaviour
{
    [Header("Zones — wire each BottlePlacementZone.onSolved here")]
    [Tooltip("Total number of zones (bar + tables) that must be filled")]
    public int totalZones = 4; // e.g. 1 bar + 3 tables

    [Header("On All Zones Filled")]
    public UnityEvent onAllFilled;
    public GameObject rewardObject;

    [Header("Symphony Note")]
    [Tooltip("Assign the SymphonyNote (Normal) GameObject — hidden at start, shown when all bottles placed")]
    public SymphonyNote normalSymphonyNote;
    [Tooltip("Assign the SymphonyNote (Secret) GameObject — hidden when bottle puzzle completes (secret path locked)")]
    public SymphonyNote secretSymphonyNote;

    private int _filledCount = 0;
    private bool _done = false;

    void Start()
    {
        if (rewardObject != null) rewardObject.SetActive(false);
    }

    // Wire this to each BottlePlacementZone's onSolved UnityEvent in Inspector
    public void OnZoneSolved()
    {
        if (_done) return;
        _filledCount++;
        Debug.Log($"[BarBottleRoomManager] Zone filled {_filledCount}/{totalZones}");

        if (_filledCount >= totalZones)
        {
            _done = true;
            BarRoomState.OnBottlePuzzleCompleted();
            onAllFilled?.Invoke();
            if (rewardObject != null) rewardObject.SetActive(true);
            // Spawn the normal note — player must read it to unlock normal syphons
            if (normalSymphonyNote != null) normalSymphonyNote.Activate();
            // Hide the secret note — bottle puzzle completion locks the secret path permanently
            if (secretSymphonyNote != null) secretSymphonyNote.gameObject.SetActive(false);
            Debug.Log("[BarBottleRoomManager] All zones filled. Normal note spawned; secret note hidden.");
        }
    }
}