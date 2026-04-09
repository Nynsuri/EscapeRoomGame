using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// PortalRing — Unity 6000.3.9f1
///
/// The ring mesh object spins. Triggers are on a SEPARATE sibling
/// GameObject that does NOT spin — both sit under the same parent.
///
/// ─── Hierarchy ───────────────────────────────────────────────────
///   PortalRoot  (empty, static)
///   ├── RingMesh            ← PortalRing script here, this spins
///   └── PortalTriggers      ← empty static GameObject
///       ├── SideATrigger    ← BoxCollider (IsTrigger=true)
///       │                      Rigidbody (IsKinematic=true, UseGravity=false)
///       │                      PortalSideTrigger (isSideA=true, assign ring)
///       └── SideBTrigger    ← BoxCollider (IsTrigger=true)
///                              Rigidbody (IsKinematic=true, UseGravity=false)
///                              PortalSideTrigger (isSideA=false, assign ring)
/// ─────────────────────────────────────────────────────────────────
/// </summary>
public class PortalRing : MonoBehaviour
{
    [Header("Spin")]
    public float spinSpeed = 360f;

    [Header("Side A — Corridor")]
    public Material sideASkybox;
    [Range(0f, 1f)] public float sideAReflectionIntensity = 0f;

    [Header("Side B — Room")]
    public Material sideBSkybox;
    [Range(0f, 1f)] public float sideBReflectionIntensity = 1f;

    [Header("Transition")]
    public float transitionDuration = 1.0f;

    [Header("Room Blockers (optional)")]
    public PortalBlocker[] roomBlockers;

    private Coroutine _transitionCoroutine;

    void Update()
    {
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);
    }

    public void OnSideTriggered(bool isSideA)
    {
        Material targetSkybox = isSideA ? sideASkybox : sideBSkybox;
        float targetReflection = isSideA ? sideAReflectionIntensity : sideBReflectionIntensity;

        if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = StartCoroutine(TransitionTo(targetSkybox, targetReflection));

        SetBlockers(blocking: isSideA);

        // Leaving room back to corridor — clear inventory
        if (isSideA)
            ClearInventory();
    }

    void ClearInventory()
    {
        var inventory = FindFirstObjectByType<Inventory>();
        if (inventory == null) return;

        // Collect all items into a temp list to avoid modifying while iterating
        var toRemove = new System.Collections.Generic.List<InventoryItem>();
        foreach (var item in inventory.Items)
            toRemove.Add(item);

        foreach (var item in toRemove)
            inventory.RemoveItem(item);
    }

    IEnumerator TransitionTo(Material skybox, float targetReflection)
    {
        if (skybox != null)
            RenderSettings.skybox = skybox;

        float start = RenderSettings.reflectionIntensity;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            RenderSettings.reflectionIntensity =
                Mathf.Lerp(start, targetReflection,
                           Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration));
            yield return null;
        }

        RenderSettings.reflectionIntensity = targetReflection;
        DynamicGI.UpdateEnvironment();
    }

    void SetBlockers(bool blocking)
    {
        if (roomBlockers == null) return;
        foreach (var b in roomBlockers)
            if (b != null) b.SetBlocking(blocking);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.4f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1.8f, 1.8f, 0.02f));
    }
}