using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GunItem -- Unity 6000.3.9f1
/// Attach to the TOP-LEVEL gun GameObject (the one that has ItemPickup on it,
/// e.g. "gun" or "bdebdcc58f524e05a9715789012cb2c6.fbx").
///
/// The gun is made of many separate mesh pieces (base, front, chamber, bullets,
/// hammer, trigger, etc.). Instead of moving individual meshes, this script moves
/// the entire gun parent to the camera -- exactly like TorchItem moves its mesh.
///
/// Crosshair is drawn via a Canvas that is created at runtime.
/// </summary>
public class GunItem : InventoryItem
{
    [Header("Shoot")]
    public KeyCode shootKey = KeyCode.Mouse0;
    public float shootRange = 100f;
    public float fireCooldown = 0.6f;

    [Header("Held position -- tweak in Play Mode")]
    [Tooltip("Position relative to camera (right, up, forward). Negative Y = lower corner.")]
    public Vector3 heldOffset = new Vector3(0.42f, -0.35f, 0.38f);
    [Tooltip("Local rotation of the gun while held.")]
    public Vector3 heldRotation = new Vector3(0f, 0f, 0f);
    [Tooltip("Scale multiplier applied on top of the gun world scale. < 1 = smaller, > 1 = bigger.")]
    public float heldScale = 0.55f;

    [Header("Muzzle Flash (optional -- assign a child particle / light GO)")]
    public GameObject muzzleFlashObject;
    public float muzzleFlashDuration = 0.07f;

    [Header("Audio (optional)")]
    public AudioClip shootSound;

    [Header("Crosshair")]
    public float crosshairSize = 10f;
    public float crosshairThickness = 2f;
    public float crosshairGap = 4f;
    public Color crosshairColor = Color.white;

    [Header("Auto Collider (added at runtime on this GO for pickup raycast)")]
    [Tooltip("Size of the box collider in LOCAL space. If your gun has a tiny scale (e.g. 0.05) make this large, e.g. (6, 3, 12).")]
    public Vector3 pickupColliderSize = new Vector3(6f, 3f, 12f);
    [Tooltip("Center offset of the auto collider relative to this GO's pivot.")]
    public Vector3 pickupColliderCenter = new Vector3(0f, 0f, 0f);

    // -- Private ---------------------------------------------------------------

    private Camera _cam;
    private AudioSource _audio;
    private float _nextFireTime;
    private bool _inHand;

    // The root transform we move to the camera (RootNode.001 or whichever child
    // sits directly under the pickup GO and contains all the meshes)
    private Transform _gunRoot;

    // Original world transform (to restore on drop)
    private Transform _originalParent;
    private Vector3 _originalLocalPos;
    private Quaternion _originalLocalRot;
    private Vector3 _originalLocalScale;
    private Vector3 _originalWorldScale;  // lossyScale before pickup

    // Crosshair UI
    private GameObject _crosshairCanvas;
    private Image[] _crosshairLines; // left, right, up, down
    private Image _hitConfirmImage; // red flash on target hit

    // -- Awake -----------------------------------------------------------------

    void Awake()
    {
        itemName = "Revolver";
        description = $"Left-click to shoot.";

        // The gun root is the first child that isn't a component-holder --
        // we just take transform directly since the whole GO IS the gun.
        // We'll move THIS transform to the camera.
        _gunRoot = transform;

        // Store original transform so we can restore on drop
        _originalParent = _gunRoot.parent;
        _originalLocalPos = _gunRoot.localPosition;
        _originalLocalRot = _gunRoot.localRotation;
        _originalLocalScale = _gunRoot.localScale;
        _originalWorldScale = _gunRoot.lossyScale;  // world scale before any reparenting

        // Audio
        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 0f;

        if (muzzleFlashObject != null) muzzleFlashObject.SetActive(false);

        // Add a box collider on this GO so the player raycast can hit the gun
        // for pickup. Sized to roughly cover the revolver -- tweak in Inspector.
        var col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.size = pickupColliderSize;
        col.center = pickupColliderCenter;
    }

    // -- InventoryItem overrides -----------------------------------------------

    public override void OnPickup()
    {
        // Hide all renderers + colliders via base (keeps GO active)
        base.OnPickup();

        _cam = Camera.main ?? FindFirstObjectByType<Camera>();
        if (_cam == null) return;

        // Disable the pickup collider while held so it can't be re-picked up
        var col = GetComponent<BoxCollider>();
        if (col != null) col.enabled = false;

        // Re-enable ALL renderers on every piece of the gun
        foreach (var r in _gunRoot.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;

        // If heldScale is (1,1,1) (untouched default), auto-calculate the correct
        // local scale needed under the camera to preserve the gun's world size.
        // This fixes the giant-gun problem when the original scale is tiny (e.g. 0.049).
        // Apply heldScale as a multiplier on top of the original world scale
        Vector3 targetWorldScale = _originalWorldScale * heldScale;

        _gunRoot.SetParent(_cam.transform, false);
        _gunRoot.localPosition = heldOffset;
        _gunRoot.localRotation = Quaternion.Euler(heldRotation);

        Vector3 camScale = _cam.transform.lossyScale;
        _gunRoot.localScale = new Vector3(
            camScale.x != 0f ? targetWorldScale.x / camScale.x : targetWorldScale.x,
            camScale.y != 0f ? targetWorldScale.y / camScale.y : targetWorldScale.y,
            camScale.z != 0f ? targetWorldScale.z / camScale.z : targetWorldScale.z);

        // Show crosshair
        BuildCrosshair();

        _inHand = true;
    }

    public override void OnDrop(Vector3 worldPosition)
    {
        _inHand = false;

        DestroyCrosshair();

        // Re-enable pickup collider
        var col = GetComponent<BoxCollider>();
        if (col != null) col.enabled = true;

        // Detach from camera first, place at drop position in world space
        _gunRoot.SetParent(null, true);
        _gunRoot.position = worldPosition;
        _gunRoot.localRotation = _originalLocalRot;
        _gunRoot.localScale = _originalLocalScale;

        // Re-enable all renderers and colliders (base would do this but on wrong transform)
        foreach (var r in _gunRoot.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;
        foreach (var c in _gunRoot.GetComponentsInChildren<Collider>(true))
            c.enabled = true;

        // Don't call base.OnDrop — it would move transform.position again and
        // fight with what we just set. Manually re-enable the pickup collider (done above).
    }

    public override void OnConsume()
    {
        DestroyCrosshair();
        base.OnConsume();
    }

    public override void OnInventoryUpdate()
    {
        if (!_inHand) return;
        if (Input.GetKeyDown(shootKey))
            TryShoot();
    }

    public override void OnSelect()
    {
        if (_crosshairCanvas != null) _crosshairCanvas.SetActive(true);
    }

    public override void OnDeselect()
    {
        if (_crosshairCanvas != null) _crosshairCanvas.SetActive(false);
    }

    // -- Shooting --------------------------------------------------------------

    void TryShoot()
    {
        if (Time.time < _nextFireTime) return;
        _nextFireTime = Time.time + fireCooldown;

        if (shootSound != null) _audio.PlayOneShot(shootSound);
        StartCoroutine(MuzzleFlash());

        if (_cam == null) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * shootRange, Color.red, 0.5f);

        if (Physics.Raycast(ray, out RaycastHit hit, shootRange))
        {
            var target = hit.collider.GetComponentInParent<ShootingTarget>();
            if (target != null)
            {
                target.RegisterHit();
                StartCoroutine(HitConfirmFlash());
            }
            Debug.Log($"[GunItem] Shot hit: {hit.collider.gameObject.name}");
        }
    }

    IEnumerator MuzzleFlash()
    {
        if (muzzleFlashObject != null)
        {
            muzzleFlashObject.SetActive(true);
            yield return new WaitForSeconds(muzzleFlashDuration);
            muzzleFlashObject.SetActive(false);
        }
    }

    IEnumerator HitConfirmFlash()
    {
        if (_hitConfirmImage == null) yield break;
        _hitConfirmImage.color = new Color(1f, 0.2f, 0.2f, 0.6f);
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(0.6f, 0f, t / 0.25f);
            _hitConfirmImage.color = new Color(1f, 0.2f, 0.2f, a);
            yield return null;
        }
        _hitConfirmImage.color = Color.clear;
    }

    // -- Crosshair -------------------------------------------------------------

    void BuildCrosshair()
    {
        if (_crosshairCanvas != null) return;

        // Canvas
        _crosshairCanvas = new GameObject("GunCrosshair");
        var canvas = _crosshairCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        _crosshairCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();

        _crosshairLines = new Image[4]; // left, right, up, down

        Vector2[] sizes = new Vector2[]
        {
            new Vector2(crosshairSize, crosshairThickness), // left
            new Vector2(crosshairSize, crosshairThickness), // right
            new Vector2(crosshairThickness, crosshairSize), // up
            new Vector2(crosshairThickness, crosshairSize), // down
        };

        float half = crosshairGap + crosshairSize * 0.5f;
        Vector2[] positions = new Vector2[]
        {
            new Vector2(-half, 0f),
            new Vector2( half, 0f),
            new Vector2(0f,  half),
            new Vector2(0f, -half),
        };

        // Hit confirm — full screen red flash, starts invisible
        var hitGO = new GameObject("HitConfirm");
        hitGO.transform.SetParent(_crosshairCanvas.transform, false);
        _hitConfirmImage = hitGO.AddComponent<Image>();
        _hitConfirmImage.color = Color.clear;
        var hitRT = _hitConfirmImage.rectTransform;
        hitRT.anchorMin = Vector2.zero;
        hitRT.anchorMax = Vector2.one;
        hitRT.offsetMin = hitRT.offsetMax = Vector2.zero;

        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject($"CH_{i}");
            go.transform.SetParent(_crosshairCanvas.transform, false);
            var img = go.AddComponent<Image>();
            img.color = crosshairColor;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizes[i];
            rt.anchoredPosition = positions[i];
            _crosshairLines[i] = img;
        }
    }

    void DestroyCrosshair()
    {
        if (_crosshairCanvas != null)
        {
            Destroy(_crosshairCanvas);
            _crosshairCanvas = null;
        }
    }
}