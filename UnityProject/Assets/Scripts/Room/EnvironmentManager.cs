// EnvironmentManager.cs
// SexKit Quest App
//
// Manages the visual environment surrounding the user.
// Supports: passthrough (real room), skybox (360 photos), or custom meshes.
// The bed anchor from SceneUnderstanding stays the same regardless of environment.
// JOY positions relative to the bed — the world around her is swappable.
//
// Environments can be switched by:
//   - User in Settings
//   - Agent via set_environment ControlFrame
//   - Persona system (Luna gets beach, Sarah gets hotel)

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
    public Material customSkyboxMaterial;   // user-imported 360 photo

    [Header("Ambient Lighting")]
    public Color passthroughAmbient = new Color(0.5f, 0.5f, 0.5f);
    public float skyboxAmbientIntensity = 0.6f;

    public enum EnvironmentMode
    {
        Passthrough,    // Real room via Quest cameras (default)
        Skybox,         // 360 photo environment
        LiDARScan,      // iPhone room scan mesh
        Void            // Dark void — just JOY, nothing else
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

        // Initialize presets if none assigned
        if (presets == null || presets.Length == 0)
        {
            presets = DefaultPresets();
        }

        ApplyEnvironment(currentMode);
    }

    // MARK: - Switch Environment

    public void SetEnvironment(EnvironmentMode mode, string skyboxName = null)
    {
        currentMode = mode;

        if (mode == EnvironmentMode.Skybox && !string.IsNullOrWhiteSpace(skyboxName))
        {
            currentSkyboxName = skyboxName;
        }

        ApplyEnvironment(mode);
    }

    /// Called from ControlFrame environment data
    public void SetEnvironmentFromAgent(string environmentName)
    {
        if (string.IsNullOrWhiteSpace(environmentName)) return;

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

        if (lower == "lidar" || lower == "scan" || lower == "room_scan")
        {
            SetEnvironment(EnvironmentMode.LiDARScan);
            return;
        }

        // Try to match a skybox preset
        foreach (var preset in presets)
        {
            if (preset.name.ToLowerInvariant().Contains(lower) ||
                lower.Contains(preset.name.ToLowerInvariant()))
            {
                SetEnvironment(EnvironmentMode.Skybox, preset.name);
                return;
            }
        }

        // No match — try as raw skybox name anyway
        SetEnvironment(EnvironmentMode.Skybox, environmentName);
    }

    private void ApplyEnvironment(EnvironmentMode mode)
    {
        switch (mode)
        {
            case EnvironmentMode.Passthrough:
                EnablePassthrough(true);
                SetSkyboxMaterial(null);
                SetRoomMeshVisible(false);
                SetAmbient(passthroughAmbient, 1f);
                Debug.Log("[Environment] Passthrough — real room");
                break;

            case EnvironmentMode.Skybox:
                EnablePassthrough(false);
                var preset = FindPreset(currentSkyboxName);
                if (preset != null && preset.material != null)
                {
                    SetSkyboxMaterial(preset.material);
                    SetAmbient(preset.ambientColor, preset.ambientIntensity);
                    Debug.Log($"[Environment] Skybox — {preset.name}");
                }
                else if (customSkyboxMaterial != null)
                {
                    SetSkyboxMaterial(customSkyboxMaterial);
                    SetAmbient(Color.gray, skyboxAmbientIntensity);
                    Debug.Log("[Environment] Custom skybox");
                }
                SetRoomMeshVisible(false);
                break;

            case EnvironmentMode.LiDARScan:
                EnablePassthrough(false);
                SetSkyboxMaterial(null);
                SetRoomMeshVisible(true);
                SetAmbient(Color.gray, 0.4f);
                Debug.Log("[Environment] LiDAR room scan");
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
                SetRoomMeshVisible(false);
                SetAmbient(new Color(0.1f, 0.1f, 0.12f), 0.3f);
                Debug.Log("[Environment] Void — darkness");
                break;
        }
    }

    // MARK: - Helpers

    private void EnablePassthrough(bool enabled)
    {
        if (passthroughManager != null)
        {
            passthroughManager.SetPassthrough(enabled);
        }

        if (mainCamera != null && enabled)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.clear;
        }
    }

    private void SetSkyboxMaterial(Material mat)
    {
        if (mat != null)
        {
            RenderSettings.skybox = mat;
            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.Skybox;
            }
        }
    }

    private void SetRoomMeshVisible(bool visible)
    {
        if (roomMeshLoader == null) return;
        // Room mesh visibility is handled by RoomMeshLoader
        roomMeshLoader.usePassthrough = !visible;
    }

    private void SetAmbient(Color color, float intensity)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = color * intensity;
    }

    private SkyboxPreset FindPreset(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || presets == null) return null;
        var lower = name.ToLowerInvariant();
        foreach (var preset in presets)
        {
            if (preset.name.ToLowerInvariant() == lower) return preset;
        }
        return null;
    }

    // MARK: - Default Presets

    /// Default preset definitions — materials assigned in inspector or loaded from Resources
    private SkyboxPreset[] DefaultPresets()
    {
        return new[]
        {
            new SkyboxPreset
            {
                name = "mountain",
                displayName = "Mountain Night",
                description = "Starry sky, mountain peaks, warm campfire glow",
                ambientColor = new Color(0.15f, 0.15f, 0.25f),
                ambientIntensity = 0.4f,
                material = LoadSkyboxMaterial("Skybox_Mountain")
            },
            new SkyboxPreset
            {
                name = "beach",
                displayName = "Beach Sunset",
                description = "Ocean horizon, golden sunset, palm trees",
                ambientColor = new Color(0.6f, 0.4f, 0.3f),
                ambientIntensity = 0.7f,
                material = LoadSkyboxMaterial("Skybox_Beach")
            },
            new SkyboxPreset
            {
                name = "hotel",
                displayName = "Luxury Suite",
                description = "Floor-to-ceiling windows, city lights, modern",
                ambientColor = new Color(0.35f, 0.35f, 0.4f),
                ambientIntensity = 0.5f,
                material = LoadSkyboxMaterial("Skybox_Hotel")
            },
            new SkyboxPreset
            {
                name = "cabin",
                displayName = "Mountain Cabin",
                description = "Warm wood, fireplace glow, snow outside",
                ambientColor = new Color(0.5f, 0.35f, 0.2f),
                ambientIntensity = 0.5f,
                material = LoadSkyboxMaterial("Skybox_Cabin")
            },
            new SkyboxPreset
            {
                name = "cave",
                displayName = "Candlelit Cave",
                description = "Warm stone walls, flickering firelight, intimate",
                ambientColor = new Color(0.4f, 0.25f, 0.15f),
                ambientIntensity = 0.35f,
                material = LoadSkyboxMaterial("Skybox_Cave")
            },
        };
    }

    private Material LoadSkyboxMaterial(string resourceName)
    {
        // Load from Resources folder — user drops skybox materials there
        return Resources.Load<Material>($"Environments/{resourceName}");
    }
}

// MARK: - Data Types

[System.Serializable]
public class SkyboxPreset
{
    public string name;              // internal name for agent commands
    public string displayName;       // shown to user
    public string description;
    public Material material;        // 6-sided or panoramic skybox material
    public Color ambientColor;       // ambient light color for this environment
    public float ambientIntensity;   // ambient light strength
}
