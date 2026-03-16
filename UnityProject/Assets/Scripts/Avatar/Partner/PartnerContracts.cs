using System;
using UnityEngine;

public interface IPartnerDirector
{
    void Apply(ControlFrame frame);
    void Tick(float deltaTime);
    PartnerRuntimeState GetState();
}

public interface IPartnerBodyController
{
    void SetPoseIntent(PartnerPoseIntent intent, float blendTime);
    void SetAttentionTarget(PartnerAttentionTarget target, Vector3? worldTarget, float blendTime);
    void SetHandTargets(Vector3? leftTarget, Vector3? rightTarget, float blendTime);
    void PlayGesture(string gestureName, float blendTime);
    void ClearTargets(float blendTime);
    void Tick(float deltaTime);
}

public interface IPartnerFaceController
{
    void SetFacePreset(PartnerFacePreset preset, float blendTime);
    void SetEmotion(string emotion, float intensity);
    void SetSpeechState(float mouthOpen, string speechStyle);
    void SetViseme(string viseme, float weight);
    void ClearVisemes(float blendTime = 0.1f);
    void Tick(float deltaTime);
}

public interface IPartnerVoiceController
{
    void Speak(string text, string style);
    void Stop();
    bool IsSpeaking { get; }
    void Tick(float deltaTime);
}

[Serializable]
public class PartnerRuntimeState
{
    public string mode = "idle";
    public string emotion = "neutral";
    public string activeGesture = string.Empty;
    public string lastSpeechText = string.Empty;
    public float activePriority;
    public float expiresAt = -1f;
    public bool isSpeaking;
    public float breathingRate;
    public float breathingDepth;
    public float physicalRhythmHz;
    public float physicalIntensity;
    public float physicalAmplitude;
    public PartnerPoseIntent poseIntent = PartnerPoseIntent.Idle;
    public PartnerFacePreset facePreset = PartnerFacePreset.Neutral;
}
