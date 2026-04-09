using UnityEngine;

/// <summary>
/// VerticalDoorOpenTrigger — corridor-side child of VerticalSlidingDoor.
/// BoxCollider (IsTrigger=true) + Rigidbody (IsKinematic=true, UseGravity=false)
/// </summary>
public class VerticalDoorOpenTrigger : MonoBehaviour
{
    private VerticalSlidingDoor _door;

    void Awake()
    {
        _door = GetComponentInParent<VerticalSlidingDoor>();
        if (_door == null)
            Debug.LogWarning("[VerticalDoorOpenTrigger] No VerticalSlidingDoor found in parent!");
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _door?.TriggerOpen();
    }
}
