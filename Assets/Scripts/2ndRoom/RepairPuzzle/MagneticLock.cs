using System.Collections;
using UnityEngine;

/// <summary>
/// MagneticLock.cs — attach to the BLACK PLATFORM the box sits on.
/// The red toolbox sits on top. When locked, the box can't be picked up.
/// HackingPuzzle.OnSolved calls Unlock() to release the magnetic lock.
/// </summary>
public class MagneticLock : MonoBehaviour
{
    [Header("The Box sitting on this platform")]
    [Tooltip("The red repair box GameObject — has ItemPickup on it")]
    public GameObject repairBox;


    private bool _unlocked = false;

    void Start()
    {
        // Disable pickup on the box until unlocked
        if (repairBox != null)
        {
            var pickup = repairBox.GetComponent<ItemPickup>();
            if (pickup != null) pickup.enabled = false;
        }

    }

    // Called by HackingPuzzle OnSolved event
    public void Unlock()
    {
        if (_unlocked) return;
        _unlocked = true;

        if (repairBox != null)
        {
            var pickup = repairBox.GetComponent<ItemPickup>();
            if (pickup != null) pickup.enabled = true;
        }

       
    }

    public bool IsUnlocked => _unlocked;

  
}