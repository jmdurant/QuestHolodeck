using UnityEngine;

public class ExperienceModeController : MonoBehaviour
{
    public enum ExperienceMode
    {
        Conversation,
        Activity,
        Training,
    }

    [Header("Mode")]
    public ExperienceMode defaultMode = ExperienceMode.Conversation;
    public ExperienceMode currentMode = ExperienceMode.Conversation;
    public bool applyModeOnStart = true;

    [Header("References")]
    public OVRCameraRig cameraRig;
    public Camera mainCamera;
    public JoyBodyController joyBodyController;
    public SexKitAvatarDriver avatarDriver;
    public RoomMeshLoader roomMeshLoader;
    public PassthroughManager passthroughManager;
    public EnvironmentManager environmentManager;
    public ObserverCameraController observerCameraController;
    public Canvas hudCanvas;
    public GameObject roomEnvironmentRoot;
    public GameObject worldKeyLight;
    public GameObject worldFillLight;
    public GameObject conversationPortraitLightRoot;

    [Header("Conversation Staging")]
    public bool keepConversationFramed = false;
    public Vector3 conversationHeadLookOffset = new(0.0f, -0.04f, 0.0f);
    public bool conversationHudVisibleByDefault = false;
    public bool conversationHudAnchorToJoy = false;
    public Vector3 conversationHudJoyLocalOffset = new(0.0f, 1.42f, -0.35f);
    public float conversationHudHeight = 1.42f;
    public float conversationHudTowardUserDistance = 0.55f;
    public float conversationHudHorizontalOffset = 0.0f;
    public bool activityHudFollowHead = true;

    [Header("Activity Staging")]
    public float activityPartnerReclineDegrees = 0.0f;
    public float activityPartnerMattressLift = 0.10f;

    [Header("Activity Environment")]
    public EnvironmentManager.EnvironmentMode activityEnvironmentMode = EnvironmentManager.EnvironmentMode.Passthrough;
    public string activitySkyboxName = "beach";
    private Transform _eyeTransform;
    private SexKitHUD _sexKitHud;
    private RectTransform _hudPanelRect;
    private bool _conversationHudVisible;
    private bool _hudCanvasStateCaptured;
    private RenderMode _hudOriginalRenderMode;
    private Camera _hudOriginalWorldCamera;
    private float _hudOriginalPlaneDistance;
    private bool _hudOriginalOverrideSorting;
    private int _hudOriginalSortingOrder;

    void Start()
    {
        ResolveReferences();
        _conversationHudVisible = conversationHudVisibleByDefault;

        if (applyModeOnStart)
        {
            SetMode(defaultMode, true);
        }
    }

    void LateUpdate()
    {
        ResolveReferences();

        // Enforce visibility each frame in case startup order creates primitive bodies after mode was applied.
        if (avatarDriver != null)
        {
            if (currentMode == ExperienceMode.Conversation || currentMode == ExperienceMode.Training)
                avatarDriver.SetPrimitiveVisibility(false, false);
            else if (currentMode == ExperienceMode.Activity)
                avatarDriver.SetPrimitiveVisibility(true, true);
        }

        // Enforce local Meta avatar renderer visibility per mode to avoid first-person body parts leaking into conversation view.
        if (currentMode == ExperienceMode.Conversation || currentMode == ExperienceMode.Training)
            SetLocalMetaAvatarVisible(false);
        else if (currentMode == ExperienceMode.Activity)
            SetLocalMetaAvatarVisible(true);

    }

    public void SetMode(ExperienceMode mode, bool snap = false)
    {
        ResolveReferences();
        currentMode = mode;

        switch (mode)
        {
            case ExperienceMode.Conversation:
                ApplyConversationMode();
                break;

            case ExperienceMode.Activity:
                ApplyActivityMode();
                break;

            case ExperienceMode.Training:
                ApplyTrainingMode();
                break;
        }
    }

    private void ApplyConversationMode()
    {
        if (passthroughManager != null)
        {
            passthroughManager.SetPassthrough(false);
        }

        if (mainCamera != null)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.black;
        }

        if (roomEnvironmentRoot != null)
        {
            roomEnvironmentRoot.SetActive(false);
        }

        if (hudCanvas != null)
        {
            hudCanvas.gameObject.SetActive(true);
        }

        if (observerCameraController != null)
        {
            observerCameraController.observerEnabled = false;
            observerCameraController.pipEnabled = false;
            observerCameraController.gameObject.SetActive(false);
        }

        SetConversationPortraitLighting(true);

        if (avatarDriver != null)
        {
            avatarDriver.SetPrimitiveVisibility(false, false);
        }

        SetLocalMetaAvatarVisible(false);

        ConfigureHudForCurrentMode();
        ConfigureConversationHudOnConnectionPlane();
        if (_sexKitHud != null)
            _sexKitHud.SetConversationHrOnly(true);
        ApplyConversationHudVisibility();

            if (joyBodyController != null)
            {
                joyBodyController.stageAtBedSideByDefault = false;
                joyBodyController.RestageForCurrentMode();
                joyBodyController.rotateRootTowardLookTarget = false;
                joyBodyController.enableHeadLook = true;
                joyBodyController.followUserHeadByDefault = true;
                joyBodyController.userHeadLookOffset = conversationHeadLookOffset;
                joyBodyController.ClearTargets(0.1f);
            }
    }

    private void ApplyTrainingMode()
    {
        ApplyConversationMode();
    }

    private void ApplyActivityMode()
    {
        if (roomEnvironmentRoot != null)
        {
            roomEnvironmentRoot.SetActive(true);
        }

        if (environmentManager != null)
        {
            environmentManager.SetEnvironment(activityEnvironmentMode, activitySkyboxName);
        }
        else if (passthroughManager != null)
        {
            var enablePassthrough = roomMeshLoader == null || roomMeshLoader.usePassthrough;
            passthroughManager.SetPassthrough(enablePassthrough);
        }

        if (environmentManager == null && mainCamera != null)
        {
            if (roomMeshLoader != null && roomMeshLoader.usePassthrough)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = Color.clear;
            }
            else
            {
                mainCamera.clearFlags = CameraClearFlags.Skybox;
            }
        }

        if (hudCanvas != null)
        {
            hudCanvas.gameObject.SetActive(true);
        }

        if (observerCameraController != null)
        {
            observerCameraController.gameObject.SetActive(true);
        }

        SetConversationPortraitLighting(false);

        if (joyBodyController != null)
        {
            if (avatarDriver != null)
            {
                avatarDriver.partnerReclineDegrees = activityPartnerReclineDegrees;
                avatarDriver.partnerMattressLift = activityPartnerMattressLift;
            }

            joyBodyController.stageAtBedSideByDefault = true;
            joyBodyController.RestageForCurrentMode();
            joyBodyController.enableHeadLook = true;
            joyBodyController.rotateRootTowardLookTarget = true;
            joyBodyController.followUserHeadByDefault = true;
            joyBodyController.userHeadLookOffset = Vector3.zero;
            joyBodyController.ClearTargets(0.1f);
        }

        StageUserRigToActivityAnchor();

        SetLocalMetaAvatarVisible(true);
        RestoreHudCanvasState();
        ConfigureHudForCurrentMode();
        if (_sexKitHud != null)
            _sexKitHud.SetConversationHrOnly(false);
        ApplyConversationHudVisibility();
    }

    private void ResolveReferences()
    {
        cameraRig ??= FindFirstObjectByType<OVRCameraRig>();
        mainCamera ??= Camera.main;
        joyBodyController ??= FindFirstObjectByType<JoyBodyController>();
        avatarDriver ??= FindFirstObjectByType<SexKitAvatarDriver>();
        roomMeshLoader ??= FindFirstObjectByType<RoomMeshLoader>();
        passthroughManager ??= FindFirstObjectByType<PassthroughManager>();
        environmentManager ??= FindFirstObjectByType<EnvironmentManager>();
        observerCameraController ??= FindFirstObjectByType<ObserverCameraController>();
        worldKeyLight ??= GameObject.Find("Directional Light");
        worldFillLight ??= GameObject.Find("FillLight");
        conversationPortraitLightRoot ??= GameObject.Find("ConversationPortraitLights");

        if (hudCanvas == null)
        {
            var hud = FindFirstObjectByType<SexKitHUD>();
            hudCanvas = hud != null ? hud.GetComponentInParent<Canvas>() : null;
        }

        _sexKitHud ??= FindFirstObjectByType<SexKitHUD>();
        if (_hudPanelRect == null && hudCanvas != null)
        {
            var panel = hudCanvas.transform.Find("HudPanel");
            _hudPanelRect = panel != null ? panel.GetComponent<RectTransform>() : null;
        }

        if (roomEnvironmentRoot == null && roomMeshLoader != null)
        {
            roomEnvironmentRoot = roomMeshLoader.gameObject;
        }

        if (cameraRig != null)
        {
            _eyeTransform = cameraRig.centerEyeAnchor != null ? cameraRig.centerEyeAnchor : cameraRig.transform;
        }
    }

    private void SetConversationPortraitLighting(bool enabled)
    {
        if (conversationPortraitLightRoot != null)
            conversationPortraitLightRoot.SetActive(enabled);

        if (worldKeyLight != null)
            worldKeyLight.SetActive(!enabled);

        if (worldFillLight != null)
            worldFillLight.SetActive(!enabled);
    }

    private void StageUserRigToActivityAnchor()
    {
        if (cameraRig == null || avatarDriver == null)
            return;

        if (!avatarDriver.TryGetDefaultStandingAnchor(true, out var userAnchorPosition, out var userAnchorRotation))
            return;

        var rigTransform = cameraRig.transform;
        var eyeTransform = cameraRig.centerEyeAnchor != null ? cameraRig.centerEyeAnchor : rigTransform;

        float targetEyeHeight = 1.55f;
        if (cameraRig.centerEyeAnchor != null && cameraRig.centerEyeAnchor.localPosition.y > 0.5f)
            targetEyeHeight = cameraRig.centerEyeAnchor.localPosition.y;

        var targetEyePosition = userAnchorPosition + Vector3.up * targetEyeHeight;
        var desiredLookDirection = ResolveActivityLookDirection(targetEyePosition, userAnchorRotation);
        var desiredEyeRotation = Quaternion.LookRotation(desiredLookDirection.normalized, Vector3.up);
        var eyeLocalRotation = Quaternion.Inverse(rigTransform.rotation) * eyeTransform.rotation;
        rigTransform.rotation = desiredEyeRotation * Quaternion.Inverse(eyeLocalRotation);

        var translatedEyePosition = eyeTransform.position;
        rigTransform.position += targetEyePosition - translatedEyePosition;
    }

    private Vector3 ResolveActivityLookDirection(Vector3 targetEyePosition, Quaternion userAnchorRotation)
    {
        var fallbackForward = userAnchorRotation * Vector3.forward;
        var flatForward = Vector3.ProjectOnPlane(fallbackForward, Vector3.up);
        if (flatForward.sqrMagnitude > 0.001f)
            return flatForward.normalized;

        if (avatarDriver != null && avatarDriver.TryGetDefaultStandingAnchor(false, out var partnerAnchorPosition, out _))
        {
            var towardPartner = Vector3.ProjectOnPlane((partnerAnchorPosition + Vector3.up * 0.65f) - targetEyePosition, Vector3.up);
            if (towardPartner.sqrMagnitude > 0.001f)
                return towardPartner.normalized;
        }

        return Vector3.forward;
    }

    public void SetConversationHudAnchorToJoy(bool enabled)
    {
        conversationHudAnchorToJoy = enabled;
        ConfigureHudForCurrentMode();
    }

    public void SetConversationHudVisible(bool visible)
    {
        _conversationHudVisible = visible;
        ApplyConversationHudVisibility();
    }

    public void ToggleConversationHudVisible()
    {
        SetConversationHudVisible(!_conversationHudVisible);
    }

    private void ConfigureHudForCurrentMode()
    {
        if (_sexKitHud == null)
            return;

        if ((currentMode == ExperienceMode.Conversation || currentMode == ExperienceMode.Training) && conversationHudAnchorToJoy)
        {
            var joyAnchor = ResolveJoyAnchor();
            if (joyAnchor != null)
            {
                _sexKitHud.smoothPlacement = false;
                _sexKitHud.anchorFaceCamera = true;
                _sexKitHud.anchorYawOffset = 0f;
                _sexKitHud.SetAnchorTowardCameraPlacement(
                    joyAnchor,
                    conversationHudHeight,
                    conversationHudTowardUserDistance,
                    conversationHudHorizontalOffset
                );
                return;
            }

            // Do not fall back to head-follow in conversation mode while waiting for Joy anchor;
            // that causes visible "zoom/drift" when the anchor appears.
            _sexKitHud.smoothPlacement = false;
            _sexKitHud.SetHeadFollowPlacement(false);
            return;
        }

        if (currentMode == ExperienceMode.Conversation || currentMode == ExperienceMode.Training)
        {
            // Stable fallback for conversation: keep HUD directly in front of the user.
            _sexKitHud.anchorToTarget = false;
            _sexKitHud.followHead = false;
            _sexKitHud.smoothPlacement = false;
            _sexKitHud.distanceFromHead = 0.9f;
            _sexKitHud.heightOffset = 0.30f;
            _sexKitHud.horizontalOffset = -0.42f;
            return;
        }

        _sexKitHud.smoothPlacement = true;
        _sexKitHud.anchorFaceCamera = true;
        _sexKitHud.SetHeadFollowPlacement(activityHudFollowHead);
    }

    private Transform ResolveJoyAnchor()
    {
        if (joyBodyController != null && joyBodyController.modelRoot != null)
            return joyBodyController.modelRoot;

        var joyRoot = GameObject.Find("AvatarSystem/JoyPartner");
        return joyRoot != null ? joyRoot.transform : null;
    }

    private void ConfigureConversationHudOnConnectionPlane()
    {
        if (hudCanvas == null)
            return;

        CaptureHudCanvasStateIfNeeded();

        // Put vitals on the same 2D plane as connection UI.
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.worldCamera = null;
        hudCanvas.overrideSorting = true;
        hudCanvas.sortingOrder = 10;

        var hudRect = hudCanvas.GetComponent<RectTransform>();
        if (hudRect != null)
        {
            hudRect.anchorMin = Vector2.zero;
            hudRect.anchorMax = Vector2.one;
            hudRect.pivot = new Vector2(0.5f, 0.5f);
            hudRect.anchoredPosition = Vector2.zero;
            hudRect.sizeDelta = Vector2.zero;
        }

        if (_hudPanelRect != null)
        {
            _hudPanelRect.anchorMin = new Vector2(0f, 1f);
            _hudPanelRect.anchorMax = new Vector2(0f, 1f);
            _hudPanelRect.pivot = new Vector2(0f, 1f);
            _hudPanelRect.sizeDelta = new Vector2(420f, 120f);
            _hudPanelRect.anchoredPosition = new Vector2(24f, -24f);
            _hudPanelRect.localRotation = Quaternion.identity;
            _hudPanelRect.localScale = Vector3.one;
        }
    }

    private void CaptureHudCanvasStateIfNeeded()
    {
        if (_hudCanvasStateCaptured || hudCanvas == null)
            return;

        _hudOriginalRenderMode = hudCanvas.renderMode;
        _hudOriginalWorldCamera = hudCanvas.worldCamera;
        _hudOriginalPlaneDistance = hudCanvas.planeDistance;
        _hudOriginalOverrideSorting = hudCanvas.overrideSorting;
        _hudOriginalSortingOrder = hudCanvas.sortingOrder;
        _hudCanvasStateCaptured = true;
    }

    private void RestoreHudCanvasState()
    {
        if (!_hudCanvasStateCaptured || hudCanvas == null)
            return;

        hudCanvas.renderMode = _hudOriginalRenderMode;
        hudCanvas.worldCamera = _hudOriginalWorldCamera;
        hudCanvas.planeDistance = _hudOriginalPlaneDistance;
        hudCanvas.overrideSorting = _hudOriginalOverrideSorting;
        hudCanvas.sortingOrder = _hudOriginalSortingOrder;
    }

    private void ApplyConversationHudVisibility()
    {
        if (_hudPanelRect == null)
            return;

        if (currentMode == ExperienceMode.Conversation || currentMode == ExperienceMode.Training)
            _hudPanelRect.gameObject.SetActive(_conversationHudVisible);
        else
            _hudPanelRect.gameObject.SetActive(true);
    }

    private void SetLocalMetaAvatarVisible(bool visible)
    {
        var localMetaRoot = GameObject.Find("AvatarSystem/LocalMetaAvatar");
        if (localMetaRoot == null)
            return;

        var renderers = localMetaRoot.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = visible;
        }
    }
}
