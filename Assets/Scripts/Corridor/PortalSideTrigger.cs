using UnityEngine;

/// <summary>
/// PortalSideTrigger — attach to SideATrigger and SideBTrigger children.
/// Requires: BoxCollider (IsTrigger=true) + Rigidbody (IsKinematic=true, UseGravity=false)
///
/// IMPORTANT: Assign the PortalRing manually in the inspector because
/// this object gets detached from the ring at runtime and
/// GetComponentInParent won't work after detachment.
/// </summary>
public class PortalSideTrigger : MonoBehaviour
{
    [Tooltip("Drag the PortalRing GameObject here")]
    public PortalRing portalRing;

    [Tooltip("ON = corridor side (Side A). OFF = room side (Side B).")]
    public bool isSideA = true;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            portalRing?.OnSideTriggered(isSideA);
    }
}