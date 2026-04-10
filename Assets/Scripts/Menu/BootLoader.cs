using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// BootLoader — lives in your very first scene (or use [RuntimeInitializeOnLoadMethod]).
/// Applies saved display + audio settings immediately on launch so the
/// window/resolution are correct before the main menu appears.
///
/// If you only have one scene add this as a component alongside MainMenuManager.
/// </summary>
public class BootLoader : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string     volumeParam = "MasterVolume";

    private void Awake()
    {
        // Apply persisted settings as early as possible
        SettingsManager.ApplySavedSettingsOnBoot(audioMixer, volumeParam);
    }
}
