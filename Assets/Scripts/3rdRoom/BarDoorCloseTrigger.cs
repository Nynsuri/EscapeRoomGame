using UnityEngine;

/// <summary>
/// BarDoorCloseTrigger — place on a STATIC sibling of the door mesh.
/// BoxCollider (IsTrigger=true) + Rigidbody (IsKinematic=true, UseGravity=false)
/// Place just past the door on the room side.
/// Assign the door (BarRoomDoorUnlocker) manually in inspector.
/// </summary>
public class BarDoorCloseTrigger : MonoBehaviour
{
    [Tooltip("Drag the door GameObject (BarRoomDoorUnlocker) here")]
    public BarRoomDoorUnlocker door;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            door?.TriggerClose();
    }
}
