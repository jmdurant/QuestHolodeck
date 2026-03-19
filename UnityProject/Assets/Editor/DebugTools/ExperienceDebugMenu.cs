using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExperienceDebugMenu
{
    [MenuItem("QuestHolodeck/Debug/Mode/Conversation")]
    private static void SetConversation() => ExperienceDebugRuntimeTools.ApplyMode("Conversation");

    [MenuItem("QuestHolodeck/Debug/Mode/Training")]
    private static void SetTraining() => ExperienceDebugRuntimeTools.ApplyMode("Training");

    [MenuItem("QuestHolodeck/Debug/Mode/Activity")]
    private static void SetActivity() => ExperienceDebugRuntimeTools.ApplyMode("Activity");

    [MenuItem("QuestHolodeck/Debug/Activity Environment/Passthrough")]
    private static void SetActivityPassthrough() => ExperienceDebugRuntimeTools.ApplyActivityEnvironment("Passthrough", "beach");

    [MenuItem("QuestHolodeck/Debug/Activity Environment/Skybox/Beach")]
    private static void SetSkyboxBeach() => ExperienceDebugRuntimeTools.ApplyActivityEnvironment("Skybox", "beach");

    [MenuItem("QuestHolodeck/Debug/Activity Environment/Skybox/Aurora")]
    private static void SetSkyboxAurora() => ExperienceDebugRuntimeTools.ApplyActivityEnvironment("Skybox", "aurora");

    [MenuItem("QuestHolodeck/Debug/Activity Environment/Skybox/Clouds")]
    private static void SetSkyboxClouds() => ExperienceDebugRuntimeTools.ApplyActivityEnvironment("Skybox", "clouds");

    [MenuItem("QuestHolodeck/Debug/Activity Environment/Skybox/Space")]
    private static void SetSkyboxSpace() => ExperienceDebugRuntimeTools.ApplyActivityEnvironment("Skybox", "space");

    [MenuItem("QuestHolodeck/Debug/Activity Environment/Skybox/Meadow")]
    private static void SetSkyboxMeadow() => ExperienceDebugRuntimeTools.ApplyActivityEnvironment("Skybox", "meadow");

    [MenuItem("QuestHolodeck/Debug/Activity Environment/Void")]
    private static void SetActivityVoid() => ExperienceDebugRuntimeTools.ApplyActivityEnvironment("Void", "beach");

    [MenuItem("QuestHolodeck/Debug/Activity Environment/Custom Mesh")]
    private static void SetActivityCustomMesh() => ExperienceDebugRuntimeTools.ApplyActivityEnvironment("CustomMesh", "beach");

    [MenuItem("QuestHolodeck/Debug/Activity Environment/Start 5s Cycle")]
    private static void StartCycle() => ExperienceDebugRuntimeTools.StartActivityEnvironmentCycle(5f);

    [MenuItem("QuestHolodeck/Debug/Activity Environment/Stop Cycle")]
    private static void StopCycle() => ExperienceDebugRuntimeTools.StopActivityEnvironmentCycle();

    [MenuItem("QuestHolodeck/Debug/Fallback Room/Refresh")]
    private static void RefreshFallbackRoom() => ExperienceDebugRuntimeTools.RefreshFallbackRoom();

    [MenuItem("QuestHolodeck/Debug/Fallback Room/Inspect")]
    private static void InspectFallbackRoom() => ExperienceDebugRuntimeTools.InspectFallbackRoom();

    [MenuItem("QuestHolodeck/Debug/Scene/Mark Dirty")]
    private static void MarkSceneDirty()
    {
        var modeController = ExperienceDebugRuntimeTools.FindAny<ExperienceModeController>();
        if (modeController != null)
            EditorSceneManager.MarkSceneDirty(modeController.gameObject.scene);
    }
}
