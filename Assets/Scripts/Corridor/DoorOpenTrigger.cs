using UnityEngine;

/// <summary>
/// DoorOpenTrigger — attach to the corridor-side trigger child of SlidingDoors.
/// Requires: BoxCollider (IsTrigger=true) + Rigidbody (IsKinematic=true, UseGravity=false)
/// </summary>
public class DoorOpenTrigger : MonoBehaviour
{
    private ProximitySlidingDoors _doors;

    void Awake()
    {
        _doors = GetComponentInParent<ProximitySlidingDoors>();
        if (_doors == null)
            Debug.LogWarning("[DoorOpenTrigger] No ProximitySlidingDoors found in parent!");
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _doors?.TriggerOpen();
    }
}
