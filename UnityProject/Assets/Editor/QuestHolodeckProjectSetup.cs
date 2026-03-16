using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.CompositionLayers;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

public static class QuestHolodeckProjectSetup
{
    private const string ScenePath = "Assets/Scenes/SexKitScene.unity";
    private const string ApkOutputPath = "Builds/QuestHolodeck.apk";
    private const string OpenXRLoaderTypeName = "UnityEngine.XR.OpenXR.OpenXRLoader";

    [MenuItem("Tools/Quest Holodeck/Run Project Setup")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        EnsureFolders();
        ConfigureBuildTarget();
        ConfigurePlayerSettings();
        ConfigureXR();
        ConfigureMetaProjectSettings();
        EnsureTextMeshProResources();
        EnsureAndroidManifest();
        CreateOrUpdateScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[QuestHolodeckSetup] Project configuration complete.");
    }

    public static void RunAndExit()
    {
        try
        {
            Run();
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void BuildApkAndExit()
    {
        try
        {
            Run();
            BuildApk();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Editor");
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/XR");
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void ConfigureBuildTarget()
    {
        var androidPlayerPath = System.IO.Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer");
        if (!System.IO.Directory.Exists(androidPlayerPath))
        {
            throw new BuildFailedException("Unity Android Build Support is not installed for this editor. Install Android Build Support from Unity Hub, then rerun Tools > Quest Holodeck > Run Project Setup.");
        }

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new BuildFailedException("Failed to switch the active build target to Android.");
            }
        }

        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        EditorUserBuildSettings.buildAppBundle = false;
    }

    private static void ConfigurePlayerSettings()
    {
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.docto.questholodeck");
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
        PlayerSettings.Android.forceInternetPermission = true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.virtualRealitySplashScreen = null;
    }

    private static void ConfigureXR()
    {
        var settingsPerBuildTarget = GetOrCreateXRGeneralSettings();
        if (!settingsPerBuildTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
        {
            settingsPerBuildTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        }

        var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        if (generalSettings == null)
        {
            throw new BuildFailedException("Unable to create XR general settings for Android.");
        }

        generalSettings.InitManagerOnStart = true;
        var assigned = XRPackageMetadataStore.AssignLoader(generalSettings.AssignedSettings, OpenXRLoaderTypeName, BuildTargetGroup.Android);
        if (!assigned && !XRPackageMetadataStore.IsLoaderAssigned(OpenXRLoaderTypeName, BuildTargetGroup.Android))
        {
            throw new BuildFailedException("Unable to assign the OpenXR loader for Android.");
        }

        EditorUtility.SetDirty(generalSettings);
        EditorUtility.SetDirty(generalSettings.AssignedSettings);

        var openXRSettings = GetOrCreateOpenXRSettings(BuildTargetGroup.Android);
        if (openXRSettings == null)
        {
            throw new BuildFailedException("OpenXR settings for Android were not created.");
        }

        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        openXRSettings = GetOrCreateOpenXRSettings(BuildTargetGroup.Android);
        EnableFeature<MetaQuestFeature>(openXRSettings);
        EnableFeature<HandTracking>(openXRSettings);
        EnableFeature<MetaHandTrackingAim>(openXRSettings);
        EnableFeature<OpenXRCompositionLayersFeature>(openXRSettings);
        EnableFeature<HandInteractionProfile>(openXRSettings);
        EnableFeature<OculusTouchControllerProfile>(openXRSettings);
        EnableFeature<MetaQuestTouchPlusControllerProfile>(openXRSettings);

        EditorUtility.SetDirty(openXRSettings);
    }

    private static OpenXRSettings GetOrCreateOpenXRSettings(BuildTargetGroup buildTargetGroup)
    {
        var existing = OpenXRSettings.GetSettingsForBuildTargetGroup(buildTargetGroup);
        if (existing != null)
        {
            return existing;
        }

        var packageSettingsType = Type.GetType("UnityEditor.XR.OpenXR.OpenXRPackageSettings, Unity.XR.OpenXR.Editor");
        if (packageSettingsType == null)
        {
            return null;
        }

        var getOrCreateInstance = packageSettingsType.GetMethod("GetOrCreateInstance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        var packageSettings = getOrCreateInstance?.Invoke(null, null);
        if (packageSettings == null)
        {
            return null;
        }

        var getSettingsForBuildTargetGroup = packageSettingsType.GetMethod("GetSettingsForBuildTargetGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(BuildTargetGroup) }, null);
        var settings = getSettingsForBuildTargetGroup?.Invoke(packageSettings, new object[] { buildTargetGroup }) as OpenXRSettings;
        if (settings != null)
        {
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty((UnityEngine.Object)packageSettings);
            AssetDatabase.SaveAssets();
        }

        return settings;
    }

    private static XRGeneralSettingsPerBuildTarget GetOrCreateXRGeneralSettings()
    {
        if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget settingsPerBuildTarget)
            && settingsPerBuildTarget != null)
        {
            return settingsPerBuildTarget;
        }

        settingsPerBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
        const string assetPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        AssetDatabase.CreateAsset(settingsPerBuildTarget, assetPath);
        AssetDatabase.SaveAssets();
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settingsPerBuildTarget, true);
        return settingsPerBuildTarget;
    }

    private static void EnableFeature<TFeature>(OpenXRSettings settings) where TFeature : UnityEngine.XR.OpenXR.Features.OpenXRFeature
    {
        var feature = settings.GetFeature<TFeature>();
        if (feature == null)
        {
            Debug.LogWarning($"[QuestHolodeckSetup] OpenXR feature missing: {typeof(TFeature).Name}");
            return;
        }

        feature.enabled = true;
        EditorUtility.SetDirty(feature);
    }

    private static void ConfigureMetaProjectSettings()
    {
        var config = OVRProjectConfig.CachedProjectConfig;
        if (config == null)
        {
            throw new BuildFailedException("OVRProjectConfig could not be created.");
        }

        config.targetDeviceTypes = new List<OVRProjectConfig.DeviceType>
        {
            OVRProjectConfig.DeviceType.Quest3,
            OVRProjectConfig.DeviceType.Quest3S,
        };
        config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
        config.handTrackingFrequency = OVRProjectConfig.HandTrackingFrequency.LOW;
        config.eyeTrackingSupport = OVRProjectConfig.FeatureSupport.None;
        config.bodyTrackingSupport = OVRProjectConfig.FeatureSupport.None;
        config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Required;
        config.systemLoadingScreenBackground = OVRProjectConfig.SystemLoadingScreenBackground.ContextualPassthrough;
        OVRProjectConfig.CommitProjectConfig(config);
    }

    private static void EnsureAndroidManifest()
    {
        if (!OVRManifestPreprocessor.DoesAndroidManifestExist())
        {
            OVRManifestPreprocessor.GenerateManifestForSubmission();
        }
    }

    private static void EnsureTextMeshProResources()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            TMP_PackageResourceImporter.ImportResources(importEssentials: true, importExamples: false, interactive: false);
            AssetDatabase.Refresh();
        }
    }

    private static void CreateOrUpdateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "SexKitScene";

        CreateDirectionalLight();
        var eventSystem = CreateEventSystem();
        var cameraRig = CreateCameraRig();
        var ovrManager = cameraRig != null ? cameraRig.GetComponent<OVRManager>() ?? cameraRig.GetComponentInChildren<OVRManager>() : null;

        var sexKitManager = new GameObject("SexKitManager");
        var wsClient = sexKitManager.AddComponent<SexKitWebSocketClient>();
        sexKitManager.AddComponent<UnityMainThreadDispatcher>();
        var bonjourDiscovery = sexKitManager.AddComponent<BonjourDiscovery>();
        wsClient.serverAddress = string.Empty;
        wsClient.autoReconnect = true;
        bonjourDiscovery.serviceType = "_sexkit-stream._tcp.";
        bonjourDiscovery.scanTimeout = 10f;

        var trackingRoot = new GameObject("TrackingMerge");
        var trackingMerge = trackingRoot.AddComponent<QuestTrackingMerge>();
        var trackingUpstream = trackingRoot.AddComponent<QuestTrackingUpstream>();
        trackingMerge.questHeadTransform = cameraRig != null ? cameraRig.centerEyeAnchor : null;
        trackingMerge.mergeHeadTracking = true;
        trackingMerge.mergeHandTracking = true;
        trackingMerge.enableEyeTracking = false;
        trackingUpstream.tracking = trackingMerge;
        trackingUpstream.wsClient = wsClient;
        trackingUpstream.sendUpstream = true;
        trackingUpstream.sendRate = 30f;

        var avatarRoot = new GameObject("AvatarSystem");
        var avatarDriver = avatarRoot.AddComponent<SexKitAvatarDriver>();
        var metaAvatarBridge = avatarRoot.AddComponent<MetaAvatarBridge>();
        var aiAgentController = avatarRoot.AddComponent<AIAgentController>();
        avatarDriver.mode = SexKitAvatarDriver.AvatarMode.Primitive;
        avatarDriver.sceneOffset = new Vector3(0f, 0f, -2f);
        metaAvatarBridge.useMetaAvatars = true;
        metaAvatarBridge.cameraRig = cameraRig;
        metaAvatarBridge.questTracking = trackingMerge;
        aiAgentController.mode = AIAgentController.AgentMode.RuleBased;
        aiAgentController.userTracking = trackingMerge;
        aiAgentController.avatarDriver = avatarDriver;
        aiAgentController.enableEyeContact = true;

        var roomRoot = new GameObject("RoomEnvironment");
        var roomMeshLoader = roomRoot.AddComponent<RoomMeshLoader>();
        var passthroughManager = roomRoot.AddComponent<PassthroughManager>();
        roomMeshLoader.usePassthrough = true;
        roomMeshLoader.autoPlaceFromCalibration = true;
        passthroughManager.enablePassthrough = true;
        passthroughManager.ovrManager = ovrManager;
        passthroughManager.cameraRig = cameraRig;

        var connectionCanvas = CreateConnectionCanvas(bonjourDiscovery);
        var hudCanvas = CreateHudCanvas();
        var audioRoot = new GameObject("AgentAudio");
        var agentVoiceObject = new GameObject("AgentVoice");
        agentVoiceObject.transform.SetParent(audioRoot.transform, false);
        var agentVoice = agentVoiceObject.AddComponent<AudioSource>();
        agentVoice.playOnAwake = false;
        agentVoice.spatialBlend = 1f;
        agentVoice.minDistance = 0.5f;
        agentVoice.maxDistance = 5f;
        var spatialAudioManager = audioRoot.AddComponent<SpatialAudioManager>();
        spatialAudioManager.agentVoice = agentVoice;
        spatialAudioManager.agentController = aiAgentController;
        spatialAudioManager.spatializeVoice = true;

        if (eventSystem != null)
        {
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }

        SceneManager.MoveGameObjectToScene(sexKitManager, scene);
        SceneManager.MoveGameObjectToScene(trackingRoot, scene);
        SceneManager.MoveGameObjectToScene(avatarRoot, scene);
        SceneManager.MoveGameObjectToScene(roomRoot, scene);
        SceneManager.MoveGameObjectToScene(connectionCanvas, scene);
        SceneManager.MoveGameObjectToScene(hudCanvas, scene);
        SceneManager.MoveGameObjectToScene(audioRoot, scene);

        if (cameraRig != null)
        {
            SceneManager.MoveGameObjectToScene(cameraRig.gameObject, scene);
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
        };
    }

    private static void BuildApk()
    {
        CleanupTransientXRBuildAssets();

        var outputDirectory = System.IO.Path.GetDirectoryName(ApkOutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            System.IO.Directory.CreateDirectory(outputDirectory);
        }

        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = ApkOutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new BuildFailedException($"Android build failed with result: {report.summary.result}");
        }

        Debug.Log($"[QuestHolodeckSetup] Built APK at {ApkOutputPath}");
    }

    private static void CleanupTransientXRBuildAssets()
    {
        if (AssetDatabase.DeleteAsset("Assets/XR/Temp"))
        {
            AssetDatabase.Refresh();
        }
    }

    private static void CreateDirectionalLight()
    {
        var lightObject = new GameObject("Directional Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static GameObject CreateEventSystem()
    {
        var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            return existing.gameObject;
        }

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
        return eventSystem;
    }

    private static OVRCameraRig CreateCameraRig()
    {
        var prefabGuid = AssetDatabase.FindAssets("OVRCameraRig t:Prefab").FirstOrDefault();
        if (string.IsNullOrEmpty(prefabGuid))
        {
            Debug.LogWarning("[QuestHolodeckSetup] OVRCameraRig prefab not found. Creating a fallback camera.");
            var fallback = new GameObject("OVRCameraRig");
            var camera = fallback.AddComponent<Camera>();
            fallback.AddComponent<AudioListener>();
            camera.tag = "MainCamera";
            return fallback.AddComponent<OVRCameraRig>();
        }

        var path = AssetDatabase.GUIDToAssetPath(prefabGuid);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            throw new BuildFailedException("Failed to instantiate OVRCameraRig prefab.");
        }

        instance.name = "OVRCameraRig";
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;
        return instance.GetComponent<OVRCameraRig>();
    }

    private static GameObject CreateConnectionCanvas(BonjourDiscovery bonjourDiscovery)
    {
        var canvasObject = CreateUIRoot("ConnectionCanvas", RenderMode.ScreenSpaceOverlay, null);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.pixelPerfect = false;

        var rootPanel = CreatePanel("ConnectPanel", canvasObject.transform, new Color(0.06f, 0.07f, 0.09f, 0.88f));
        StretchToParent(rootPanel.GetComponent<RectTransform>(), 0.15f, 0.15f, 0.15f, 0.15f);

        var layout = rootPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = false;

        var title = CreateText("TitleText", rootPanel.transform, "SexKit Quest", 48, TextAlignmentOptions.Center);
        SetPreferredHeight(title.rectTransform, 72f);
        var subtitle = CreateText("SubtitleText", rootPanel.transform, "Auto-discover or connect manually", 24, TextAlignmentOptions.Center);
        SetPreferredHeight(subtitle.rectTransform, 40f);

        var addressInput = CreateInputField("AddressInput", rootPanel.transform, "ws://192.168.1.5:8080");
        SetPreferredHeight(addressInput.GetComponent<RectTransform>(), 70f);

        var buttonRow = CreateUIObject("ButtonRow", rootPanel.transform);
        var buttonRowLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        buttonRowLayout.spacing = 16f;
        buttonRowLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonRowLayout.childForceExpandWidth = true;
        buttonRowLayout.childControlHeight = false;
        SetPreferredHeight(buttonRow.GetComponent<RectTransform>(), 72f);

        var connectButton = CreateButton("ConnectButton", buttonRow.transform, "Connect");
        var scanButton = CreateButton("ScanButton", buttonRow.transform, "Scan Network");
        SetPreferredHeight(connectButton.GetComponent<RectTransform>(), 72f);
        SetPreferredHeight(scanButton.GetComponent<RectTransform>(), 72f);

        var statusText = CreateText("StatusText", rootPanel.transform, "Waiting to scan...", 22, TextAlignmentOptions.Center);
        SetPreferredHeight(statusText.rectTransform, 44f);

        var connectedPanel = CreatePanel("ConnectedPanel", canvasObject.transform, new Color(0.06f, 0.07f, 0.09f, 0.88f));
        StretchToParent(connectedPanel.GetComponent<RectTransform>(), 0.15f, 0.2f, 0.15f, 0.2f);
        connectedPanel.SetActive(false);

        var connectedLayout = connectedPanel.AddComponent<VerticalLayoutGroup>();
        connectedLayout.padding = new RectOffset(40, 40, 40, 40);
        connectedLayout.spacing = 18f;
        connectedLayout.childAlignment = TextAnchor.UpperCenter;
        connectedLayout.childForceExpandWidth = true;
        connectedLayout.childControlHeight = false;

        var frameCountText = CreateText("FrameCountText", connectedPanel.transform, "0 frames", 32, TextAlignmentOptions.Center);
        SetPreferredHeight(frameCountText.rectTransform, 52f);
        var dataPreviewText = CreateText("DataPreviewText", connectedPanel.transform, "No frame data yet", 24, TextAlignmentOptions.TopLeft);
        SetPreferredHeight(dataPreviewText.rectTransform, 180f);
        dataPreviewText.textWrappingMode = TextWrappingModes.Normal;
        var disconnectButton = CreateButton("DisconnectButton", connectedPanel.transform, "Disconnect");
        SetPreferredHeight(disconnectButton.GetComponent<RectTransform>(), 72f);

        var connectionUi = canvasObject.AddComponent<ConnectionUI>();
        connectionUi.addressInput = addressInput;
        connectionUi.connectButton = connectButton;
        connectionUi.disconnectButton = disconnectButton;
        connectionUi.scanButton = scanButton;
        connectionUi.statusText = statusText;
        connectionUi.frameCountText = frameCountText;
        connectionUi.dataPreviewText = dataPreviewText;
        connectionUi.connectPanel = rootPanel;
        connectionUi.connectedPanel = connectedPanel;
        connectionUi.bonjourDiscovery = bonjourDiscovery;
        return canvasObject;
    }

    private static GameObject CreateHudCanvas()
    {
        var hudCanvas = CreateUIRoot("HUDCanvas", RenderMode.WorldSpace, null);
        var canvas = hudCanvas.GetComponent<Canvas>();
        canvas.transform.position = new Vector3(0f, 1.4f, 1.5f);
        canvas.transform.localScale = Vector3.one * 0.001f;

        var background = CreatePanel("HudPanel", hudCanvas.transform, new Color(0.08f, 0.09f, 0.11f, 0.85f));
        var backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.sizeDelta = new Vector2(900f, 520f);
        backgroundRect.anchoredPosition = Vector2.zero;

        var heartRateText = CreateText("HeartRateText", background.transform, "HR --", 38, TextAlignmentOptions.TopLeft);
        heartRateText.rectTransform.anchoredPosition = new Vector2(24f, -24f);
        SetAnchoredSize(heartRateText.rectTransform, 300f, 56f);

        var partnerHeartRateText = CreateText("PartnerHeartRateText", background.transform, "Partner --", 32, TextAlignmentOptions.TopRight);
        partnerHeartRateText.rectTransform.anchoredPosition = new Vector2(-24f, -24f);
        SetAnchoredSize(partnerHeartRateText.rectTransform, 320f, 56f);
        partnerHeartRateText.rectTransform.anchorMin = new Vector2(1f, 1f);
        partnerHeartRateText.rectTransform.anchorMax = new Vector2(1f, 1f);
        partnerHeartRateText.rectTransform.pivot = new Vector2(1f, 1f);

        var positionText = CreateText("PositionText", background.transform, "Position", 30, TextAlignmentOptions.TopLeft);
        positionText.rectTransform.anchoredPosition = new Vector2(24f, -110f);
        SetAnchoredSize(positionText.rectTransform, 400f, 48f);

        var timerText = CreateText("TimerText", background.transform, "00:00", 30, TextAlignmentOptions.TopLeft);
        timerText.rectTransform.anchoredPosition = new Vector2(24f, -170f);
        SetAnchoredSize(timerText.rectTransform, 220f, 48f);

        var intensityText = CreateText("IntensityText", background.transform, "Gentle", 30, TextAlignmentOptions.TopLeft);
        intensityText.rectTransform.anchoredPosition = new Vector2(24f, -230f);
        SetAnchoredSize(intensityText.rectTransform, 260f, 48f);

        var connectionText = CreateText("ConnectionText", background.transform, "Disconnected", 30, TextAlignmentOptions.TopLeft);
        connectionText.rectTransform.anchoredPosition = new Vector2(24f, -290f);
        SetAnchoredSize(connectionText.rectTransform, 300f, 48f);

        var dataSourceText = CreateText("DataSourceText", background.transform, "Tier 0: Unknown", 26, TextAlignmentOptions.TopLeft);
        dataSourceText.rectTransform.anchoredPosition = new Vector2(24f, -350f);
        SetAnchoredSize(dataSourceText.rectTransform, 800f, 64f);
        dataSourceText.textWrappingMode = TextWrappingModes.Normal;

        var hud = hudCanvas.AddComponent<SexKitHUD>();
        hud.heartRateText = heartRateText;
        hud.partnerHeartRateText = partnerHeartRateText;
        hud.positionText = positionText;
        hud.timerText = timerText;
        hud.intensityText = intensityText;
        hud.connectionText = connectionText;
        hud.dataSourceText = dataSourceText;
        hud.followHead = true;
        hud.distanceFromHead = 1.5f;
        hud.heightOffset = -0.3f;

        return hudCanvas;
    }

    private static GameObject CreateUIRoot(string name, RenderMode renderMode, Transform parent)
    {
        var root = new GameObject(name);
        root.layer = LayerMask.NameToLayer("UI");
        var rectTransform = root.AddComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = renderMode;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        return root;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        var panel = CreateUIObject(name, parent);
        var image = panel.AddComponent<Image>();
        image.color = color;
        return panel;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.layer = LayerMask.NameToLayer("UI");
        var rectTransform = go.AddComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.localScale = Vector3.one;
        return go;
    }

    private static Button CreateButton(string name, Transform parent, string label)
    {
        var buttonObject = CreateUIObject(name, parent);
        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.46f, 0.36f, 1f);
        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        var text = CreateText("Label", buttonObject.transform, label, 24, TextAlignmentOptions.Center);
        StretchToParent(text.rectTransform, 0f, 0f, 0f, 0f);
        return button;
    }

    private static TMP_InputField CreateInputField(string name, Transform parent, string placeholder)
    {
        var inputObject = CreateUIObject(name, parent);
        var image = inputObject.AddComponent<Image>();
        image.color = new Color(0.13f, 0.14f, 0.18f, 1f);
        var input = inputObject.AddComponent<TMP_InputField>();

        var textArea = CreateUIObject("Text Area", inputObject.transform);
        StretchToParent(textArea.GetComponent<RectTransform>(), 18f, 14f, 18f, 14f);

        var placeholderText = CreateText("Placeholder", textArea.transform, placeholder, 24, TextAlignmentOptions.Left);
        placeholderText.color = new Color(0.7f, 0.72f, 0.76f, 0.7f);
        StretchToParent(placeholderText.rectTransform, 0f, 0f, 0f, 0f);

        var inputText = CreateText("Text", textArea.transform, string.Empty, 24, TextAlignmentOptions.Left);
        StretchToParent(inputText.rectTransform, 0f, 0f, 0f, 0f);

        input.textViewport = textArea.GetComponent<RectTransform>();
        input.textComponent = inputText;
        input.placeholder = placeholderText;
        input.targetGraphic = image;
        return input;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float fontSize, TextAlignmentOptions alignment)
    {
        var textObject = CreateUIObject(name, parent);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.96f, 0.97f, 0.98f, 1f);
        text.raycastTarget = false;
        StretchToParent(text.rectTransform, 0f, 0f, 0f, 0f);
        return text;
    }

    private static void StretchToParent(RectTransform rectTransform, float left, float right, float top, float bottom)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, -top);
    }

    private static void SetPreferredHeight(RectTransform rectTransform, float height)
    {
        var layout = rectTransform.gameObject.GetComponent<LayoutElement>() ?? rectTransform.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
    }

    private static void SetAnchoredSize(RectTransform rectTransform, float width, float height)
    {
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.sizeDelta = new Vector2(width, height);
    }
}
