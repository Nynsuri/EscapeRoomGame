using UnityEngine;

/// <summary>
/// DoorCloseTrigger — attach to the room-side trigger child of SlidingDoors.
/// Requires: BoxCollider (IsTrigger=true) + Rigidbody (IsKinematic=true, UseGravity=false)
/// After firing once, the entire door system shuts down permanently.
/// </summary>
public class DoorCloseTrigger : MonoBehaviour
{
    private ProximitySlidingDoors _doors;

    void Awake()
    {
        _doors = GetComponentInParent<ProximitySlidingDoors>();
        if (_doors == null)
            Debug.LogWarning("[DoorCloseTrigger] No ProximitySlidingDoors found in parent!");
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _doors?.TriggerClose();
    }
}
