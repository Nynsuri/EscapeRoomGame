using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// GameTimer — reads the chosen difficulty from PlayerPrefs and counts down.
/// Builds its own Canvas + UI at runtime, same pattern as FPSCounter.
/// Displays in the upper-left corner by default.
/// No prefab or manual Canvas setup needed — just attach to any GameObject.
/// </summary>
public class GameTimer : MonoBehaviour
{
    [Header("Display")]
    public int fontSize = 20;
    public Color timerColor = Color.white;
    public Color urgentColor = new Color(1f, 0.2f, 0.2f);  // under urgentThreshold
    public float urgentThreshold = 60f;                          // seconds
    public Color difficultyColor = new Color(0.6f, 0.8f, 1f);

    [Header("Scene Names")]
    [SerializeField] private string menuSceneName = "MainMenu";

    // Runtime state
    /// <summary>Fires when the countdown reaches zero. EndingTrigger subscribes to this.</summary>
    public event System.Action OnTimeUp;

    private float _timeRemaining;
    private bool _isUnlimited;
    private bool _isPaused;
    private string _difficultyName;

    // UI refs 
    private Text _timerText;
    private Text _difficultyText;

    private void Awake()
    {
        BuildUI();
    }

    private void Start()
    {
        _difficultyName = PlayerPrefs.GetString(MainMenuManager.PREF_DIFFICULTY_NAME, "Easy");
        _timeRemaining = PlayerPrefs.GetFloat(MainMenuManager.PREF_DIFFICULTY_TIME, 3600f);
        _isUnlimited = _timeRemaining <= 0f;

        if (_difficultyText != null)
            _difficultyText.text = _difficultyName.ToUpper();

        if (_isUnlimited && _timerText != null)
            _timerText.text = "\u221e";  // ∞

        Debug.Log($"[GameTimer] Difficulty: {_difficultyName}  |  Time: " +
                  (_isUnlimited ? "Unlimited" : $"{_timeRemaining}s"));
    }

    private void Update()
    {
        if (_isPaused || _isUnlimited) return;

        _timeRemaining -= Time.deltaTime;

        if (_timeRemaining <= 0f)
        {
            _timeRemaining = 0f;
            UpdateTimerUI();
            FireTimeUp();
            return;
        }

        UpdateTimerUI();
    }

    // UI builder

    private void BuildUI()
    {
        var cgo = new GameObject("GameTimerCanvas");
        DontDestroyOnLoad(cgo);
        var cv = cgo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 50;
        cgo.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ConstantPixelSize;

        float pad = 10f;

        // Difficulty label 
        var diffGO = new GameObject("DifficultyLabel", typeof(RectTransform), typeof(Text));
        diffGO.transform.SetParent(cgo.transform, false);
        var diffRT = diffGO.GetComponent<RectTransform>();
        diffRT.anchorMin = diffRT.anchorMax = new Vector2(0f, 1f);
        diffRT.pivot = new Vector2(0f, 1f);
        diffRT.sizeDelta = new Vector2(160f, 26f);
        diffRT.anchoredPosition = new Vector2(pad, -pad);
        _difficultyText = diffGO.GetComponent<Text>();
        _difficultyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _difficultyText.fontSize = fontSize - 4;
        _difficultyText.fontStyle = FontStyle.Bold;
        _difficultyText.alignment = TextAnchor.MiddleLeft;
        _difficultyText.color = difficultyColor;
        _difficultyText.text = "...";

        // Timer label 
        var timerGO = new GameObject("TimerLabel", typeof(RectTransform), typeof(Text));
        timerGO.transform.SetParent(cgo.transform, false);
        var timerRT = timerGO.GetComponent<RectTransform>();
        timerRT.anchorMin = timerRT.anchorMax = new Vector2(0f, 1f);
        timerRT.pivot = new Vector2(0f, 1f);
        timerRT.sizeDelta = new Vector2(160f, 32f);
        timerRT.anchoredPosition = new Vector2(pad, -pad - 26f);
        _timerText = timerGO.GetComponent<Text>();
        _timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _timerText.fontSize = fontSize;
        _timerText.fontStyle = FontStyle.Bold;
        _timerText.alignment = TextAnchor.MiddleLeft;
        _timerText.color = timerColor;
        _timerText.text = "--:--";
    }

    // UI update 

    private void UpdateTimerUI()
    {
        if (_timerText == null) return;

        int total = Mathf.CeilToInt(_timeRemaining);
        int minutes = total / 60;
        int seconds = total % 60;
        _timerText.text = $"{minutes:00}:{seconds:00}";
        _timerText.color = _timeRemaining < urgentThreshold ? urgentColor : timerColor;
    }

    // Events 

    private void FireTimeUp()
    {
        Debug.Log("[GameTimer] Time's up!");
        OnTimeUp?.Invoke();
        Invoke(nameof(ReturnToMenu), 3f);
    }

    private void ReturnToMenu() => SceneManager.LoadScene(menuSceneName);

    // Public API 

    public void PauseTimer() => _isPaused = true;
    public void ResumeTimer() => _isPaused = false;

    public void NotifyGameCompleted()
    {
        bool wasEasy = string.Equals(_difficultyName, "Easy",
                           StringComparison.OrdinalIgnoreCase);
        if (wasEasy && !MainMenuManager.IsUnlimitedUnlocked())
        {
            MainMenuManager.UnlockUnlimitedMode();
            Debug.Log("[GameTimer] Easy completed — Unlimited mode unlocked!");
        }
    }

    public static void NotifyEasyCompleted() => MainMenuManager.UnlockUnlimitedMode();

    public float TimeRemaining => _isUnlimited ? float.MaxValue : _timeRemaining;
    public bool IsUnlimited => _isUnlimited;
}