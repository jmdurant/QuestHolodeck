using UnityEngine;
using UnityEditor;

public static class SetupConversationLighting
{
    [MenuItem("Tools/Setup Conversation Lighting")]
    public static void Setup()
    {
        // Ambient light — fill the shadow side so it's not black
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.25f, 0.22f, 0.2f); // warm grey fill
        RenderSettings.ambientIntensity = 1.0f;

        // Key light — slightly above, facing Joy (+Z direction)
        var light = Object.FindFirstObjectByType<Light>();
        if (light != null)
        {
            light.transform.eulerAngles = new Vector3(35f, 180f, 0f); // from front, 35° down
            light.intensity = 1.0f;
            light.color = new Color(1f, 0.97f, 0.92f); // slightly warm white
            light.shadows = LightShadows.Soft;
            Debug.Log("[Lighting] Key light: front, 35° down, warm white");
        }

        // Create fill light if one doesn't exist
        var allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        Light fillLight = null;
        foreach (var l in allLights)
        {
            if (l.gameObject.name == "FillLight")
            {
                fillLight = l;
                break;
            }
        }

        if (fillLight == null)
        {
            var fillGO = new GameObject("FillLight");
            fillLight = fillGO.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            Debug.Log("[Lighting] Created fill light");
        }

        fillLight.transform.eulerAngles = new Vector3(20f, 340f, 0f); // from front-right, gentle
        fillLight.intensity = 0.4f;
        fillLight.color = new Color(0.85f, 0.9f, 1f); // slightly cool for contrast
        fillLight.shadows = LightShadows.None; // fill light doesn't cast shadows

        EditorUtility.SetDirty(fillLight.gameObject);
        Debug.Log("[Lighting] Fill light: front-right, 20° down, cool, no shadows");
        Debug.Log("[Lighting] Ambient: warm grey (0.25, 0.22, 0.20)");
        Debug.Log("[Lighting] Done — 3-point portrait lighting for conversation mode");
    }
}
