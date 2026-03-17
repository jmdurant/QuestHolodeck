using UnityEngine;

public class ExperienceModeController : MonoBehaviour
{
    public enum ExperienceMode
    {
        Conversation,
        Activity,
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
    public ObserverCameraController observerCameraController;
    public Canvas hudCanvas;
    public GameObject roomEnvironmentRoot;

    [Header("Conversation Staging")]
    public bool keepConversationFramed = false;
    private Transform _eyeTransform;

    void Start()
    {
        ResolveReferences();

        if (applyModeOnStart)
        {
            SetMode(defaultMode, true);
        }
    }

    void LateUpdate()
    {
        ResolveReferences();
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
            hudCanvas.gameObject.SetActive(false);
        }

        if (observerCameraController != null)
        {
            observerCameraController.observerEnabled = false;
            observerCameraController.pipEnabled = false;
            observerCameraController.gameObject.SetActive(false);
        }

        if (avatarDriver != null)
        {
            avatarDriver.SetPrimitiveVisibility(false, false);
        }

            if (joyBodyController != null)
            {
                joyBodyController.stageAtBedSideByDefault = false;
                joyBodyController.rotateRootTowardLookTarget = false;
                joyBodyController.enableHeadLook = true;
                joyBodyController.followUserHeadByDefault = true;
                joyBodyController.ClearTargets(0.1f);
            }
    }

    private void ApplyActivityMode()
    {
        if (roomEnvironmentRoot != null)
        {
            roomEnvironmentRoot.SetActive(true);
        }

        if (passthroughManager != null)
        {
            var enablePassthrough = roomMeshLoader == null || roomMeshLoader.usePassthrough;
            passthroughManager.SetPassthrough(enablePassthrough);
        }

        if (mainCamera != null)
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

        if (joyBodyController != null)
        {
            joyBodyController.enableHeadLook = true;
            joyBodyController.rotateRootTowardLookTarget = true;
            joyBodyController.followUserHeadByDefault = true;
        }
    }

    private void ResolveReferences()
    {
        cameraRig ??= FindFirstObjectByType<OVRCameraRig>();
        mainCamera ??= Camera.main;
        joyBodyController ??= FindFirstObjectByType<JoyBodyController>();
        avatarDriver ??= FindFirstObjectByType<SexKitAvatarDriver>();
        roomMeshLoader ??= FindFirstObjectByType<RoomMeshLoader>();
        passthroughManager ??= FindFirstObjectByType<PassthroughManager>();
        observerCameraController ??= FindFirstObjectByType<ObserverCameraController>();

        if (hudCanvas == null)
        {
            var hud = FindFirstObjectByType<SexKitHUD>();
            hudCanvas = hud != null ? hud.GetComponentInParent<Canvas>() : null;
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
}
