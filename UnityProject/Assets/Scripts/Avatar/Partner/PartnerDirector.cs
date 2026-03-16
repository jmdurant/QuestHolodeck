using UnityEngine;

public class PartnerDirector : MonoBehaviour, IPartnerDirector
{
    [Header("References")]
    public PartnerCommandBus commandBus;
    public PartnerBodyController bodyController;
    public PartnerFaceController faceController;
    public PartnerVoiceController voiceController;
    public QuestTrackingMerge trackingMerge;
    public SexKitAvatarDriver avatarDriver;

    [Header("Behavior")]
    public float defaultBlendTime = 0.35f;
    public bool acceptLowPriorityUpdates = true;
    public bool resetToIdleOnExpiry = true;

    [Header("Runtime")]
    public PartnerRuntimeState runtimeState = new();

    private ControlFrame _activeFrame;
    private bool _expiredHandled;

    void Start()
    {
        commandBus ??= GetComponent<PartnerCommandBus>();
        bodyController ??= GetComponent<PartnerBodyController>();
        faceController ??= GetComponent<PartnerFaceController>();
        voiceController ??= FindFirstObjectByType<PartnerVoiceController>();
        trackingMerge ??= FindFirstObjectByType<QuestTrackingMerge>();
        avatarDriver ??= GetComponent<SexKitAvatarDriver>();

        if (commandBus != null)
        {
            commandBus.OnControlFrameReceived += Apply;
        }
    }

    void Update()
    {
        Tick(Time.deltaTime);
    }

    public void Apply(ControlFrame frame)
    {
        if (frame == null || !frame.HasMeaningfulPayload())
        {
            return;
        }

        if (!ShouldAccept(frame))
        {
            return;
        }

        _activeFrame = frame;
        _expiredHandled = false;

        var blendTime = Mathf.Max(0.05f, defaultBlendTime);
        var targetPoint = ResolveAttentionTarget(frame);

        runtimeState.mode = string.IsNullOrWhiteSpace(frame.mode) ? runtimeState.mode : frame.mode;
        runtimeState.activePriority = 1f;
        runtimeState.expiresAt = -1f;

        ApplyState(frame, blendTime, targetPoint);
    }

    public void Tick(float deltaTime)
    {
        bodyController?.Tick(deltaTime);
        faceController?.Tick(deltaTime);
        voiceController?.Tick(deltaTime);

        runtimeState.isSpeaking = voiceController != null && voiceController.IsSpeaking;

        if (!resetToIdleOnExpiry || _expiredHandled || runtimeState.expiresAt <= 0f || Time.time < runtimeState.expiresAt)
        {
            return;
        }

        ResetToIdle();
        _expiredHandled = true;
    }

    public PartnerRuntimeState GetState()
    {
        return runtimeState;
    }

    private void ApplyState(ControlFrame frame, float blendTime, Vector3? targetPoint)
    {
        var poseIntent = ResolvePoseIntent(frame);
        runtimeState.poseIntent = poseIntent;
        bodyController?.SetPoseIntent(poseIntent, blendTime);

        var attentionTarget = ResolveAttentionTargetKind(frame);
        bodyController?.SetAttentionTarget(attentionTarget, targetPoint, blendTime);

        if (targetPoint == null)
        {
            bodyController?.ClearTargets(blendTime);
        }

        if (frame.gesture != null && !string.IsNullOrWhiteSpace(frame.gesture.name))
        {
            runtimeState.activeGesture = frame.gesture.name;
            bodyController?.PlayGesture(frame.gesture.name, blendTime);
        }

        var facePreset = ResolveFacePreset(frame);
        runtimeState.facePreset = facePreset;
        faceController?.SetFacePreset(facePreset, ResolveBlendTime(frame, blendTime));
        faceController?.SetEmotion(ResolveEmotion(frame), ResolveEmotionIntensity(frame));

        ApplySpeech(frame);
        ApplyBreathing(frame);
    }

    private void ApplySpeech(ControlFrame frame)
    {
        if (frame.verbal != null && !string.IsNullOrWhiteSpace(frame.verbal.text))
        {
            runtimeState.lastSpeechText = frame.verbal.text;
            voiceController?.Speak(frame.verbal.text, frame.verbal.emotion);
        }
    }

    private bool ShouldAccept(ControlFrame frame)
    {
        if (_activeFrame == null || runtimeState.expiresAt <= 0f || Time.time >= runtimeState.expiresAt)
        {
            return true;
        }

        if (acceptLowPriorityUpdates)
        {
            return true;
        }

        return true;
    }

    private Vector3? ResolveAttentionTarget(ControlFrame frame)
    {
        switch (ResolveAttentionTargetKind(frame))
        {
            case PartnerAttentionTarget.UserFace:
                return trackingMerge != null ? trackingMerge.HeadPosition : null;

            case PartnerAttentionTarget.UserChest:
                return trackingMerge != null ? trackingMerge.HeadPosition + Vector3.down * 0.25f : null;

            case PartnerAttentionTarget.Bed:
                return avatarDriver != null && avatarDriver.bedTransform != null
                    ? avatarDriver.bedTransform.position
                    : null;

            case PartnerAttentionTarget.None:
            default:
                return null;
        }
    }

    private PartnerAttentionTarget ResolveAttentionTargetKind(ControlFrame frame)
    {
        var target = frame.gaze != null ? frame.gaze.target : null;
        if (string.IsNullOrWhiteSpace(target))
        {
            return PartnerAttentionTarget.None;
        }

        return target.ToLowerInvariant() switch
        {
            "user_eyes" => PartnerAttentionTarget.UserFace,
            "user_face" => PartnerAttentionTarget.UserFace,
            "user_chest" => PartnerAttentionTarget.UserChest,
            "bed" => PartnerAttentionTarget.Bed,
            _ => PartnerAttentionTarget.None,
        };
    }

    private PartnerPoseIntent ResolvePoseIntent(ControlFrame frame)
    {
        if (frame.physical != null && !string.IsNullOrWhiteSpace(frame.physical.position))
        {
            switch (frame.physical.position.ToLowerInvariant())
            {
                case "missionary":
                    return PartnerPoseIntent.Reclined;
                case "cowgirl":
                    return PartnerPoseIntent.Kneeling;
            }
        }

        if (frame.mode != null)
        {
            switch (frame.mode.ToLowerInvariant())
            {
                case "physical":
                    return PartnerPoseIntent.Comforting;
                case "adjust":
                    return PartnerPoseIntent.Brace;
                case "suggest":
                    return PartnerPoseIntent.LeanBack;
            }
        }

        return PartnerPoseIntent.Idle;
    }

    private PartnerFacePreset ResolveFacePreset(ControlFrame frame)
    {
        var value = frame.expression != null ? frame.expression.expression : null;
        if (string.IsNullOrWhiteSpace(value))
        {
            value = frame.verbal != null ? frame.verbal.emotion : null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return PartnerFacePreset.Neutral;
        }

        return value.ToLowerInvariant() switch
        {
            "pleasure" => PartnerFacePreset.Pleasure,
            "intense_pleasure" => PartnerFacePreset.IntensePleasure,
            "breathless" => PartnerFacePreset.Breathless,
            "gentle" => PartnerFacePreset.Gentle,
            "warm" => PartnerFacePreset.Warm,
            "teasing" => PartnerFacePreset.Teasing,
            "focused" => PartnerFacePreset.Focused,
            "concerned" => PartnerFacePreset.Concerned,
            _ => PartnerFacePreset.Neutral,
        };
    }

    private string ResolveEmotion(ControlFrame frame)
    {
        if (frame.verbal != null && !string.IsNullOrWhiteSpace(frame.verbal.emotion))
        {
            runtimeState.emotion = frame.verbal.emotion;
            return frame.verbal.emotion;
        }

        if (frame.expression != null && !string.IsNullOrWhiteSpace(frame.expression.expression))
        {
            runtimeState.emotion = frame.expression.expression;
            return frame.expression.expression;
        }

        return runtimeState.emotion;
    }

    private float ResolveEmotionIntensity(ControlFrame frame)
    {
        if (frame.expression != null && frame.expression.intensity > 0f)
        {
            return Mathf.Clamp01(frame.expression.intensity);
        }

        if (frame.reaction != null && frame.reaction.intensity > 0f)
        {
            return Mathf.Clamp01(frame.reaction.intensity);
        }

        if (frame.verbal != null && frame.verbal.urgency > 0f)
        {
            return Mathf.Clamp01(frame.verbal.urgency);
        }

        return 0.25f;
    }

    private float ResolveBlendTime(ControlFrame frame, float fallback)
    {
        if (frame.expression != null && frame.expression.blendTime > 0f)
        {
            return frame.expression.blendTime;
        }

        return fallback;
    }

    private void ApplyBreathing(ControlFrame frame)
    {
        runtimeState.breathingRate = frame.breathing != null ? frame.breathing.rate : runtimeState.breathingRate;
        runtimeState.breathingDepth = frame.breathing != null ? frame.breathing.depth : runtimeState.breathingDepth;
        runtimeState.physicalRhythmHz = frame.physical != null ? frame.physical.rhythmHz : runtimeState.physicalRhythmHz;
        runtimeState.physicalAmplitude = frame.physical != null ? frame.physical.amplitude : runtimeState.physicalAmplitude;
        runtimeState.physicalIntensity = frame.physical != null ? frame.physical.intensity : runtimeState.physicalIntensity;
    }

    private void ResetToIdle()
    {
        runtimeState.activePriority = 0f;
        runtimeState.poseIntent = PartnerPoseIntent.Idle;
        runtimeState.facePreset = PartnerFacePreset.Neutral;
        runtimeState.activeGesture = string.Empty;
        runtimeState.expiresAt = -1f;

        bodyController?.SetPoseIntent(PartnerPoseIntent.Idle, defaultBlendTime);
        bodyController?.ClearTargets(defaultBlendTime);
        faceController?.SetFacePreset(PartnerFacePreset.Neutral, defaultBlendTime);
        faceController?.SetEmotion("neutral", 0f);
    }

    void OnDestroy()
    {
        if (commandBus != null)
        {
            commandBus.OnControlFrameReceived -= Apply;
        }
    }
}
