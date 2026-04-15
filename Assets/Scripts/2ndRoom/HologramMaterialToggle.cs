using System.Reflection;
using UnityEngine;

/// <summary>
/// HologramMaterialToggle — Unity 6000.3.9f1
///
/// Keeps the original A/B swap behavior, but Material B is now chosen
/// automatically based on puzzle completion combination:
///
/// - Hack done
/// - Cryo done
/// - Engine done
/// - Hack + Cryo
/// - Hack + Engine
/// - Cryo + Engine
/// - All done
///
/// Setup:
/// 1. Attach to the hologram Quad.
/// 2. Assign targetRenderer.
/// 3. Assign materialA (default view).
/// 4. Assign the combination materials below.
/// 5. Assign the 3 puzzle references.
/// </summary>
public class HologramMaterialToggle : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Drag the Quad GameObject here — the one whose material gets swapped")]
    public Renderer targetRenderer;

    [Header("Base Material")]
    [Tooltip("Starting material (Material A)")]
    public Material materialA;

    [Header("No Completion Material")]
    public Material noneDoneMaterial;

    [Header("Single Completion Materials")]
    public Material hackDoneMaterial;
    public Material cryoDoneMaterial;
    public Material engineDoneMaterial;

    [Header("Double Completion Materials")]
    public Material hackAndCryoDoneMaterial;
    public Material hackAndEngineDoneMaterial;
    public Material cryoAndEngineDoneMaterial;

    [Header("Triple Completion Material")]
    public Material allDoneMaterial;

    [Header("Puzzle References")]
    public HackingPuzzle hackingPuzzle;
    public CircuitRepairPuzzle cryoPuzzle;
    public EngineCalibratorPuzzle enginePuzzle;

    [Header("Interaction")]
    public Camera playerCamera;
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Sound Effects")]
    [Tooltip("Looping beep played while on Material A")]
    public AudioClip loopSoundA;
    [Tooltip("One-shot sound played once when swapped to Material B")]
    public AudioClip oneShotSoundB;
    [Tooltip("Volume of the looping sound when unoccluded")]
    [Range(0f, 1f)] public float loopVolume = 1f;

    [Header("Audio Mixer")]
    public UnityEngine.Audio.AudioMixerGroup audioMixerGroup;

    [Header("Audio Occlusion")]
    [Tooltip("Layers considered walls — match your wall/floor layers")]
    public LayerMask occlusionLayers = ~0;

    [Header("HUD")]
    public string promptText = "Press E to interact";
    public float promptYOffset = 60f;

    // ── State ─────────────────────────────────────────────────────
    private bool _onB = false;

    private bool _showPrompt = false;
    private GUIStyle _promptStyle;

    private AudioSource _loopSource;

    // Reflection cache
    private FieldInfo _hackStateField;
    private FieldInfo _cryoSolvedField;
    private FieldInfo _engineSolvedField;

    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (targetRenderer != null && materialA != null)
            targetRenderer.material = materialA;

        CacheReflection();

        _loopSource = gameObject.AddComponent<AudioSource>();
        _loopSource.clip = loopSoundA;
        _loopSource.loop = true;
        _loopSource.playOnAwake = false;
        AudioHelper.Configure(_loopSource, audioMixerGroup);

        if (loopSoundA != null)
            _loopSource.Play();
    }

    void Update()
    {
        if (playerCamera == null) return;

        UpdateOcclusion();

        // If currently on B, keep B material synced automatically
        if (_onB && targetRenderer != null)
        {
            Material comboMaterial = GetCurrentCombinationMaterial();
            if (comboMaterial != null && targetRenderer.material != comboMaterial)
                targetRenderer.material = comboMaterial;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, interactDistance)
                   && hitInfo.collider.gameObject == gameObject;

        _showPrompt = hit;

        if (hit && Input.GetKeyDown(interactKey))
            Toggle();
    }

    void CacheReflection()
    {
        if (hackingPuzzle != null)
            _hackStateField = typeof(HackingPuzzle).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);

        if (cryoPuzzle != null)
            _cryoSolvedField = typeof(CircuitRepairPuzzle).GetField("_everSolved", BindingFlags.NonPublic | BindingFlags.Instance);

        if (enginePuzzle != null)
            _engineSolvedField = typeof(EngineCalibratorPuzzle).GetField("_solved", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    void UpdateOcclusion()
    {
        if (_loopSource == null || !_loopSource.isPlaying) return;

        Vector3 listenerPos = playerCamera.transform.position;
        Vector3 sourcePos = transform.position;
        Vector3 dir = sourcePos - listenerPos;
        float dist = dir.magnitude;

        RaycastHit[] hits = Physics.RaycastAll(
            listenerPos,
            dir.normalized,
            dist,
            occlusionLayers,
            QueryTriggerInteraction.Ignore
        );

        bool blocked = false;
        foreach (var h in hits)
        {
            if (h.collider.gameObject == gameObject ||
                h.collider.transform.IsChildOf(transform))
                continue;

            blocked = true;
            break;
        }

        _loopSource.volume = blocked ? 0f : loopVolume;
    }

    void Toggle()
    {
        _onB = !_onB;

        if (targetRenderer != null)
            targetRenderer.material = _onB ? GetCurrentCombinationMaterial() : materialA;

        if (_onB)
        {
            _loopSource.Stop();
            if (oneShotSoundB != null)
                AudioHelper.Play(oneShotSoundB, transform.position, audioMixerGroup);
        }
        else
        {
            if (loopSoundA != null)
                _loopSource.Play();
        }
    }

    Material GetCurrentCombinationMaterial()
    {
        bool hackDone = IsHackDone();
        bool cryoDone = IsCryoDone();
        bool engineDone = IsEngineDone();

        if (hackDone && cryoDone && engineDone)
            return GetFallback(allDoneMaterial);

        if (hackDone && cryoDone)
            return GetFallback(hackAndCryoDoneMaterial);

        if (hackDone && engineDone)
            return GetFallback(hackAndEngineDoneMaterial);

        if (cryoDone && engineDone)
            return GetFallback(cryoAndEngineDoneMaterial);

        if (hackDone)
            return GetFallback(hackDoneMaterial);

        if (cryoDone)
            return GetFallback(cryoDoneMaterial);

        if (engineDone)
            return GetFallback(engineDoneMaterial);

        // If nothing is done yet and player toggles to B,
        // fall back to A so there is never a null material.
        return GetFallback(noneDoneMaterial);
    }

    Material GetFallback(Material preferred)
    {
        if (preferred != null) return preferred;
        if (materialA != null) return materialA;
        return targetRenderer != null ? targetRenderer.sharedMaterial : null;
    }

    bool IsHackDone()
    {
        if (hackingPuzzle == null || _hackStateField == null)
            return false;

        object stateValue = _hackStateField.GetValue(hackingPuzzle);
        if (stateValue == null)
            return false;

        return stateValue.ToString() == "Solved";
    }

    bool IsCryoDone()
    {
        if (cryoPuzzle == null || _cryoSolvedField == null)
            return false;

        object solvedValue = _cryoSolvedField.GetValue(cryoPuzzle);
        return solvedValue is bool solved && solved;
    }

    bool IsEngineDone()
    {
        if (enginePuzzle == null || _engineSolvedField == null)
            return false;

        object solvedValue = _engineSolvedField.GetValue(enginePuzzle);
        return solvedValue is bool solved && solved;
    }

    void OnGUI()
    {
        if (!_showPrompt) return;

        if (_promptStyle == null)
        {
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 0.6f) }
            };
        }

        const float w = 400f;
        const float h = 36f;
        float x = (Screen.width - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f + promptYOffset;

        GUI.color = Color.black;
        GUI.Label(new Rect(x + 2, y + 2, w, h), promptText, _promptStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, w, h), promptText, _promptStyle);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}