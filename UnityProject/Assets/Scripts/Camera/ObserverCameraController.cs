using UnityEngine;
using UnityEngine.UI;

public class ObserverCameraController : MonoBehaviour
{
    public enum ObserverCameraPreset
    {
        TopDown,
        BedsideLeft,
        BedsideRight,
        CornerFrontLeft,
        CornerFrontRight,
        PartnerEyes,
        UserShoulder,
    }

    [Header("References")]
    public OVRCameraRig cameraRig;
    public SexKitAvatarDriver avatarDriver;
    public JoyBodyController joyBodyController;
    public PartnerDirector partnerDirector;
    public Camera observerCamera;
    public RawImage pipImage;

    [Header("State")]
    public bool observerEnabled;
    public bool pipEnabled;
    public ObserverCameraPreset activePreset = ObserverCameraPreset.BedsideLeft;

    [Header("Observer Camera")]
    public LayerMask cullingMask = ~0;
    public float smoothPositionSpeed = 6f;
    public float smoothRotationSpeed = 8f;
    public Vector2Int renderTextureSize = new(1024, 576);
    public Color backgroundColor = new(0.02f, 0.02f, 0.03f, 1f);

    [Header("World Presets")]
    public float bedsideDistance = 1.25f;
    public float bedsideHeight = 1.05f;
    public float topDownHeight = 2.35f;
    public float cornerDistance = 1.6f;
    public float cornerHeight = 1.35f;
    public float cornerDepth = 1.25f;
    public float lookAtHeight = 0.55f;
    public float userShoulderDistance = 0.45f;
    public float userShoulderHeight = -0.08f;

    [Header("Partner Eyes")]
    public float partnerEyeOffset = 0.03f;
    public float partnerEyeLookDistance = 1.6f;

    [Header("PIP")]
    public bool createPipCanvasIfMissing = true;
    public Vector2 pipSize = new(360f, 202f);
    public Vector3 pipLocalOffset = new(-0.28f, -0.14f, 0.8f);
    public Vector3 pipLocalEuler = Vector3.zero;

    [Header("Debug")]
    public bool enableDebugHotkeys = true;
    public KeyCode toggleObserverKey = KeyCode.O;
    public KeyCode togglePipKey = KeyCode.P;
    public KeyCode cyclePresetKey = KeyCode.RightBracket;
    public KeyCode reversePresetKey = KeyCode.LeftBracket;

    private RenderTexture _observerTexture;
    private Canvas _pipCanvas;
    private RectTransform _pipRect;

    void Start()
    {
        ResolveReferences();
        EnsureObserverCamera();
        EnsurePipUi();
        ApplyStateImmediate();
    }

    void Update()
    {
        if (enableDebugHotkeys)
        {
            HandleDebugHotkeys();
        }
    }

    void LateUpdate()
    {
        ResolveReferences();

        if (observerCamera == null)
        {
            EnsureObserverCamera();
        }

        if (observerCamera == null)
        {
            return;
        }

        observerCamera.enabled = observerEnabled;
        if (!observerEnabled)
        {
            UpdatePipVisibility();
            return;
        }

        UpdateObserverPose(Time.deltaTime);
        UpdatePipVisibility();
    }

    public void SetObserverEnabled(bool enabled)
    {
        observerEnabled = enabled;
        ApplyStateImmediate();
    }

    public void SetPipEnabled(bool enabled)
    {
        pipEnabled = enabled;
        ApplyStateImmediate();
    }

    public void SetPreset(ObserverCameraPreset preset)
    {
        activePreset = preset;
    }

    public void CyclePreset(int direction = 1)
    {
        var presets = System.Enum.GetValues(typeof(ObserverCameraPreset));
        var current = (int)activePreset;
        current = (current + direction) % presets.Length;
        if (current < 0)
        {
            current += presets.Length;
        }

        activePreset = (ObserverCameraPreset)current;
    }

    public string GetPresetLabel()
    {
        return activePreset switch
        {
            ObserverCameraPreset.TopDown => "Top Down",
            ObserverCameraPreset.BedsideLeft => "Bedside Left",
            ObserverCameraPreset.BedsideRight => "Bedside Right",
            ObserverCameraPreset.CornerFrontLeft => "Corner Front Left",
            ObserverCameraPreset.CornerFrontRight => "Corner Front Right",
            ObserverCameraPreset.PartnerEyes => "Partner Eyes",
            ObserverCameraPreset.UserShoulder => "User Shoulder",
            _ => activePreset.ToString(),
        };
    }

    private void HandleDebugHotkeys()
    {
        if (Input.GetKeyDown(toggleObserverKey))
        {
            SetObserverEnabled(!observerEnabled);
        }

        if (Input.GetKeyDown(togglePipKey))
        {
            SetPipEnabled(!pipEnabled);
        }

        if (Input.GetKeyDown(cyclePresetKey))
        {
            CyclePreset(1);
        }

        if (Input.GetKeyDown(reversePresetKey))
        {
            CyclePreset(-1);
        }
    }

    private void ResolveReferences()
    {
        cameraRig ??= FindFirstObjectByType<OVRCameraRig>();
        avatarDriver ??= FindFirstObjectByType<SexKitAvatarDriver>();
        joyBodyController ??= FindFirstObjectByType<JoyBodyController>();
        partnerDirector ??= FindFirstObjectByType<PartnerDirector>();
        pipImage ??= FindFirstObjectByType<RawImage>(FindObjectsInactive.Exclude);
    }

    private void EnsureObserverCamera()
    {
        if (observerCamera == null)
        {
            var cameraObject = new GameObject("ObserverCamera");
            cameraObject.transform.SetParent(transform, false);
            observerCamera = cameraObject.AddComponent<Camera>();
            observerCamera.stereoTargetEye = StereoTargetEyeMask.None;
        }

        EnsureRenderTexture();
        observerCamera.targetTexture = _observerTexture;
        observerCamera.clearFlags = CameraClearFlags.SolidColor;
        observerCamera.backgroundColor = backgroundColor;
        observerCamera.cullingMask = cullingMask;
        observerCamera.enabled = observerEnabled;
    }

    private void EnsureRenderTexture()
    {
        var width = Mathf.Max(128, renderTextureSize.x);
        var height = Mathf.Max(128, renderTextureSize.y);
        if (_observerTexture != null && _observerTexture.width == width && _observerTexture.height == height)
        {
            return;
        }

        if (_observerTexture != null)
        {
            _observerTexture.Release();
        }

        _observerTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "ObserverCameraRT",
            useMipMap = false,
            autoGenerateMips = false,
        };
        _observerTexture.Create();
    }

    private void EnsurePipUi()
    {
        if (pipImage != null)
        {
            _pipRect = pipImage.rectTransform;
            _pipCanvas = pipImage.GetComponentInParent<Canvas>();
            pipImage.texture = _observerTexture;
            UpdatePipVisibility();
            return;
        }

        if (!createPipCanvasIfMissing || cameraRig == null || cameraRig.centerEyeAnchor == null)
        {
            return;
        }

        var canvasObject = new GameObject("ObserverPIPCanvas");
        canvasObject.transform.SetParent(cameraRig.centerEyeAnchor, false);
        canvasObject.transform.localPosition = pipLocalOffset;
        canvasObject.transform.localRotation = Quaternion.Euler(pipLocalEuler);

        _pipCanvas = canvasObject.AddComponent<Canvas>();
        _pipCanvas.renderMode = RenderMode.WorldSpace;
        _pipCanvas.worldCamera = cameraRig.centerEyeAnchor.GetComponent<Camera>();
        canvasObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        canvasObject.AddComponent<GraphicRaycaster>();

        var panelObject = new GameObject("ObserverPIP");
        panelObject.transform.SetParent(canvasObject.transform, false);
        pipImage = panelObject.AddComponent<RawImage>();
        pipImage.texture = _observerTexture;
        pipImage.color = Color.white;

        _pipRect = pipImage.rectTransform;
        _pipRect.sizeDelta = pipSize;
        _pipCanvas.transform.localScale = Vector3.one * 0.001f;

        UpdatePipVisibility();
    }

    private void ApplyStateImmediate()
    {
        if (observerCamera != null)
        {
            observerCamera.enabled = observerEnabled;
        }

        UpdatePipVisibility();
    }

    private void UpdatePipVisibility()
    {
        if (_pipCanvas == null)
        {
            return;
        }

        _pipCanvas.gameObject.SetActive(observerEnabled && pipEnabled);
        if (pipImage != null && pipImage.texture != _observerTexture)
        {
            pipImage.texture = _observerTexture;
        }
    }

    private void UpdateObserverPose(float deltaTime)
    {
        if (!TryResolvePresetPose(out var targetPosition, out var targetRotation))
        {
            return;
        }

        observerCamera.transform.position = Vector3.Lerp(
            observerCamera.transform.position,
            targetPosition,
            deltaTime * smoothPositionSpeed);
        observerCamera.transform.rotation = Quaternion.Slerp(
            observerCamera.transform.rotation,
            targetRotation,
            deltaTime * smoothRotationSpeed);
    }

    private bool TryResolvePresetPose(out Vector3 position, out Quaternion rotation)
    {
        var anchor = GetSceneAnchor();
        var bedBasis = GetBedBasis(anchor, out var right, out var forward);
        var lookTarget = anchor + Vector3.up * lookAtHeight;

        switch (activePreset)
        {
            case ObserverCameraPreset.TopDown:
                position = anchor + Vector3.up * topDownHeight;
                rotation = Quaternion.LookRotation((lookTarget - position).normalized, forward);
                return true;

            case ObserverCameraPreset.BedsideLeft:
                position = anchor - right * bedsideDistance + Vector3.up * bedsideHeight;
                rotation = Quaternion.LookRotation((lookTarget - position).normalized, Vector3.up);
                return true;

            case ObserverCameraPreset.BedsideRight:
                position = anchor + right * bedsideDistance + Vector3.up * bedsideHeight;
                rotation = Quaternion.LookRotation((lookTarget - position).normalized, Vector3.up);
                return true;

            case ObserverCameraPreset.CornerFrontLeft:
                position = anchor - right * cornerDistance - forward * cornerDepth + Vector3.up * cornerHeight;
                rotation = Quaternion.LookRotation((lookTarget - position).normalized, Vector3.up);
                return true;

            case ObserverCameraPreset.CornerFrontRight:
                position = anchor + right * cornerDistance - forward * cornerDepth + Vector3.up * cornerHeight;
                rotation = Quaternion.LookRotation((lookTarget - position).normalized, Vector3.up);
                return true;

            case ObserverCameraPreset.PartnerEyes:
                if (joyBodyController != null && joyBodyController.headBone != null)
                {
                    var head = joyBodyController.headBone;
                    position = head.position + head.forward * partnerEyeOffset;
                    var target = ResolvePartnerEyeLookTarget(position);
                    rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);
                    return true;
                }
                break;

            case ObserverCameraPreset.UserShoulder:
                if (cameraRig != null && cameraRig.centerEyeAnchor != null)
                {
                    var eye = cameraRig.centerEyeAnchor;
                    position = eye.position - eye.forward * userShoulderDistance + eye.right * 0.18f + Vector3.up * userShoulderHeight;
                    rotation = Quaternion.LookRotation(eye.forward, Vector3.up);
                    return true;
                }
                break;
        }

        position = observerCamera != null ? observerCamera.transform.position : Vector3.zero;
        rotation = observerCamera != null ? observerCamera.transform.rotation : Quaternion.identity;
        return observerCamera != null;
    }

    private Vector3 ResolvePartnerEyeLookTarget(Vector3 fallbackPosition)
    {
        if (cameraRig != null && cameraRig.centerEyeAnchor != null)
        {
            return cameraRig.centerEyeAnchor.position;
        }

        if (joyBodyController != null && joyBodyController.headBone != null)
        {
            return joyBodyController.headBone.position + joyBodyController.headBone.forward * partnerEyeLookDistance;
        }

        return fallbackPosition + Vector3.forward * partnerEyeLookDistance;
    }

    private Vector3 GetSceneAnchor()
    {
        if (avatarDriver != null && avatarDriver.bedTransform != null)
        {
            return avatarDriver.bedTransform.position;
        }

        if (joyBodyController != null && joyBodyController.partnerRoot != null)
        {
            return joyBodyController.partnerRoot.position;
        }

        if (avatarDriver != null)
        {
            return avatarDriver.transform.position + avatarDriver.sceneOffset;
        }

        return transform.position;
    }

    private bool GetBedBasis(Vector3 anchor, out Vector3 right, out Vector3 forward)
    {
        if (avatarDriver != null && avatarDriver.bedTransform != null)
        {
            right = avatarDriver.bedTransform.right.normalized;
            forward = avatarDriver.bedTransform.forward.normalized;
            return true;
        }

        if (cameraRig != null && cameraRig.centerEyeAnchor != null)
        {
            var projectedForward = Vector3.ProjectOnPlane(cameraRig.centerEyeAnchor.forward, Vector3.up).normalized;
            if (projectedForward.sqrMagnitude > 0.001f)
            {
                forward = projectedForward;
                right = Vector3.Cross(Vector3.up, forward).normalized;
                return true;
            }
        }

        forward = Vector3.forward;
        right = Vector3.right;
        return false;
    }

    void OnDestroy()
    {
        if (_observerTexture != null)
        {
            _observerTexture.Release();
        }
    }
}
