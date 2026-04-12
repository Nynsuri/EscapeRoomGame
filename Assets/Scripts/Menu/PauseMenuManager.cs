using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// PauseMenuManager — ESC toggles pause, but only when no puzzle is active.
///
/// HOW TO USE WITH PUZZLES:
///   Make every puzzle inherit from BasePuzzle instead of MonoBehaviour.
///   Call OpenPuzzle() when the puzzle UI opens, ClosePuzzle() when it closes.
///   Everything else is automatic — no manual wiring per puzzle needed.
///
/// SETUP:
///   Attach this script to a GameObject called "PauseManager" in your Game scene.
///   Assign all fields in the Inspector.
/// </summary>
[DefaultExecutionOrder(200)]
public class PauseMenuManager : MonoBehaviour
{
    // Inspector references 
    [Header("Panels (children of your Pause Canvas)")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Pause Menu Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Settings — Display Mode")]
    [SerializeField] private Button fullscreenButton;
    [SerializeField] private Button windowedButton;
    [SerializeField] private Button windowedBorderlessButton;
    [SerializeField] private Color activeButtonColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color inactiveButtonColor = new Color(0.3f, 0.3f, 0.3f);

    [Header("Settings — Resolution")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Header("Settings — Audio")]
    [SerializeField] private TMP_Dropdown audioDeviceDropdown;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string mixerVolumeParam = "MasterVolume";

    [Header("Settings — Mouse Sensitivity")]
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private float minSensitivity = 10f;
    [SerializeField] private float maxSensitivity = 500f;

    [Header("Settings — Back Button")]
    [SerializeField] private Button settingsBackButton;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // PlayerPrefs keys (must match SettingsManager.cs) 
    private const string PREF_FULLSCREEN_MODE = "DisplayMode";
    private const string PREF_RES_WIDTH = "ResolutionWidth";
    private const string PREF_RES_HEIGHT = "ResolutionHeight";
    private const string PREF_AUDIO_DEVICE = "AudioDevice";
    private const string PREF_VOLUME = "MasterVolume";
    private const string PREF_MOUSE_SENS = "MouseSensitivity";

    // Runtime state 
    private bool _isPaused = false;
    private Resolution[] _resolutions;
    private List<string> _audioDevices = new List<string>();
    private FullScreenMode _currentMode;
    private void Awake()
    {
        // Make sure both panels start hidden
        pauseMenuPanel?.SetActive(false);
        settingsPanel?.SetActive(false);

        // Wire up buttons so you don't have to do it in the Inspector for every scene
        resumeButton?.onClick.AddListener(Resume);
        settingsButton?.onClick.AddListener(OpenSettings);
        mainMenuButton?.onClick.AddListener(GoToMainMenu);
        settingsBackButton?.onClick.AddListener(CloseSettings);

        fullscreenButton?.onClick.AddListener(OnFullscreenClicked);
        windowedButton?.onClick.AddListener(OnWindowedClicked);
        windowedBorderlessButton?.onClick.AddListener(OnWindowedBorderlessClicked);

        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        if (audioDeviceDropdown != null)
            audioDeviceDropdown.onValueChanged.AddListener(OnAudioDeviceChanged);

        if (mouseSensitivitySlider != null)
            mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
    }

    private void Update()
    {
        if (BasePuzzle.AnyPuzzleActive) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // If inventory is open, close it instead of pausing
            var inventory = FindFirstObjectByType<Inventory>();
            if (inventory != null && inventory.IsOpen)
            {
                inventory.ForceClose();
                return;
            }

            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                CloseSettings();
                return;
            }

            if (_isPaused) Resume();
            else Pause();
        }
    }

    //  PAUSE / RESUME

    public void Pause()
    {
        if (BasePuzzle.AnyPuzzleActive) return;

        _isPaused = true;
        Time.timeScale = 0f;

        pauseMenuPanel?.SetActive(true);
        settingsPanel?.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Disable player and inventory input
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = false;

        var inv = FindFirstObjectByType<Inventory>();
        if (inv != null) inv.enabled = false;
    }

    public void Resume()
    {
        _isPaused = false;
        Time.timeScale = 1f;

        pauseMenuPanel?.SetActive(false);
        settingsPanel?.SetActive(false);

        // Re-enable player and inventory input
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = true;

        var inv = FindFirstObjectByType<Inventory>();
        if (inv != null) inv.enabled = true;

        // Restore cursor — locked unless inventory is open
        bool invOpen = inv != null && inv.IsOpen;
        Cursor.lockState = invOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = invOpen;
    }

    //  PANEL SWITCHING

    public void OpenSettings()
    {
        pauseMenuPanel?.SetActive(false);
        settingsPanel?.SetActive(true);

        // Populate/refresh settings controls each time the panel opens
        BuildResolutionDropdown();
        BuildAudioDeviceDropdown();
        LoadCurrentSettings();
    }

    public void CloseSettings()
    {
        settingsPanel?.SetActive(false);
        pauseMenuPanel?.SetActive(true);
    }

    public void GoToMainMenu()
    {
        // Always restore time before loading a new scene
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    //  DISPLAY MODE

    private void OnFullscreenClicked() => ApplyDisplayMode(FullScreenMode.ExclusiveFullScreen);
    private void OnWindowedClicked() => ApplyDisplayMode(FullScreenMode.Windowed);
    private void OnWindowedBorderlessClicked() => ApplyDisplayMode(FullScreenMode.FullScreenWindow);

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
        int savedW = PlayerPrefs.GetInt(PREF_RES_WIDTH, Screen.currentResolution.width);
        int savedH = PlayerPrefs.GetInt(PREF_RES_HEIGHT, Screen.currentResolution.height);
        int currentIndex = 0;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            var r = _resolutions[i];
            double hz = r.refreshRateRatio.value;
            options.Add($"{r.width} × {r.height}  @{hz:F0}Hz");
            if (r.width == savedW && r.height == savedH)
                currentIndex = i;
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.SetValueWithoutNotify(currentIndex);
        resolutionDropdown.RefreshShownValue();
    }

    private void OnResolutionChanged(int index)
    {
        if (_resolutions == null || index >= _resolutions.Length) return;
        var r = _resolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode, r.refreshRateRatio);
        PlayerPrefs.SetInt(PREF_RES_WIDTH, r.width);
        PlayerPrefs.SetInt(PREF_RES_HEIGHT, r.height);
        PlayerPrefs.Save();
    }

    //  AUDIO DEVICE

    private void BuildAudioDeviceDropdown()
    {
        if (audioDeviceDropdown == null) return;

        _audioDevices.Clear();
        string[] devices = GetOutputDeviceNames();

        audioDeviceDropdown.ClearOptions();
        var options = new List<string>();
        foreach (var d in devices) { _audioDevices.Add(d); options.Add(d); }
        audioDeviceDropdown.AddOptions(options);

        string savedDevice = PlayerPrefs.GetString(PREF_AUDIO_DEVICE, "");
        int savedIdx = _audioDevices.IndexOf(savedDevice);
        if (savedIdx >= 0)
        {
            audioDeviceDropdown.SetValueWithoutNotify(savedIdx);
            audioDeviceDropdown.RefreshShownValue();
        }
    }

    private void OnAudioDeviceChanged(int index)
    {
        if (index < 0 || index >= _audioDevices.Count) return;
        string deviceName = _audioDevices[index];

        var method = typeof(AudioSettings).GetMethod(
            "SetPreferredDeviceForAudioOutput",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        method?.Invoke(null, new object[] { deviceName });

        PlayerPrefs.SetString(PREF_AUDIO_DEVICE, deviceName);
        PlayerPrefs.Save();
    }

    private static string[] GetOutputDeviceNames()
    {
        var method = typeof(AudioSettings).GetMethod(
            "GetAvailableOutputDevices",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (method != null) return (string[])method.Invoke(null, null);
        return new string[] { "Default" };
    }

    //  VOLUME

    public void OnVolumeChanged(float value)
    {
        float dB = value > 0.0001f ? Mathf.Log10(value) * 20f : -80f;
        if (audioMixer != null) audioMixer.SetFloat(mixerVolumeParam, dB);
        PlayerPrefs.SetFloat(PREF_VOLUME, value);
        PlayerPrefs.Save();
    }

    public void OnMouseSensitivityChanged(float value)
    {
        PlayerPrefs.SetFloat(PREF_MOUSE_SENS, value);
        PlayerPrefs.Save();

        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.mouseSensitivity = value;
    }

    //  LOAD SAVED SETTINGS INTO UI

    private void LoadCurrentSettings()
    {
        _currentMode = (FullScreenMode)PlayerPrefs.GetInt(
            PREF_FULLSCREEN_MODE, (int)FullScreenMode.ExclusiveFullScreen);
        RefreshDisplayModeButtons();

        float vol = PlayerPrefs.GetFloat(PREF_VOLUME, 1f);
        if (volumeSlider != null)
            volumeSlider.SetValueWithoutNotify(vol);

        // Mouse sensitivity
        float sens = PlayerPrefs.GetFloat(PREF_MOUSE_SENS, 100f);
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.minValue = minSensitivity;
            mouseSensitivitySlider.maxValue = maxSensitivity;
            mouseSensitivitySlider.SetValueWithoutNotify(sens);
        }
    }

    //  CLEANUP — make sure time scale is restored if this object is destroyed

    private void OnDestroy()
    {
        if (_isPaused)
            Time.timeScale = 1f;
    }
}