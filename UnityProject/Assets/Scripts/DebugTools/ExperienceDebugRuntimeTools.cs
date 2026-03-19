using System.Collections;
using System.Reflection;
using UnityEngine;

public static class ExperienceDebugRuntimeTools
{
    private static readonly (string modeName, string skyboxName)[] ActivityEnvironmentCycleSequence =
    {
        ("Passthrough", "beach"),
        ("Skybox", "beach"),
        ("Skybox", "aurora"),
        ("Skybox", "clouds"),
        ("Skybox", "space"),
        ("Skybox", "meadow"),
        ("Void", "beach"),
        ("CustomMesh", "beach"),
    };

    public static bool ApplyMode(string modeName)
    {
        var controller = FindAny<ExperienceModeController>();
        if (controller == null)
        {
            Debug.LogError("[ExperienceDebugRuntimeTools] Missing ExperienceModeController.");
            return false;
        }

        if (!System.Enum.TryParse(modeName, true, out ExperienceModeController.ExperienceMode mode))
        {
            Debug.LogError($"[ExperienceDebugRuntimeTools] Unknown mode '{modeName}'.");
            return false;
        }

        controller.SetMode(mode, true);
        Debug.Log($"[ExperienceDebugRuntimeTools] Applied mode {mode}.");
        return true;
    }

    public static bool ApplyActivityEnvironment(string modeName, string skyboxName = "beach")
    {
        var modeController = FindAny<ExperienceModeController>();
        var environmentManager = FindAny<EnvironmentManager>();

        if (modeController == null || environmentManager == null)
        {
            Debug.LogError("[ExperienceDebugRuntimeTools] Missing controller or environment manager.");
            return false;
        }

        if (!System.Enum.TryParse(modeName, true, out EnvironmentManager.EnvironmentMode environmentMode))
        {
            Debug.LogError($"[ExperienceDebugRuntimeTools] Unknown environment mode '{modeName}'.");
            return false;
        }

        modeController.activityEnvironmentMode = environmentMode;
        modeController.activitySkyboxName = skyboxName;
        modeController.SetMode(ExperienceModeController.ExperienceMode.Activity, true);
        environmentManager.SetEnvironment(environmentMode, skyboxName);

        Debug.Log($"[ExperienceDebugRuntimeTools] Activity -> {environmentMode} ({skyboxName})");
        return true;
    }

    public static void StartActivityEnvironmentCycle(float delaySeconds = 5f)
    {
        var existing = Object.FindFirstObjectByType<ActivityEnvironmentCycleRunner>();
        if (existing != null)
            Object.Destroy(existing.gameObject);

        var runnerObject = new GameObject("ActivityEnvironmentCycleRunner");
        Object.DontDestroyOnLoad(runnerObject);
        var runner = runnerObject.AddComponent<ActivityEnvironmentCycleRunner>();
        runner.Begin(ActivityEnvironmentCycleSequence, delaySeconds);
        Debug.Log("[ExperienceDebugRuntimeTools] Started activity environment cycling.");
    }

    public static void StopActivityEnvironmentCycle()
    {
        var existing = Object.FindFirstObjectByType<ActivityEnvironmentCycleRunner>();
        if (existing != null)
        {
            Object.Destroy(existing.gameObject);
            Debug.Log("[ExperienceDebugRuntimeTools] Stopped activity environment cycling.");
        }
    }

    public static void RefreshFallbackRoom()
    {
        var loader = FindAny<RoomMeshLoader>();
        if (loader == null)
        {
            Debug.LogError("[ExperienceDebugRuntimeTools] Missing RoomMeshLoader.");
            return;
        }

        loader.RefreshFallbackVisuals();
        Debug.Log("[ExperienceDebugRuntimeTools] Refreshed fallback room.");
    }

    public static void InspectFallbackRoom()
    {
        var loader = FindAny<RoomMeshLoader>();
        if (loader == null)
        {
            Debug.LogError("[ExperienceDebugRuntimeTools] Missing RoomMeshLoader.");
            return;
        }

        var type = typeof(RoomMeshLoader);
        var bed = (GameObject)type.GetField("_bedInstance", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(loader);
        var left = (GameObject)type.GetField("_leftBedsideTable", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(loader);
        var right = (GameObject)type.GetField("_rightBedsideTable", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(loader);
        var pillowLeft = (GameObject)type.GetField("_leftPillow", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(loader);
        var pillowRight = (GameObject)type.GetField("_rightPillow", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(loader);

        Debug.Log(
            "[ExperienceDebugRuntimeTools] " +
            $"bed={(bed != null ? bed.name : "null")} " +
            $"leftTable={(left != null ? left.name : "null")} " +
            $"rightTable={(right != null ? right.name : "null")} " +
            $"leftPillow={(pillowLeft != null ? pillowLeft.name : "null")} " +
            $"rightPillow={(pillowRight != null ? pillowRight.name : "null")}");
    }

    public static T FindAny<T>() where T : Object
    {
        var live = Object.FindFirstObjectByType<T>();
        if (live != null)
            return live;

        foreach (var candidate in Resources.FindObjectsOfTypeAll<T>())
        {
            if (candidate != null)
                return candidate;
        }

        return null;
    }
}

public class ActivityEnvironmentCycleRunner : MonoBehaviour
{
    private (string modeName, string skyboxName)[] _sequence;
    private float _delaySeconds;

    public void Begin((string modeName, string skyboxName)[] sequence, float delaySeconds)
    {
        _sequence = sequence;
        _delaySeconds = delaySeconds;
        StartCoroutine(Cycle());
    }

    private IEnumerator Cycle()
    {
        var index = 0;
        while (true)
        {
            var entry = _sequence[index];
            ExperienceDebugRuntimeTools.ApplyActivityEnvironment(entry.modeName, entry.skyboxName);
            index = (index + 1) % _sequence.Length;
            yield return new WaitForSeconds(_delaySeconds);
        }
    }
}
