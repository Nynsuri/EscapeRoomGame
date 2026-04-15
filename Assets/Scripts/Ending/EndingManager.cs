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

    [Header("Shared")]
    [SerializeField] private Image fadeImage;          // full-screen black Image for fade

    [Header("Timing")]
    [SerializeField] private float endingDisplayDuration = 7f;  // seconds ending text shows before credits
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


    [Header("Credits")]
    [SerializeField] private float creditsScrollDuration = 40f;

    // ── Credits content ───────────────────────────────────────────
    private const string CreditsText =
        "ASSETS & CREDITS\n" +
        "All 3D models sourced from Sketchfab\n\n\n" +

        "── ROOM 1 ──\n\n" +

        "Bed Side Table\n" +
        "  Author: phillips.kieran\n" +
        "  License: CC BY 4.0\n\n" +

        "Wooden Table\n" +
        "  Author: Mehdi Shahsavan\n" +
        "  License: CC BY 4.0\n\n" +

        "Generator\n" +
        "  Author: DJMaesen (bumstrum)\n" +
        "  License: CC Attribution\n\n" +

        "Damaged Frame\n" +
        "  Author: Gabbo (gabbo104)\n" +
        "  License: CC BY 4.0\n\n" +

        "Key\n" +
        "  Author: yomans\n" +
        "  License: CC Attribution\n\n" +

        "Door Wooden Old\n" +
        "  Author: Mehdi Shahsavan\n" +
        "  License: CC BY 4.0\n\n" +

        "Filing Cabinets (papers)\n" +
        "  Author: TooManyDemons\n" +
        "  License: CC BY 4.0\n\n\n" +

        "── ROOM 2 ──\n\n" +

        "Cryopod\n" +
        "  Author: epilogueronin\n" +
        "  License: CC Attribution\n\n" +

        "Digital City\n" +
        "  Author: Moshe Caine\n" +
        "  License: CC Attribution\n\n" +

        "Repair Kit / Control Panel\n" +
        "  Author: Ramil Kudashev (mlknz)\n" +
        "  License: CC BY 4.0\n\n" +

        "Realistic Galaxy Skybox HDRI Panorama\n" +
        "  Author: Aliaksandr.melas\n" +
        "  License: CC BY 4.0\n\n\n" +

        "── ROOM 3 ──\n\n" +

        "Old Wooden Grand Piano\n" +
        "  Author: Mikael H. (mikaelhyttinen)\n" +
        "  License: CC Attribution\n\n" +

        "Revolver\n" +
        "  Author: DJMaesen (bumstrum)\n" +
        "  License: CC Attribution\n\n" +

        "Old Western Bar\n" +
        "  Author: gallacs\n" +
        "  License: CC BY 4.0\n\n" +

        "Saloon Floor Tile Texture\n" +
        "  Author: CATholic\n" +
        "  License: CC Attribution\n\n" +

        "Bar with Bottles\n" +
        "  Author: windupbird\n" +
        "  License: CC BY 4.0\n\n" +

        "Door Wooden\n" +
        "  Author: Mehdi Shahsavan\n" +
        "  License: CC Attribution\n\n\n" +

        "── ROOM 4 ──\n\n" +

        "Chemistry Glassware\n" +
        "  Author: melissasyamsiah\n" +
        "  License: CC BY 4.0\n\n" +

        "Binocular Microscope\n" +
        "  Author: miqasasuqasa\n" +
        "  License: CC BY 4.0\n\n" +

        "Laboratory Table\n" +
        "  Author: yuitop\n" +
        "  License: CC BY 4.0\n\n\n" +

        "Thank you for playing.";

    // ── Runtime ───────────────────────────────────────────────────
    private void Start()
    {
        // Restore time scale in case the game paused before transitioning
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        bool goodEnding = PlayerPrefs.GetInt(PREF_GOOD_ENDING, 0) == 1;

        if (goodEnding)
        {
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
        if (goodTitleText != null) goodTitleText.text = goodTitle;
        if (goodBodyText != null) goodBodyText.text = goodBody;
        if (goodSubtitleText != null) goodSubtitleText.text = goodUnlockText;

        SetFade(1f);
        goodEndingPanel?.SetActive(true);
        badEndingPanel?.SetActive(false);

        yield return StartCoroutine(Fade(1f, 0f));
        yield return new WaitForSeconds(endingDisplayDuration);
        yield return StartCoroutine(Fade(0f, 1f));

        goodEndingPanel?.SetActive(false);
        yield return StartCoroutine(PlayCredits());
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
        yield return new WaitForSeconds(endingDisplayDuration);
        yield return StartCoroutine(Fade(0f, 1f));

        badEndingPanel?.SetActive(false);
        yield return StartCoroutine(PlayCredits());
    }

    //  CREDITS

    private IEnumerator PlayCredits()
    {
        // ── Build credits canvas ──────────────────────────────────
        var canvasGO = new GameObject("CreditsCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode =
            UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;

        // Black background
        var bg = new GameObject("BG", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        bg.transform.SetParent(canvasGO.transform, false);
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bg.GetComponent<UnityEngine.UI.Image>().color = Color.black;

        // Mask area — leaves a margin and hides text outside it
        var maskGO = new GameObject("Mask", typeof(RectTransform), typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.Mask));
        maskGO.transform.SetParent(canvasGO.transform, false);
        var maskRT = maskGO.GetComponent<RectTransform>();
        maskRT.anchorMin = new Vector2(0.1f, 0.08f);
        maskRT.anchorMax = new Vector2(0.9f, 0.92f);
        maskRT.offsetMin = maskRT.offsetMax = Vector2.zero;
        maskGO.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0.01f);
        maskGO.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;

        // Scrolling text container
        var textGO = new GameObject("CreditsText", typeof(RectTransform));
        textGO.transform.SetParent(maskGO.transform, false);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0f, 0f);
        textRT.anchorMax = new Vector2(1f, 0f);
        textRT.pivot = new Vector2(0.5f, 0f);
        textRT.sizeDelta = new Vector2(0f, 2000f); // tall enough for all text

        var tmPro = textGO.AddComponent<TMPro.TextMeshProUGUI>();
        tmPro.text = CreditsText;
        tmPro.fontSize = 22;
        tmPro.alignment = TMPro.TextAlignmentOptions.Center;
        tmPro.color = Color.white;
        tmPro.textWrappingMode = TMPro.TextWrappingModes.Normal;

        // Force mesh update to get correct preferred height
        tmPro.ForceMeshUpdate();
        float textHeight = tmPro.preferredHeight + 60f;
        textRT.sizeDelta = new Vector2(0f, textHeight);

        // Wait one frame so Unity's layout system resolves RectTransform sizes
        yield return null;

        // Skip prompt fixed at bottom
        var skipGO = new GameObject("SkipPrompt", typeof(RectTransform));
        skipGO.transform.SetParent(canvasGO.transform, false);
        var skipRT = skipGO.GetComponent<RectTransform>();
        skipRT.anchorMin = new Vector2(0f, 0f); skipRT.anchorMax = new Vector2(1f, 0f);
        skipRT.pivot = new Vector2(0.5f, 0f);
        skipRT.sizeDelta = new Vector2(0f, 40f);
        skipRT.anchoredPosition = new Vector2(0f, 10f);
        var skipTMP = skipGO.AddComponent<TMPro.TextMeshProUGUI>();
        skipTMP.text = "[ Press any key to skip ]";
        skipTMP.fontSize = 16;
        skipTMP.alignment = TMPro.TextAlignmentOptions.Center;
        skipTMP.color = new Color(1f, 1f, 1f, 0.45f);

        var canvasFader = canvasGO.AddComponent<CanvasGroup>();
        canvasFader.alpha = 0f;

        // ── Scroll text upward, title starting at centre of screen ─
        // pivot is at bottom of text, anchor at bottom of mask.
        // We want the TOP of the text at maskHeight/2:
        //   top of text = anchoredPosition.y + textHeight = maskHeight/2
        //   → anchoredPosition.y = maskHeight/2 - textHeight
        float maskHeight = maskRT.rect.height;
        float startY = maskHeight * 0.5f - textHeight;   // top of text at centre of mask
        float endY   = maskHeight;                        // bottom of text scrolled past top

        textRT.anchoredPosition = new Vector2(0f, startY);

        bool skipped = false;
        float elapsed = 0f;

        while (elapsed < creditsScrollDuration && !skipped)
        {
            if (Input.anyKeyDown) skipped = true;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / creditsScrollDuration);
            textRT.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, t));

            // Fade in during first fadeDuration seconds of scrolling
            if (canvasFader.alpha < 1f)
                canvasFader.alpha = Mathf.Clamp01(elapsed / fadeDuration);

            yield return null;
        }

        // Fade out credits
        float fadeOut = 1f;
        while (fadeOut > 0f)
        {
            fadeOut -= Time.deltaTime / fadeDuration;
            canvasFader.alpha = Mathf.Clamp01(fadeOut);
            yield return null;
        }

        Destroy(canvasGO);
        ReturnToMenu();
    }

    //  HELPERS

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
        PlayerPrefs.DeleteKey(PREF_GOOD_ENDING);
        PlayerPrefs.Save();
        SceneManager.LoadScene(mainMenuSceneName);
    }
}