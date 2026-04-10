using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// EndingManager — Unity 6000.3.9f1
///
/// Attach to a persistent GameObject in your Ending scene.
/// Reads whether the player earned the good ending via PlayerPrefs
/// (set by EndingTrigger in the game scene before loading this scene).
///
/// SCENE SETUP:
///   1. Create a new scene called "EndingScene".
///   2. Add a Canvas with ScreenSpaceOverlay.
///   3. Inside the Canvas create:
///        - GoodEndingPanel  (full-screen black panel + text)
///        - BadEndingPanel   (full-screen black panel + text)
///   4. Attach this script to an empty GameObject "EndingManager".
///   5. Assign all references in the Inspector.
///   6. Add "EndingScene" to Build Settings.
///
/// TRIGGERING:
///   Call EndingTrigger.LoadEnding() from your game scene when the
///   game ends (e.g. timer runs out, final puzzle solved, etc).
///   EndingTrigger checks CollectibleManager.Instance.IsComplete
///   and writes the result to PlayerPrefs before loading this scene.
/// </summary>
public class EndingManager : MonoBehaviour
{
    // PlayerPrefs key written by EndingTrigger
    public const string PREF_GOOD_ENDING = "EarnedGoodEnding";

    [Header("Panels")]
    [SerializeField] private GameObject goodEndingPanel;
    [SerializeField] private GameObject badEndingPanel;

    [Header("Good Ending Text")]
    [SerializeField] private TMP_Text goodTitleText;
    [SerializeField] private TMP_Text goodBodyText;
    [SerializeField] private TMP_Text goodSubtitleText;  // e.g. "Unlimited mode unlocked!"

    [Header("Bad Ending Text")]
    [SerializeField] private TMP_Text badTitleText;
    [SerializeField] private TMP_Text badBodyText;
    [SerializeField] private TMP_Text badCountdownText;   // inside BadEndingPanel

    [Header("Shared")]
    [SerializeField] private TMP_Text goodCountdownText;  // inside GoodEndingPanel
    [SerializeField] private Image fadeImage;          // full-screen black Image for fade

    [Header("Timing")]
    [SerializeField] private float goodEndingDuration = 25f;  // seconds before returning to menu
    [SerializeField] private float badEndingDuration = 20f;
    [SerializeField] private float fadeDuration = 1.5f;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Ending Text Content")]
    [TextArea(3, 6)]
    [SerializeField] private string goodTitle = "FREEDOM";
    [TextArea(3, 10)]
    [SerializeField]
    private string goodBody =
        "With all relics returned to their pedestals, the ancient mechanism stirs to life.\n\n" +
        "The sealed door dissolves. Beyond it — daylight.\n\n" +
        "You step through. You are free.";
    [TextArea(1, 3)]
    [SerializeField] private string goodUnlockText = "✦  Unlimited mode unlocked  ✦";

    [TextArea(3, 6)]
    [SerializeField] private string badTitle = "TRAPPED";
    [TextArea(3, 10)]
    [SerializeField]
    private string badBody =
        "The relics remain scattered. The mechanism stays silent.\n\n" +
        "The walls close in. The lights go out.\n\n" +
        "Not every story ends the way you hoped.";


    private void Start()
    {
        // Restore time scale in case the game paused before transitioning
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        bool goodEnding = PlayerPrefs.GetInt(PREF_GOOD_ENDING, 0) == 1;

        if (goodEnding)
        {
            // Unlock unlimited mode
            MainMenuManager.UnlockUnlimitedMode();
            StartCoroutine(PlayGoodEnding());
        }
        else
        {
            StartCoroutine(PlayBadEnding());
        }
    }

    //  GOOD ENDING

    private IEnumerator PlayGoodEnding()
    {
        // Set up text
        if (goodTitleText != null) goodTitleText.text = goodTitle;
        if (goodBodyText != null) goodBodyText.text = goodBody;
        if (goodSubtitleText != null) goodSubtitleText.text = goodUnlockText;

        // Start fully black, fade in
        SetFade(1f);
        goodEndingPanel?.SetActive(true);
        badEndingPanel?.SetActive(false);

        yield return StartCoroutine(Fade(1f, 0f));

        // Count down with live text
        yield return StartCoroutine(Countdown(goodEndingDuration, goodCountdownText));

        // Fade out then return to menu
        yield return StartCoroutine(Fade(0f, 1f));
        ReturnToMenu();
    }

    //  BAD ENDING

    private IEnumerator PlayBadEnding()
    {
        if (badTitleText != null) badTitleText.text = badTitle;
        if (badBodyText != null) badBodyText.text = badBody;

        SetFade(1f);
        badEndingPanel?.SetActive(true);
        goodEndingPanel?.SetActive(false);

        yield return StartCoroutine(Fade(1f, 0f));
        yield return StartCoroutine(Countdown(badEndingDuration, badCountdownText));
        yield return StartCoroutine(Fade(0f, 1f));
        ReturnToMenu();
    }
    //  HELPERS

    private IEnumerator Countdown(float duration, TMP_Text label)
    {
        float remaining = duration;
        while (remaining > 0f)
        {
            if (label != null)
                label.text = $"Returning to menu in {Mathf.CeilToInt(remaining)}...";

            remaining -= Time.deltaTime;
            yield return null;
        }

        if (label != null)
            label.text = "";
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeImage == null) yield break;

        float elapsed = 0f;
        Color c = fadeImage.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, elapsed / fadeDuration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = to;
        fadeImage.color = c;
    }

    private void SetFade(float alpha)
    {
        if (fadeImage == null) return;
        Color c = fadeImage.color;
        c.a = alpha;
        fadeImage.color = c;
    }

    private void ReturnToMenu()
    {
        // Clean up the ending flag so it doesn't persist into next playthrough
        PlayerPrefs.DeleteKey(PREF_GOOD_ENDING);
        PlayerPrefs.Save();
        SceneManager.LoadScene(mainMenuSceneName);
    }
}