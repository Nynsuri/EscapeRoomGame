using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// EndingTrigger — Unity 6000.3.9f1
///
/// Place on any GameObject in your GAME scene.
/// Call LoadEnding() when the game is over (timer ran out,
/// final puzzle solved, player reached the exit, etc).
///
/// It checks CollectibleManager to decide which ending to play,
/// writes the result to PlayerPrefs, then loads EndingScene.
///
/// EXAMPLES of when to call LoadEnding():
///
///   // From GameTimer when time runs out:
///   EndingTrigger.LoadEnding("EndingScene");
///
///   // From a trigger zone the player walks into:
///   void OnTriggerEnter(Collider other) {
///       if (other.CompareTag("Player"))
///           EndingTrigger.LoadEnding("EndingScene");
///   }
///
///   // From a final puzzle's onSolved UnityEvent — just call
///   // the method on this component via the Inspector event.
/// </summary>
public class EndingTrigger : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string endingSceneName = "EndingScene";

    [Header("Auto-trigger on Timer End")]
    [Tooltip("If true, this script listens to GameTimer and loads the ending when time runs out.")]
    [SerializeField] private bool listenToGameTimer = true;

    private static bool _triggered = false;

    private void Awake()
    {
        _triggered = false;
    }

    private void Start()
    {
        if (!listenToGameTimer) return;

        // Hook into GameTimer if present
        var timer = FindFirstObjectByType<GameTimer>();
        if (timer != null)
            timer.OnTimeUp += OnGameTimerExpired;
    }

    private void OnDestroy()
    {
        var timer = FindFirstObjectByType<GameTimer>();
        if (timer != null)
            timer.OnTimeUp -= OnGameTimerExpired;
    }

    private void OnGameTimerExpired() => LoadEnding(endingSceneName);

    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this to end the game and load the ending scene.
    /// Can be called from anywhere — static so no reference needed.
    /// </summary>
    public static void LoadEnding(string sceneName = "EndingScene")
    {
        if (_triggered) return;
        _triggered = true;

        // Decide ending based on collectibles
        bool goodEnding = CollectibleManager.Instance != null
                       && CollectibleManager.Instance.IsComplete;

        PlayerPrefs.SetInt(EndingManager.PREF_GOOD_ENDING, goodEnding ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log($"[EndingTrigger] Loading ending. Good={goodEnding}");

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Inspector button / UnityEvent target — calls LoadEnding with the
    /// scene name configured in the Inspector.
    /// </summary>
    public void TriggerEnding() => LoadEnding(endingSceneName);
}
