using UnityEngine;

/// <summary>
/// SwingDoorCloseTrigger — place on a STATIC sibling of the door mesh.
/// Requires: BoxCollider (IsTrigger=true) + Rigidbody (IsKinematic=true, UseGravity=false)
/// Assign the DoorMesh (AutoSwingDoor) manually in the inspector.
/// </summary>
public class SwingDoorCloseTrigger : MonoBehaviour
{
    [Tooltip("Drag the DoorMesh GameObject (the one with AutoSwingDoor) here")]
    public AutoSwingDoor door;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            door?.TriggerClose();
    }
}