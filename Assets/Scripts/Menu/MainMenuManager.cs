using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// MainMenuManager — controls the root menu UI.
/// Attach to a persistent "MenuManager" GameObject in your Menu scene.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject difficultyPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Difficulty Buttons")]
    [SerializeField] private Button easyButton;
    [SerializeField] private Button mediumButton;
    [SerializeField] private Button hardButton;
    [SerializeField] private Button unlimitedButton;

    [Header("Unlimited Lock UI")]
    [SerializeField] private GameObject unlimitedLockIcon;   // Optional padlock overlay on the button
    [SerializeField] private string unlimitedUnlockKey = "UnlimitedUnlocked"; // PlayerPrefs key

    // ── Scene name of the actual game 
    [Header("Scene Names")]
    [SerializeField] private string gameSceneName = "GameScene";

    // ── Difficulty durations in seconds (0 = no limit) 
    private const float EASY_SECONDS      = 60f * 60f;   // 60 min
    private const float MEDIUM_SECONDS    = 30f * 60f;   // 30 min
    private const float HARD_SECONDS      = 15f * 60f;   // 15 min
    private const float UNLIMITED_SECONDS = 0f;          //  no limit

    // ── PlayerPrefs key shared with the game scene
    public static readonly string PREF_DIFFICULTY_TIME = "SelectedDifficultyTime";
    public static readonly string PREF_DIFFICULTY_NAME = "SelectedDifficultyName";

    
    private void Awake()
    {
        // Make sure only the main panel is visible on start
        ShowPanel(mainMenuPanel);
    }

    private void Start()
    {
        RefreshUnlimitedButton();
    }

    //  PUBLIC BUTTON CALLBACKS (wire these up in the Inspector)

    /// <summary>Called by the "Play" button.</summary>
    public void OnPlayClicked()
    {
        RefreshUnlimitedButton(); // re-check unlock state every time
        ShowPanel(difficultyPanel);
    }

    /// <summary>Called by the "Settings" button.</summary>
    public void OnSettingsClicked()
    {
        ShowPanel(settingsPanel);
    }

    /// <summary>Called by the "Quit" button.</summary>
    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Difficulty selection 

    public void OnEasyClicked()      => StartGame(EASY_SECONDS,      "Easy");
    public void OnMediumClicked()    => StartGame(MEDIUM_SECONDS,    "Medium");
    public void OnHardClicked()      => StartGame(HARD_SECONDS,      "Hard");
    public void OnUnlimitedClicked() => StartGame(UNLIMITED_SECONDS, "Unlimited");

    //  Back buttons (place on every sub-panel)

    public void OnDifficultyBackClicked() => ShowPanel(mainMenuPanel);
    public void OnSettingsBackClicked()   => ShowPanel(mainMenuPanel);

    //  PRIVATE HELPERS

    private void StartGame(float durationSeconds, string difficultyName)
    {
        // Persist difficulty so the game scene can read it
        PlayerPrefs.SetFloat(PREF_DIFFICULTY_TIME, durationSeconds);
        PlayerPrefs.SetString(PREF_DIFFICULTY_NAME, difficultyName);
        PlayerPrefs.Save();

        SceneManager.LoadScene(gameSceneName);
    }

    private void RefreshUnlimitedButton()
    {
        bool unlocked = PlayerPrefs.GetInt(unlimitedUnlockKey, 0) == 1;

        if (unlimitedButton != null)
            unlimitedButton.interactable = unlocked;

        if (unlimitedLockIcon != null)
            unlimitedLockIcon.SetActive(!unlocked);
    }

    private void ShowPanel(GameObject target)
    {
        mainMenuPanel?.SetActive(target == mainMenuPanel);
        difficultyPanel?.SetActive(target == difficultyPanel);
        settingsPanel?.SetActive(target == settingsPanel);
    }

    //  STATIC UTILITY — call from your Game scene when Easy is completed

    /// <summary>
    /// Call this from your game logic after the player finishes an Easy run.
    /// </summary>
    public static void UnlockUnlimitedMode()
    {
        PlayerPrefs.SetInt("UnlimitedUnlocked", 1);
        PlayerPrefs.Save();
        Debug.Log("[MainMenuManager] Unlimited mode unlocked!");
    }

    /// <summary>Returns true if Unlimited mode has been unlocked.</summary>
    public static bool IsUnlimitedUnlocked()
        => PlayerPrefs.GetInt("UnlimitedUnlocked", 0) == 1;
}
