using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

/// <summary>
/// SettingsManager — handles Display, Resolution, Audio Output device, and Volume.
/// Attach to the same "MenuManager" GameObject (or a child).
///
/// Requires:
///   • An AudioMixer with an exposed parameter named "MasterVolume"  (decibel float)
///   • Unity 2021.2 + (AudioSettings.GetConfiguration / Reset)
/// </summary>
public class SettingsManager : MonoBehaviour
{
    // PlayerPrefs keys 
    private const string PREF_FULLSCREEN_MODE = "DisplayMode";       // int (FullScreenMode)
    private const string PREF_RES_WIDTH = "ResolutionWidth";
    private const string PREF_RES_HEIGHT = "ResolutionHeight";
    private const string PREF_REFRESH_RATE = "RefreshRate";
    private const string PREF_AUDIO_DEVICE = "AudioDevice";
    private const string PREF_VOLUME = "MasterVolume";      // 0-1 float

    // Inspector references 
    [Header("Display Mode")]
    [SerializeField] private Button fullscreenButton;
    [SerializeField] private Button windowedButton;
    [SerializeField] private Button windowedBorderlessButton;
    [SerializeField] private Color activeButtonColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color inactiveButtonColor = new Color(0.3f, 0.3f, 0.3f);

    [Header("Resolution")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Header("Audio")]
    [SerializeField] private TMP_Dropdown audioDeviceDropdown;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private AudioMixer audioMixer;           // assign in Inspector
    [SerializeField] private string mixerVolumeParam = "MasterVolume";

    // State 
    private Resolution[] _resolutions;
    private List<string> _audioDevices = new();
    private FullScreenMode _currentMode;

    private void OnEnable()
    {
        // Populate controls every time the settings panel opens
        BuildResolutionDropdown();
        BuildAudioDeviceDropdown();
        LoadSavedSettings();
    }

    //  DISPLAY MODE

    public void OnFullscreenClicked() => ApplyDisplayMode(FullScreenMode.ExclusiveFullScreen);
    public void OnWindowedClicked() => ApplyDisplayMode(FullScreenMode.Windowed);
    public void OnWindowedBorderlessClicked() => ApplyDisplayMode(FullScreenMode.FullScreenWindow);

    private void ApplyDisplayMode(FullScreenMode mode)
    {
        _currentMode = mode;
        Screen.fullScreenMode = mode;
        PlayerPrefs.SetInt(PREF_FULLSCREEN_MODE, (int)mode);
        PlayerPrefs.Save();
        RefreshDisplayModeButtons();
    }

    private void RefreshDisplayModeButtons()
    {
        SetButtonColor(fullscreenButton, _currentMode == FullScreenMode.ExclusiveFullScreen);
        SetButtonColor(windowedButton, _currentMode == FullScreenMode.Windowed);
        SetButtonColor(windowedBorderlessButton, _currentMode == FullScreenMode.FullScreenWindow);
    }

    private void SetButtonColor(Button btn, bool active)
    {
        if (btn == null) return;
        var colors = btn.colors;
        colors.normalColor = active ? activeButtonColor : inactiveButtonColor;
        btn.colors = colors;
    }

    //  RESOLUTION

    private void BuildResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        _resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        var options = new List<string>();
        int currentIndex = 0;

        int savedW = PlayerPrefs.GetInt(PREF_RES_WIDTH, Screen.currentResolution.width);
        int savedH = PlayerPrefs.GetInt(PREF_RES_HEIGHT, Screen.currentResolution.height);

        for (int i = 0; i < _resolutions.Length; i++)
        {
            var r = _resolutions[i];
            // refreshRateRatio is a struct in Unity 2022+; use the double value
            double hz = r.refreshRateRatio.value;
            options.Add($"{r.width} × {r.height}  @{hz:F0}Hz");

            if (r.width == savedW && r.height == savedH)
                currentIndex = i;
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentIndex;
        resolutionDropdown.RefreshShownValue();

        resolutionDropdown.onValueChanged.RemoveAllListeners();
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    private void OnResolutionChanged(int index)
    {
        var r = _resolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode, r.refreshRateRatio);

        PlayerPrefs.SetInt(PREF_RES_WIDTH, r.width);
        PlayerPrefs.SetInt(PREF_RES_HEIGHT, r.height);
        // Store refresh as int numerator (close enough for restore)
        PlayerPrefs.SetInt(PREF_REFRESH_RATE, (int)r.refreshRateRatio.value);
        PlayerPrefs.Save();
    }

    //  AUDIO OUTPUT DEVICE

    private void BuildAudioDeviceDropdown()
    {
        if (audioDeviceDropdown == null) return;

        _audioDevices.Clear();

        // GetOutputDeviceNames uses pure reflection so it compiles on ALL Unity versions.
        // On Unity < 2023.1 it returns { "Default" }; on 2023.1+ it returns real device names.
        string[] devices = GetOutputDeviceNames();

        audioDeviceDropdown.ClearOptions();
        var options = new List<string>();

        foreach (var d in devices)
        {
            _audioDevices.Add(d);
            options.Add(d);
        }

        audioDeviceDropdown.AddOptions(options);

        // Restore saved device
        string savedDevice = PlayerPrefs.GetString(PREF_AUDIO_DEVICE, "");
        int savedIdx = _audioDevices.IndexOf(savedDevice);
        if (savedIdx >= 0)
        {
            audioDeviceDropdown.value = savedIdx;
            audioDeviceDropdown.RefreshShownValue();
        }

        audioDeviceDropdown.onValueChanged.RemoveAllListeners();
        audioDeviceDropdown.onValueChanged.AddListener(OnAudioDeviceChanged);
    }

    /// <summary>
    /// Unity 2023.1+ exposes AudioSettings.GetAvailableOutputDevices().
    /// For older versions this falls back gracefully to "Default".
    /// </summary>
    private static string[] GetOutputDeviceNames()
    {
        // Reflection-safe call so the script compiles on older Unity versions too
        var method = typeof(AudioSettings).GetMethod(
            "GetAvailableOutputDevices",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        if (method != null)
            return (string[])method.Invoke(null, null);

        return new string[] { "Default" };
    }

    private void OnAudioDeviceChanged(int index)
    {
        if (index < 0 || index >= _audioDevices.Count) return;

        string deviceName = _audioDevices[index];

        // Switch output device (Unity 2023.1+)
        var method = typeof(AudioSettings).GetMethod(
            "SetPreferredDeviceForAudioOutput",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        method?.Invoke(null, new object[] { deviceName });

        PlayerPrefs.SetString(PREF_AUDIO_DEVICE, deviceName);
        PlayerPrefs.Save();
    }

    //  VOLUME

    public void OnVolumeChanged(float value)
    {
        // Convert 0-1 slider to decibels (-80 dB silence → 0 dB full)
        float dB = value > 0.0001f ? Mathf.Log10(value) * 20f : -80f;

        if (audioMixer != null)
            audioMixer.SetFloat(mixerVolumeParam, dB);

        PlayerPrefs.SetFloat(PREF_VOLUME, value);
        PlayerPrefs.Save();
    }

    //  LOAD SAVED SETTINGS ON START

    private void LoadSavedSettings()
    {
        // --- Display mode ---
        _currentMode = (FullScreenMode)PlayerPrefs.GetInt(
            PREF_FULLSCREEN_MODE, (int)FullScreenMode.ExclusiveFullScreen);
        Screen.fullScreenMode = _currentMode;
        RefreshDisplayModeButtons();

        // --- Resolution ---
        int savedW = PlayerPrefs.GetInt(PREF_RES_WIDTH, Screen.currentResolution.width);
        int savedH = PlayerPrefs.GetInt(PREF_RES_HEIGHT, Screen.currentResolution.height);
        // Dropdown already set in BuildResolutionDropdown; apply to screen too
        Screen.SetResolution(savedW, savedH, _currentMode);

        // --- Volume ---
        float vol = PlayerPrefs.GetFloat(PREF_VOLUME, 1f);
        if (volumeSlider != null)
        {
            volumeSlider.value = vol;
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
        OnVolumeChanged(vol); // apply immediately to mixer
    }

    //  CALLED ON APP LAUNCH (from MainMenuManager.Awake or a separate
    //  BootLoader script) to apply settings before the menu even appears.
    public static void ApplySavedSettingsOnBoot(AudioMixer mixer, string volParam)
    {
        var mode = (FullScreenMode)PlayerPrefs.GetInt(
            PREF_FULLSCREEN_MODE, (int)FullScreenMode.ExclusiveFullScreen);

        int w = PlayerPrefs.GetInt(PREF_RES_WIDTH, Screen.currentResolution.width);
        int h = PlayerPrefs.GetInt(PREF_RES_HEIGHT, Screen.currentResolution.height);
        Screen.SetResolution(w, h, mode);

        float vol = PlayerPrefs.GetFloat(PREF_VOLUME, 1f);
        if (mixer != null)
        {
            float dB = vol > 0.0001f ? Mathf.Log10(vol) * 20f : -80f;
            mixer.SetFloat(volParam, dB);
        }
    }
}