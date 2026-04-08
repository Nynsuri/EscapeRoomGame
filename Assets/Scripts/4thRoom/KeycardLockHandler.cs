using UnityEngine;

/// <summary>
/// KeycardLockHandler — Attach to a door or lock that the KeycardItem can open.
/// Tag the GameObject as "KeycardLock".
///
/// When the player uses the keycard on this object, Unlock() is called.
/// Hook up onUnlocked in the Inspector to open a door, play an animation, etc.
/// </summary>
public class KeycardLockHandler : MonoBehaviour
{
    [Header("Events")]
    public UnityEngine.Events.UnityEvent onUnlocked;

    [Header("Optional Door Animation")]
    [Tooltip("If assigned, the door will slide/rotate open")]
    public Transform doorTransform;
    public Transform doorTransform1;

    [Tooltip("Local position offset to move the door to (for sliding doors)")]
    public Vector3 doorOpenOffset = new Vector3(0f, 0f, 2f);
    public Vector3 doorOpenOffset1 = new Vector3(0f, 0f, 2f);

    [Tooltip("How fast the door opens")]
    public float doorOpenSpeed = 1.5f;

    private bool _unlocked = false;

    public void Unlock()
    {
        if (_unlocked) return;
        _unlocked = true;

        Debug.Log($"[KeycardLockHandler] {gameObject.name} unlocked!");

        onUnlocked?.Invoke();

        if (doorTransform != null)
            StartCoroutine(OpenDoor());
    }

    /// <summary>Called via SendMessage by KeycardItem as a fallback.</summary>
    public void OnKeycardUsed()
    {
        Unlock();
    }

    private System.Collections.IEnumerator OpenDoor()
    {
        Vector3 start = doorTransform.localPosition;
        Vector3 end = start + doorOpenOffset;
        Vector3 start1 = doorTransform1.localPosition;
        Vector3 end1 = start1 + doorOpenOffset1;
        float elapsed = 0f;
        float distance = doorOpenOffset.magnitude;
        float duration = distance / doorOpenSpeed;
        float distance1 = doorOpenOffset1.magnitude;


        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            doorTransform.localPosition = Vector3.Lerp(start, end, t);
            doorTransform1.localPosition = Vector3.Lerp(start1, end1, t);
            yield return null;
        }
        doorTransform.localPosition = end;
    }
}
