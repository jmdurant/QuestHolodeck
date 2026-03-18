using UnityEngine;
using UnityEditor;

public static class FixAmbientLight
{
    [MenuItem("Tools/Fix Ambient Light")]
    public static void Fix()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.42f, 0.4f);
        RenderSettings.ambientIntensity = 1.0f;
        Debug.Log("[Lighting] Ambient set to (0.45, 0.42, 0.40) — bright warm fill");
    }
}
