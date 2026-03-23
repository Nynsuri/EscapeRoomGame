using UnityEngine;

/// <summary>
/// AlwaysVisible.cs — forces renderers on until explicitly told to stop.
/// Attach to items that keep getting hidden by other scripts on scene start.
/// </summary>
public class AlwaysVisible : MonoBehaviour
{
    private bool _allowHide = false;

    // Call this when the item is actually picked up
    public void AllowHide() => _allowHide = true;

    void LateUpdate()
    {
        if (_allowHide) return;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = true;
    }
}