using UnityEngine;

/// <summary>
/// VerticalDoorCloseTrigger — room-side child of VerticalSlidingDoor.
/// BoxCollider (IsTrigger=true) + Rigidbody (IsKinematic=true, UseGravity=false)
/// Fires once when player passes through — door closes and shuts down permanently.
/// </summary>
public class VerticalDoorCloseTrigger : MonoBehaviour
{
    private VerticalSlidingDoor _door;

    void Awake()
    {
        _door = GetComponentInParent<VerticalSlidingDoor>();
        if (_door == null)
            Debug.LogWarning("[VerticalDoorCloseTrigger] No VerticalSlidingDoor found in parent!");
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _door?.TriggerClose();
    }
}
