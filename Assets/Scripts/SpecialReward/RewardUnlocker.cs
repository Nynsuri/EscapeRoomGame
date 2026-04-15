using System.Collections;
using UnityEngine;

public class RewardUnlocker : MonoBehaviour
{
    [Header("Door (floor hatch)")]
    public Transform door;
    public DoorOpenMode doorOpenMode = DoorOpenMode.SlideDown;
    public float doorSlideDistance = 1.5f;
    public float doorRotateDegrees = 90f;
    public Vector3 doorRotateAxis = Vector3.right;
    public float doorOpenDuration = 1.2f;
    public float doorCloseDuration = 1.2f;

    [Header("Rising Table")]
    public Transform table;
    public float tableTargetY = 0.75f;
    public float tableRiseDuration = 2.0f;
    public float tableRiseDelay = 0.5f;

    [Header("Audio (optional)")]
    public AudioClip doorOpenSound;
    public AudioClip doorCloseSound;
    public AudioClip tableRiseSound;

    [Header("Audio Mixer")]
    public UnityEngine.Audio.AudioMixerGroup audioMixerGroup;

    public enum DoorOpenMode { SlideDown, SlideUp, SlideAside, Rotate }

    private bool _triggered = false;
    private AudioSource _audio;
    private Vector3 _doorClosedPos;
    private Quaternion _doorClosedRot;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        AudioHelper.Configure(_audio, audioMixerGroup);
        if (door != null)
        {
            _doorClosedPos = door.position;
            _doorClosedRot = door.rotation;
        }
    }

    void Update()
    {
        if (_triggered) return;
        if (CollectibleManager.Instance != null && CollectibleManager.Instance.IsComplete)
            Trigger();
    }

    public void Trigger()
    {
        if (_triggered) return;
        _triggered = true;
        StartCoroutine(OpenSequence());
    }

    IEnumerator OpenSequence()
    {
        // 1. Open door
        if (door != null)
        {
            PlaySound(doorOpenSound, door.position);
            yield return StartCoroutine(AnimateDoor(opening: true));
        }

        // 2. Wait then raise table
        yield return new WaitForSeconds(tableRiseDelay);

        if (table != null)
        {
            yield return StartCoroutine(AnimateTableRise());
        }

        // 3. Close door back
        if (door != null)
        {
            yield return new WaitForSeconds(0.3f);
            PlaySound(doorCloseSound, door.position);
            yield return StartCoroutine(AnimateDoor(opening: false));
        }
    }

    IEnumerator AnimateDoor(bool opening)
    {
        Vector3 startPos = door.position;
        Quaternion startRot = door.rotation;
        Vector3 endPos;
        Quaternion endRot;

        if (opening)
        {
            switch (doorOpenMode)
            {
                case DoorOpenMode.SlideDown: endPos = _doorClosedPos + Vector3.down * doorSlideDistance; endRot = _doorClosedRot; break;
                case DoorOpenMode.SlideUp: endPos = _doorClosedPos + Vector3.up * doorSlideDistance; endRot = _doorClosedRot; break;
                case DoorOpenMode.SlideAside: endPos = _doorClosedPos + door.right * doorSlideDistance; endRot = _doorClosedRot; break;
                case DoorOpenMode.Rotate: endPos = _doorClosedPos; endRot = Quaternion.AngleAxis(doorRotateDegrees, doorRotateAxis) * _doorClosedRot; break;
                default: endPos = _doorClosedPos; endRot = _doorClosedRot; break;
            }
        }
        else
        {
            endPos = _doorClosedPos;
            endRot = _doorClosedRot;
        }

        float duration = opening ? doorOpenDuration : doorCloseDuration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            door.position = Vector3.Lerp(startPos, endPos, t);
            door.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        door.position = endPos;
        door.rotation = endRot;
    }

    IEnumerator AnimateTableRise()
    {
        Vector3 startPos = table.position;
        Vector3 endPos = new Vector3(table.position.x, tableTargetY, table.position.z);
        PlaySound(tableRiseSound, table.position);
        float elapsed = 0f;
        while (elapsed < tableRiseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / tableRiseDuration);
            table.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        table.position = endPos;
    }

    void PlaySound(AudioClip clip, Vector3 pos)
    {
        if (clip == null) return;
        if (_audio != null) _audio.PlayOneShot(clip);
        else AudioHelper.Play(clip, pos, audioMixerGroup);
    }

    void OnDrawGizmosSelected()
    {
        if (table == null) return;
        Gizmos.color = Color.yellow;
        Vector3 target = new Vector3(table.position.x, tableTargetY, table.position.z);
        Gizmos.DrawWireCube(target, new Vector3(1f, 0.05f, 1f));
        Gizmos.DrawLine(table.position, target);
    }
}