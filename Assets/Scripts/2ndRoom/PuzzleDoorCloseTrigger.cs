using UnityEngine;

/// <summary>
/// PuzzleDoorCloseTrigger — attach to a child of the PuzzleDoorUnlocker GameObject.
/// Requires: BoxCollider (IsTrigger=true) + Rigidbody (IsKinematic=true, UseGravity=false)
/// Place just past the door on the room side.
/// Fires once when player passes through, then door closes and everything shuts down.
/// </summary>
public class PuzzleDoorCloseTrigger : MonoBehaviour
{
    private PuzzleDoorUnlocker _door;

    void Awake()
    {
        _door = GetComponentInParent<PuzzleDoorUnlocker>();
        if (_door == null)
            Debug.LogWarning("[PuzzleDoorCloseTrigger] No PuzzleDoorUnlocker found in parent!");
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _door?.TriggerClose();
    }
}
