using UnityEngine;

/// <summary>
/// AudioHelper — drop-in replacement for AudioSource.PlayClipAtPoint that applies
/// the same stable audio settings used by PianoPuzzle:
///   • No doppler (dopplerLevel = 0) — eliminates pitch wobble when player moves
///   • Logarithmic rolloff with 2 m flat zone — smooth distance falloff
///   • Full 3D blend (spatialBlend = 1)
///   • Bound to the project's AudioMixerGroup so volume is mixer-controlled
/// </summary>
public static class AudioHelper
{
    /// <summary>
    /// Play a one-shot clip at a world position with stable, mixer-bound settings.
    /// </summary>
    public static void Play(AudioClip clip, Vector3 position,
        UnityEngine.Audio.AudioMixerGroup mixerGroup = null)
    {
        if (clip == null) return;

        var go = new GameObject("_OneShot");
        go.transform.position = position;

        var src = go.AddComponent<AudioSource>();
        src.clip            = clip;
        src.spatialBlend    = 1f;
        src.rolloffMode     = AudioRolloffMode.Logarithmic;
        src.minDistance     = 2f;
        src.maxDistance     = 30f;
        src.dopplerLevel    = 0f;
        src.spread          = 0f;
        src.playOnAwake     = false;
        src.priority        = 128;

        if (mixerGroup != null)
            src.outputAudioMixerGroup = mixerGroup;

        src.Play();
        Object.Destroy(go, clip.length + 0.1f);
    }

    /// <summary>
    /// Configure an existing AudioSource with the stable piano-style settings.
    /// Call this in Awake() after GetComponent/AddComponent.
    /// Preserves spatialBlend so 2D sources (e.g. held gun) are not changed.
    /// </summary>
    public static void Configure(AudioSource src,
        UnityEngine.Audio.AudioMixerGroup mixerGroup,
        bool is3D = true)
    {
        if (src == null) return;

        if (is3D)
        {
            src.spatialBlend = 1f;
            src.rolloffMode  = AudioRolloffMode.Logarithmic;
            src.minDistance  = 2f;
            src.dopplerLevel = 0f;
            src.spread       = 0f;
        }
        else
        {
            src.dopplerLevel = 0f;
        }

        if (mixerGroup != null)
            src.outputAudioMixerGroup = mixerGroup;
    }
}
