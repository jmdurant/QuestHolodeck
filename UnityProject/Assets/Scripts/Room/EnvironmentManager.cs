// EnvironmentManager.cs
// SexKit Quest App
//
// Manages the visual environment surrounding the user.
// Supports: passthrough (real room), skybox (360 photos), or custom room meshes.
// The bed anchor from SceneUnderstanding stays the same regardless of environment.
// JOY positions relative to the bed; the world around her is swappable.

using UnityEngine;
using UnityEngine.Rendering;

public class EnvironmentManager : MonoBehaviour
{
    public static EnvironmentManager Instance { get; private set; }

    [Header("References")]
    public Camera mainCamera;
    public RoomMeshLoader roomMeshLoader;
    public PassthroughManager passthroughManager;

    [Header("Current Environment")]
    public EnvironmentMode currentMode = EnvironmentMode.Passthrough;
    public string currentSkyboxName = "";

    [Header("Skybox Library")]
    public SkyboxPreset[] presets;

    [Header("Custom Skybox")]
    public Material customSkyboxMaterial;

    [Header("Ambient Lighting")]
    public Color passthroughAmbient = new(0.5f, 0.5f, 0.5f);
    public float skyboxAmbientIntensity = 0.6f;

    public enum EnvironmentMode
    {
        Passthrough,
        Skybox,
        CustomMesh,
        Void,
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        mainCamera ??= Camera.main;
        roomMeshLoader ??= FindFirstObjectByType<RoomMeshLoader>();
        passthroughManager ??= FindFirstObjectByType<PassthroughManager>();

        if (presets == null || presets.Length == 0)
            presets = DefaultPresets();

        ApplyEnvironment(currentMode);
    }

    public void SetEnvironment(EnvironmentMode mode, string skyboxName = null)
    {
        currentMode = mode;

        if (mode == EnvironmentMode.Skybox && !string.IsNullOrWhiteSpace(skyboxName))
            currentSkyboxName = skyboxName;

        ApplyEnvironment(mode);
    }

    public void SetEnvironmentFromAgent(string environmentName)
    {
        if (string.IsNullOrWhiteSpace(environmentName))
            return;

        var lower = environmentName.ToLowerInvariant();

        if (lower == "passthrough" || lower == "real" || lower == "your_room")
        {
            SetEnvironment(EnvironmentMode.Passthrough);
            return;
        }

        if (lower == "void" || lower == "dark" || lower == "minimal")
        {
            SetEnvironment(EnvironmentMode.Void);
            return;
        }

        if (lower == "lidar" || lower == "scan" || lower == "room_scan" ||
            lower == "custom_mesh" || lower == "custommesh" || lower == "mesh")
        {
            SetEnvironment(EnvironmentMode.CustomMesh);
            return;
        }

        foreach (var preset in presets)
        {
            if (preset.name.ToLowerInvariant().Contains(lower) ||
                lower.Contains(preset.name.ToLowerInvariant()))
            {
                SetEnvironment(EnvironmentMode.Skybox, preset.name);
                return;
            }
        }

        SetEnvironment(EnvironmentMode.Skybox, environmentName);
    }

    private void ApplyEnvironment(EnvironmentMode mode)
    {
        switch (mode)
        {
            case EnvironmentMode.Passthrough:
                EnablePassthrough(true);
                SetSkyboxMaterial(null);
                ApplyFallbackPresentation(true);
                SetAmbient(passthroughAmbient, 1f);
                Debug.Log("[Environment] Passthrough - real room");
                break;

            case EnvironmentMode.Skybox:
                EnablePassthrough(false);
                ApplySkyboxEnvironment();
                ApplyFallbackPresentation(false);
                break;

            case EnvironmentMode.CustomMesh:
                EnablePassthrough(false);
                SetSkyboxMaterial(null);
                ApplyFallbackPresentation(false);
                SetAmbient(Color.gray, 0.4f);
                Debug.Log("[Environment] Custom room mesh");
                break;

            case EnvironmentMode.Void:
                EnablePassthrough(false);
                SetSkyboxMaterial(null);
                RenderSettings.skybox = null;
                if (mainCamera != null)
                {
                    mainCamera.clearFlags = CameraClearFlags.SolidColor;
                    mainCamera.backgroundColor = Color.black;
                }

                ApplyFallbackPresentation(false);
                SetAmbient(new Color(0.1f, 0.1f, 0.12f), 0.3f);
                Debug.Log("[Environment] Void - darkness");
                break;
        }
    }

    private void ApplySkyboxEnvironment()
    {
        var preset = FindPreset(currentSkyboxName);
        if (preset != null && preset.material != null)
        {
            SetSkyboxMaterial(preset.material);
            SetAmbient(preset.ambientColor, preset.ambientIntensity);
            Debug.Log($"[Environment] Skybox - {preset.name}");
            return;
        }

        if (customSkyboxMaterial != null)
        {
            SetSkyboxMaterial(customSkyboxMaterial);
            SetAmbient(Color.gray, skyboxAmbientIntensity);
            Debug.Log("[Environment] Custom skybox");
            return;
        }

        RenderSettings.skybox = null;
        if (mainCamera != null)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.black;
        }

        SetAmbient(Color.gray, skyboxAmbientIntensity);
    }

    private void EnablePassthrough(bool enabled)
    {
        if (passthroughManager != null)
            passthroughManager.SetPassthrough(enabled);

        if (mainCamera != null && enabled)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.clear;
        }
    }

    private void SetSkyboxMaterial(Material mat)
    {
        if (mat == null)
            return;

        RenderSettings.skybox = mat;
        if (mainCamera != null)
            mainCamera.clearFlags = CameraClearFlags.Skybox;
    }

    private void ApplyFallbackPresentation(bool passthroughMode)
    {
        if (roomMeshLoader == null)
            return;

        roomMeshLoader.usePassthrough = passthroughMode;
        roomMeshLoader.RefreshFallbackVisuals();
    }

    private void SetAmbient(Color color, float intensity)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = color * intensity;
    }

    private SkyboxPreset FindPreset(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || presets == null)
            return null;

        var lower = name.ToLowerInvariant();
        foreach (var preset in presets)
        {
            if (preset.name.ToLowerInvariant() == lower)
                return preset;
        }

        return null;
    }

    private SkyboxPreset[] DefaultPresets()
    {
        return new[]
        {
            new SkyboxPreset
            {
                name = "beach",
                displayName = "Beach Sunset",
                description = "Golden sands, ocean waves, warm sunset glow",
                ambientColor = new Color(0.6f, 0.4f, 0.3f),
                ambientIntensity = 0.7f,
                material = LoadSkyboxMaterial("Skybox_Beach")
            },
            new SkyboxPreset
            {
                name = "aurora",
                displayName = "Northern Lights",
                description = "Mountain lake, aurora borealis, starry sky",
                ambientColor = new Color(0.15f, 0.25f, 0.2f),
                ambientIntensity = 0.4f,
                material = LoadSkyboxMaterial("Skybox_Aurora")
            },
            new SkyboxPreset
            {
                name = "clouds",
                displayName = "Above the Clouds",
                description = "Floating above cloudscape, ethereal golden sunset",
                ambientColor = new Color(0.5f, 0.45f, 0.35f),
                ambientIntensity = 0.65f,
                material = LoadSkyboxMaterial("Skybox_Clouds")
            },
            new SkyboxPreset
            {
                name = "space",
                displayName = "Deep Space",
                description = "Cosmic nebula, stars, dark and intense",
                ambientColor = new Color(0.12f, 0.08f, 0.18f),
                ambientIntensity = 0.3f,
                material = LoadSkyboxMaterial("Skybox_Space")
            },
            new SkyboxPreset
            {
                name = "meadow",
                displayName = "Iceland Meadow",
                description = "Wildflowers, mountain pond, peaceful sunset",
                ambientColor = new Color(0.4f, 0.45f, 0.3f),
                ambientIntensity = 0.6f,
                material = LoadSkyboxMaterial("Skybox_Meadow")
            },
        };
    }

    private Material LoadSkyboxMaterial(string resourceName)
    {
        return Resources.Load<Material>($"Environments/{resourceName}");
    }
}

[System.Serializable]
public class SkyboxPreset
{
    public string name;
    public string displayName;
    public string description;
    public Material material;
    public Color ambientColor;
    public float ambientIntensity;
}
