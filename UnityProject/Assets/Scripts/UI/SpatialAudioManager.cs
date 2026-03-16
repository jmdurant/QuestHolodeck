// SpatialAudioManager.cs
// SexKit Quest App
//
// Positions audio sources at Body B's location in 3D space
// Agent's voice comes from where the avatar is standing/lying
//
// Updated for Meta XR SDK v85:
//   - Uses Meta XR Audio Source (NOT deprecated OVRAudioSource)
//   - Meta XR Audio SDK for HRTF spatialization

using UnityEngine;

public class SpatialAudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    // Use standard Unity AudioSource with Meta XR Audio SDK spatialization
    // Do NOT use OVRAudioSource (end-of-life since v47)
    public AudioSource agentVoice;
    public AudioSource ambientAudio;

    [Header("References")]
    public AIAgentController agentController;

    [Header("Settings")]
    public bool spatializeVoice = true;
    public float voiceMaxDistance = 5f;
    public float voiceMinDistance = 0.5f;

    void Start()
    {
        if (agentVoice != null && spatializeVoice)
        {
            // Standard Unity AudioSource with spatial settings
            // Meta XR Audio SDK intercepts and applies HRTF automatically
            // when the Meta XR Audio package is imported
            agentVoice.spatialize = true;
            agentVoice.spatialBlend = 1.0f;  // fully 3D
            agentVoice.minDistance = voiceMinDistance;
            agentVoice.maxDistance = voiceMaxDistance;
            agentVoice.rolloffMode = AudioRolloffMode.Linear;

            // If using Meta XR Audio Source component (recommended):
            // Add MetaXRAudioSource component to the same GameObject
            // It provides HRTF, room acoustics, and Universal HRTF
        }
    }

    void Update()
    {
        if (agentController == null || agentVoice == null) return;

        // Position voice at agent's head
        Vector3 headPos = agentController.GetAgentJoint("head");
        if (headPos != Vector3.zero)
        {
            agentVoice.transform.position = headPos;
        }
    }

    /// Play a voice clip from the agent's position
    public void PlayVoice(AudioClip clip)
    {
        if (agentVoice != null && clip != null)
        {
            agentVoice.clip = clip;
            agentVoice.Play();
        }
    }

    /// Play TTS from agent's position
    public void SpeakText(string text)
    {
        // Options:
        // 1. Android TTS via AndroidJavaObject (free, on-device)
        // 2. Pre-generate clips from AI TTS API
        // 3. Unity TTS plugin
        Debug.Log($"[Agent Voice] \"{text}\" at {agentVoice?.transform.position}");
    }
}
