using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FPSCounter -- Unity 6000.3.9f1
/// Attach to any GameObject. Creates its own Canvas + Text at runtime.
/// Displays a rolling average FPS in the corner of the screen.
/// </summary>
public class FPSCounter : MonoBehaviour
{
    [Header("Display")]
    public Corner corner          = Corner.TopRight;
    public int    fontSize        = 16;
    public Color  goodColor       = new Color(0.2f, 1f, 0.2f);   // >= goodThreshold
    public Color  okColor         = new Color(1f,   0.8f, 0.1f); // >= okThreshold
    public Color  badColor        = new Color(1f,   0.2f, 0.2f); // below okThreshold
    public int    goodThreshold   = 60;
    public int    okThreshold     = 30;

    [Header("Averaging")]
    [Tooltip("How many frames to average over")]
    public int sampleCount = 60;

    public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

    // -- UI refs ---------------------------------------------------------------
    private Text _text;

    // -- Averaging -------------------------------------------------------------
    private float[] _samples;
    private int     _sampleIndex = 0;
    private float   _sampleSum   = 0f;
    private bool    _full        = false;

    // -- Unity -----------------------------------------------------------------

    void Awake()
    {
        _samples = new float[sampleCount];
        BuildUI();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Rolling average
        _sampleSum -= _samples[_sampleIndex];
        _samples[_sampleIndex] = dt;
        _sampleSum += dt;
        _sampleIndex = (_sampleIndex + 1) % sampleCount;
        if (_sampleIndex == 0) _full = true;

        int count = _full ? sampleCount : _sampleIndex;
        float avg = _sampleSum / Mathf.Max(1, count);
        int fps   = Mathf.RoundToInt(1f / avg);

        _text.text  = $"{fps} FPS";
        _text.color = fps >= goodThreshold ? goodColor
                    : fps >= okThreshold   ? okColor
                    : badColor;
    }

    // -- UI Builder ------------------------------------------------------------

    void BuildUI()
    {
        var cgo = new GameObject("FPSCanvas");
        DontDestroyOnLoad(cgo);
        var cv = cgo.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 100; // on top of everything
        cgo.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ConstantPixelSize;

        var go = new GameObject("FPSText", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(cgo.transform, false);

        var rt = go.GetComponent<RectTransform>();

        Vector2 anchor, pivot;
        Vector2 offset;
        float pad = 8f;

        switch (corner)
        {
            case Corner.TopLeft:
                anchor = new Vector2(0f, 1f);
                pivot  = new Vector2(0f, 1f);
                offset = new Vector2(pad, -pad);
                break;
            case Corner.BottomLeft:
                anchor = new Vector2(0f, 0f);
                pivot  = new Vector2(0f, 0f);
                offset = new Vector2(pad, pad);
                break;
            case Corner.BottomRight:
                anchor = new Vector2(1f, 0f);
                pivot  = new Vector2(1f, 0f);
                offset = new Vector2(-pad, pad);
                break;
            default: // TopRight
                anchor = new Vector2(1f, 1f);
                pivot  = new Vector2(1f, 1f);
                offset = new Vector2(-pad, -pad);
                break;
        }

        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot     = pivot;
        rt.sizeDelta = new Vector2(90f, 28f);
        rt.anchoredPosition = offset;

        _text           = go.GetComponent<Text>();
        _text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _text.fontSize  = fontSize;
        _text.fontStyle = FontStyle.Bold;
        _text.alignment = corner == Corner.TopLeft || corner == Corner.BottomLeft
            ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
        _text.color     = goodColor;
        _text.text      = "-- FPS";
    }
}
