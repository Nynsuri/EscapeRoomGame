using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// PortalRing — Unity 6000.3.9f1
///
/// The ring mesh object spins. Triggers are on a SEPARATE sibling
/// GameObject that does NOT spin — both sit under the same parent.
///
/// ─── Hierarchy ───────────────────────────────────────────────────
///   PortalRoot  (empty, static)
///   ├── RingMesh            ← PortalRing script here, this spins
///   └── PortalTriggers      ← empty static GameObject
///       ├── SideATrigger    ← BoxCollider (IsTrigger=true)
///       │                      Rigidbody (IsKinematic=true, UseGravity=false)
///       │                      PortalSideTrigger (isSideA=true, assign ring)
///       └── SideBTrigger    ← BoxCollider (IsTrigger=true)
///                              Rigidbody (IsKinematic=true, UseGravity=false)
///                              PortalSideTrigger (isSideA=false, assign ring)
/// ─────────────────────────────────────────────────────────────────
/// </summary>
public class PortalRing : MonoBehaviour
{
    [Header("Spin")]
    public float spinSpeed = 360f;

    [Header("Side A — Corridor")]
    public Material sideASkybox;
    [Range(0f, 1f)] public float sideAReflectionIntensity = 0f;

    [Header("Side B — Room")]
    public Material sideBSkybox;
    [Range(0f, 1f)] public float sideBReflectionIntensity = 1f;

    [Header("Transition")]
    public float transitionDuration = 1.0f;

    [Header("Room Blockers (optional)")]
    public PortalBlocker[] roomBlockers;

    [Header("Portal Veil Particles")]
    [Tooltip("ParticleSystem parented to this RingMesh — assign in Inspector")]
    public ParticleSystem veilParticles;
    [Tooltip("Emission rate when fully veiled (player can't see through)")]
    public float veilEmissionRate = 80f;
    [Tooltip("Emission rate during/after transition (partially see-through)")]
    public float openEmissionRate = 20f;

    [Header("Sound Effects")]
    [Tooltip("Looping ambient sound while the portal is active (played from this object)")]
    public AudioClip idleAmbientSound;
    [Tooltip("Volume for the idle loop")]
    [Range(0f, 1f)] public float idleVolume = 0.4f;
    [Tooltip("Max audible distance for the idle loop (meters)")]
    public float idleMaxDistance = 10f;

    [Tooltip("Play a sound when the player crosses from Side A (Corridor) into the room")]
    public bool sideACrossingEnabled = true;
    [Tooltip("Clip to play on Side A crossing")]
    public AudioClip sideACrossingSound;

    [Tooltip("Play a sound when the player crosses from Side B (Room) into the corridor")]
    public bool sideBCrossingEnabled = true;
    [Tooltip("Clip to play on Side B crossing")]
    public AudioClip sideBCrossingSound;

    [Header("Audio Mixer")]
    public UnityEngine.Audio.AudioMixerGroup audioMixerGroup;

    [Header("Audio Occlusion")]
    [Tooltip("Layers considered walls for audio occlusion — match your wall/floor layer(s)")]
    public LayerMask occlusionLayers = ~0;
    [Tooltip("How far from the portal centre the 4 edge-sample rays are offset (meters). Set larger than the portal hole radius.")]
    public float occlusionSampleOffset = 1.2f;
    [Tooltip("How many of the 5 sample rays must have clear line-of-sight before audio plays.")]
    [Range(1, 5)] public int occlusionMinClearRays = 2;
    [Tooltip("Hard distance cutoff — portal is always silent beyond this range regardless of line-of-sight (meters). Set so it covers the corridor but not adjacent rooms.")]
    public float audioProximityRadius = 6f;

    private Coroutine _transitionCoroutine;
    private ParticleSystem.EmissionModule _emission;
    private bool _particlesInitialised;
    private AudioSource _idleSource;
    private Camera _cam;
    private Collider[] _selfColliders;

    void Start()
    {
        _cam = Camera.main ?? FindFirstObjectByType<Camera>();
        _selfColliders = GetComponentsInChildren<Collider>(true);

        if (veilParticles != null)
        {
            _emission = veilParticles.emission;
            _emission.rateOverTime = veilEmissionRate;
            _particlesInitialised = true;
        }

        if (idleAmbientSound != null)
        {
            _idleSource = gameObject.AddComponent<AudioSource>();
            _idleSource.clip = idleAmbientSound;
            _idleSource.loop = true;
            _idleSource.volume = idleVolume;
            _idleSource.playOnAwake = false;
            _idleSource.minDistance = 1f;
            _idleSource.maxDistance = idleMaxDistance;
            AudioHelper.Configure(_idleSource, audioMixerGroup);
            _idleSource.Play();
        }
    }

    void Update()
    {
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);
        UpdateOcclusion();
    }

    void UpdateOcclusion()
    {
        if (_idleSource == null || _cam == null) return;

        Vector3 listenerPos = _cam.transform.position;
        Vector3 centre = transform.position;

        // Hard distance gate — never audible beyond this radius no matter what geometry does
        if (Vector3.Distance(listenerPos, centre) > audioProximityRadius)
        {
            _idleSource.volume = 0f;
            return;
        }

        // 5 sample points: centre + 4 cardinal offsets around the ring.
        // World-space up/right so the offsets don't spin with the ring mesh.
        Vector3[] samples = new Vector3[5]
        {
            centre,
            centre + Vector3.up    * occlusionSampleOffset,
            centre + Vector3.down  * occlusionSampleOffset,
            centre + Vector3.right * occlusionSampleOffset,
            centre + Vector3.left  * occlusionSampleOffset,
        };

        int clearCount = 0;
        foreach (Vector3 target in samples)
        {
            Vector3 dir = target - listenerPos;
            float dist = dir.magnitude;
            RaycastHit[] hits = Physics.RaycastAll(listenerPos, dir.normalized, dist,
                occlusionLayers, QueryTriggerInteraction.Ignore);

            bool blocked = false;
            foreach (var hit in hits)
            {
                bool isSelf = false;
                foreach (var c in _selfColliders)
                    if (hit.collider == c) { isSelf = true; break; }
                if (!isSelf) { blocked = true; break; }
            }

            if (!blocked) clearCount++;
        }

        _idleSource.volume = clearCount >= occlusionMinClearRays ? idleVolume : 0f;
    }

    public void OnSideTriggered(bool isSideA)
    {
        Material targetSkybox = isSideA ? sideASkybox : sideBSkybox;
        float targetReflection = isSideA ? sideAReflectionIntensity : sideBReflectionIntensity;

        if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = StartCoroutine(TransitionTo(targetSkybox, targetReflection));

        SetBlockers(blocking: isSideA);
        BarRoomState.SetPlayerSide(inCorridor: !isSideA);

        // Play crossing sound for the side the player just came FROM
        if (isSideA && sideACrossingEnabled && sideACrossingSound != null)
            AudioHelper.Play(sideACrossingSound, transform.position, audioMixerGroup);
        else if (!isSideA && sideBCrossingEnabled && sideBCrossingSound != null)
            AudioHelper.Play(sideBCrossingSound, transform.position, audioMixerGroup);

        if (isSideA)
            ClearInventory();
    }

    /// <summary>Call this any time you want to programmatically set veil opacity.</summary>
    public void SetVeil(float emission)
    {
        if (!_particlesInitialised) return;
        _emission.rateOverTime = emission;
    }

    void ClearInventory()
    {
        var inventory = FindFirstObjectByType<Inventory>();
        if (inventory == null) return;

        var toRemove = new System.Collections.Generic.List<InventoryItem>();
        foreach (var item in inventory.Items)
            toRemove.Add(item);

        foreach (var item in toRemove)
        {
            item.OnConsume();
            inventory.RemoveItem(item);
        }
    }

    IEnumerator TransitionTo(Material skybox, float targetReflection)
    {
        // Thin the veil so the skybox swap doesn't look like a hard cut
        if (_particlesInitialised)
            _emission.rateOverTime = openEmissionRate;

        if (skybox != null)
            RenderSettings.skybox = skybox;

        float start = RenderSettings.reflectionIntensity;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            RenderSettings.reflectionIntensity =
                Mathf.Lerp(start, targetReflection,
                           Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration));
            yield return null;
        }

        RenderSettings.reflectionIntensity = targetReflection;
        DynamicGI.UpdateEnvironment();

        // Restore full veil after transition settles
        if (_particlesInitialised)
            _emission.rateOverTime = veilEmissionRate;
    }

    void SetBlockers(bool blocking)
    {
        if (roomBlockers == null) return;
        foreach (var b in roomBlockers)
            if (b != null) b.SetBlocking(blocking);
    }

    void OnDrawGizmosSelected()
    {
        // Portal face outline
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.4f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1.8f, 1.8f, 0.02f));

        // Audio proximity radius — always in world space
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.25f);
        Gizmos.DrawSphere(transform.position, audioProximityRadius);
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, audioProximityRadius);
    }
}