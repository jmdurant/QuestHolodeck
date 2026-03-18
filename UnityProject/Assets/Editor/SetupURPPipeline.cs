using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SetupURPPipeline
{
    [MenuItem("Tools/Setup URP Pipeline")]
    public static void Setup()
    {
        // Create the URP asset
        string settingsDir = "Assets/Settings";
        if (!AssetDatabase.IsValidFolder(settingsDir))
            AssetDatabase.CreateFolder("Assets", "Settings");

        // Create Universal Renderer Data
        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        string rendererPath = settingsDir + "/URP_Renderer.asset";
        AssetDatabase.CreateAsset(rendererData, rendererPath);
        Debug.Log("[SetupURP] Created renderer data at " + rendererPath);

        // Create URP Pipeline Asset with that renderer
        var pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
        string pipelinePath = settingsDir + "/URP_PipelineAsset.asset";
        AssetDatabase.CreateAsset(pipelineAsset, pipelinePath);
        Debug.Log("[SetupURP] Created pipeline asset at " + pipelinePath);

        // Configure for Quest 3 VR
        pipelineAsset.renderScale = 1.0f;
        pipelineAsset.msaaSampleCount = 4;
        pipelineAsset.supportsHDR = false;
        EditorUtility.SetDirty(pipelineAsset);

        // Assign to Graphics Settings
        GraphicsSettings.defaultRenderPipeline = pipelineAsset;
        Debug.Log("[SetupURP] Assigned pipeline to Graphics Settings");

        // Assign to all Quality levels
        var qualityNames = QualitySettings.names;
        int currentLevel = QualitySettings.GetQualityLevel();
        for (int i = 0; i < qualityNames.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipelineAsset;
            Debug.Log($"[SetupURP] Assigned pipeline to quality level: {qualityNames[i]}");
        }
        QualitySettings.SetQualityLevel(currentLevel, false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupURP] Done! URP pipeline is now active.");
    }
}
