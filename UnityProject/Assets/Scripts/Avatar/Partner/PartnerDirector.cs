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

        if (frame.gesture != null && !string.IsNullOrWhiteSpace(frame.gesture.ResolvedType))
        {
            runtimeState.activeGesture = frame.gesture.ResolvedType;
            // Gesture intensity scales the blend time — subtle gestures blend slower
            var gestureBlend = frame.gesture.intensity > 0.01f
                ? Mathf.Lerp(blendTime * 2f, blendTime * 0.5f, frame.gesture.intensity)
                : blendTime;
            bodyController?.PlayGesture(frame.gesture.ResolvedType, gestureBlend);
        }

        var facePreset = ResolveFacePreset(frame);
        runtimeState.facePreset = facePreset;
        faceController?.SetFacePreset(facePreset, ResolveBlendTime(frame, blendTime));
        faceController?.SetEmotion(ResolveEmotion(frame), ResolveEmotionIntensity(frame));

        ApplyMovement(frame);
        ApplyPostureDetails(frame);
        ApplyGazeDetails(frame);
        ApplySpeech(frame);
        ApplyBreathing(frame);
        ApplyReaction(frame);
        ApplyAdjustMode(frame);
    }

    private void ApplySpeech(ControlFrame frame)
    {
        if (frame.verbal == null || string.IsNullOrWhiteSpace(frame.verbal.text)) return;

        runtimeState.lastSpeechText = frame.verbal.text;

        // Urgency affects speech rate — pass as style modifier
        var style = frame.verbal.emotion ?? "neutral";
        if (frame.verbal.urgency > 0.7f)
        {
            style += "_urgent";
        }
        else if (frame.verbal.urgency < 0.3f)
        {
            style += "_slow";
        }

        voiceController?.Speak(frame.verbal.text, style);

        // Urgency also affects jaw animation intensity
        faceController?.SetSpeechState(
            0.3f + frame.verbal.urgency * 0.4f,
            frame.verbal.emotion);
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
            "user_head" => PartnerAttentionTarget.UserFace,
            "user_body" => PartnerAttentionTarget.UserChest,
            "user_chest" => PartnerAttentionTarget.UserChest,
            "user_hands" => PartnerAttentionTarget.UserChest,  // look toward hands area
            "user_hand" => PartnerAttentionTarget.UserChest,
            "user_shoulder" => PartnerAttentionTarget.UserChest,
            "bed" => PartnerAttentionTarget.Bed,
            "away" => PartnerAttentionTarget.None,
            "down" => PartnerAttentionTarget.Bed,
            "closed" => PartnerAttentionTarget.None,
            "follow_user" => PartnerAttentionTarget.UserFace,
            _ => PartnerAttentionTarget.None,
        };
    }

    private PartnerPoseIntent ResolvePoseIntent(ControlFrame frame)
    {
        // Posture field takes priority — explicit body language command
        if (frame.posture != null && !string.IsNullOrWhiteSpace(frame.posture.ResolvedState))
        {
            switch (frame.posture.ResolvedState.ToLowerInvariant())
            {
                case "standing": return PartnerPoseIntent.Idle;
                case "sitting": return PartnerPoseIntent.LeanBack;
                case "lying_back": return PartnerPoseIntent.Reclined;
                case "lying_face_down": return PartnerPoseIntent.Brace;
                case "lying_side": return PartnerPoseIntent.Comforting;
                case "kneeling": return PartnerPoseIntent.Kneeling;
                case "crouching": return PartnerPoseIntent.Kneeling;
            }
        }

        // Physical position — map all 22 positions to pose intents
        if (frame.physical != null && !string.IsNullOrWhiteSpace(frame.physical.position))
        {
            switch (frame.physical.position.ToLowerInvariant())
            {
                // Bottom positions (partner is below/receiving)
                case "missionary":
                case "missionary (kneeling)":
                    return PartnerPoseIntent.Reclined;

                // Top positions (partner is above/riding)
                case "cowgirl":
                case "reverse cowgirl":
                    return PartnerPoseIntent.Kneeling;

                // Behind positions
                case "doggy style":
                case "prone":
                    return PartnerPoseIntent.Brace;

                // Side positions
                case "spooning":
                case "side by side":
                    return PartnerPoseIntent.Comforting;

                // Upright positions
                case "standing":
                case "seated":
                case "lotus":
                    return PartnerPoseIntent.LeanIn;

                // Oral positions
                case "oral":
                case "oral (giving to male)":
                case "oral (giving to female)":
                case "oral (receiving male)":
                case "oral (receiving female)":
                    return PartnerPoseIntent.Kneeling;

                case "69":
                    return PartnerPoseIntent.Reclined;

                // Manual positions
                case "handjob (giving)":
                case "fingering (giving)":
                    return PartnerPoseIntent.ReachRight;
                case "handjob (receiving)":
                case "fingering (receiving)":
                    return PartnerPoseIntent.Reclined;

                default:
                    return PartnerPoseIntent.Comforting;
            }
        }

        // Mode-based fallback
        if (frame.mode != null)
        {
            switch (frame.mode.ToLowerInvariant())
            {
                case "physical": return PartnerPoseIntent.Comforting;
                case "adjust": return PartnerPoseIntent.Brace;
                case "suggest": return PartnerPoseIntent.LeanBack;
                case "conversation": return PartnerPoseIntent.LeanIn;
                case "approaching": return PartnerPoseIntent.Idle;
                case "transition": return PartnerPoseIntent.Comforting;
                case "resolution": return PartnerPoseIntent.Comforting;
                case "idle": return PartnerPoseIntent.Idle;
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
            "eyes_closed_bliss" => PartnerFacePreset.IntensePleasure,
            "breathless" => PartnerFacePreset.Breathless,
            "gentle" => PartnerFacePreset.Gentle,
            "warm" => PartnerFacePreset.Warm,
            "smile_soft" => PartnerFacePreset.SoftSmile,
            "smile_warm" => PartnerFacePreset.Warm,
            "teasing" => PartnerFacePreset.Teasing,
            "playful" => PartnerFacePreset.Teasing,
            "focused" => PartnerFacePreset.Focused,
            "concerned" => PartnerFacePreset.Concerned,
            "desire" => PartnerFacePreset.Warm,
            "tenderness" => PartnerFacePreset.Gentle,
            "bite_lip" => PartnerFacePreset.Focused,
            "surprise" => PartnerFacePreset.Concerned,
            "post_orgasm_peace" => PartnerFacePreset.Gentle,
            "passionate" => PartnerFacePreset.Pleasure,
            "commanding" => PartnerFacePreset.Focused,
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
        // Update runtime state from breathing
        if (frame.breathing != null)
        {
            runtimeState.breathingRate = frame.breathing.rate;
            runtimeState.breathingDepth = frame.breathing.depth;

            // Pattern affects breathing style
            if (!string.IsNullOrWhiteSpace(frame.breathing.pattern))
            {
                switch (frame.breathing.pattern.ToLowerInvariant())
                {
                    case "held":
                        // Breath held — freeze chest animation briefly
                        runtimeState.breathingRate = 2f;  // near-zero rate
                        runtimeState.breathingDepth = 0.1f;
                        break;
                    case "panting":
                        // Fast shallow breathing
                        runtimeState.breathingRate = Mathf.Max(runtimeState.breathingRate, 30f);
                        runtimeState.breathingDepth = Mathf.Min(runtimeState.breathingDepth, 0.4f);
                        break;
                    case "deep_slow":
                        // Calming breaths — slow and deep
                        runtimeState.breathingRate = Mathf.Min(runtimeState.breathingRate, 10f);
                        runtimeState.breathingDepth = Mathf.Max(runtimeState.breathingDepth, 0.8f);
                        break;
                    case "box":
                        // 4-4-4-4 pattern — steady and controlled
                        runtimeState.breathingRate = 4f;  // ~4 breaths/min in box pattern
                        runtimeState.breathingDepth = 1.0f;
                        break;
                    case "synced":
                    case "natural":
                        // Use rate/depth as provided
                        break;
                }
            }

            // Audible — toggle breathing audio on voice controller
            if (voiceController != null && frame.breathing.audible)
            {
                // Breathing sounds use speech state with low mouth open
                // This creates audible breathing through the spatial audio system
                if (runtimeState.breathingRate > 20f)
                {
                    faceController?.SetSpeechState(0.1f + runtimeState.breathingDepth * 0.15f, "breathing");
                }
            }
        }

        // Update runtime state from physical
        // Only for non-adjust mode (adjust mode handled separately in ApplyAdjustMode)
        if (frame.physical != null && (frame.mode == null || frame.mode.ToLowerInvariant() != "adjust"))
        {
            if (frame.physical.rhythmHz > 0) runtimeState.physicalRhythmHz = frame.physical.rhythmHz;
            if (frame.physical.amplitude > 0) runtimeState.physicalAmplitude = frame.physical.amplitude;
            if (frame.physical.intensity > 0) runtimeState.physicalIntensity = frame.physical.intensity;
        }

        // Drive JoyBodyController breathing + rhythm
        if (bodyController is JoyBodyController joyBody)
        {
            joyBody.SetBreathing(runtimeState.breathingRate, runtimeState.breathingDepth);
            joyBody.SetRhythm(runtimeState.physicalRhythmHz, runtimeState.physicalIntensity, runtimeState.physicalAmplitude);
        }
    }

    // ── Movement (move_to tool) ──

    private void ApplyMovement(ControlFrame frame)
    {
        if (frame.movement == null) return;

        // Resolve target position from named location or coordinates
        Vector3? target = null;

        if (frame.movement.targetPosition != null && frame.movement.targetPosition.Length >= 3)
        {
            target = new Vector3(
                frame.movement.targetPosition[0],
                frame.movement.targetPosition[1],
                frame.movement.targetPosition[2]);
        }
        else if (!string.IsNullOrWhiteSpace(frame.movement.targetLocation))
        {
            target = ResolveNamedLocation(frame.movement.targetLocation);
        }

        if (target.HasValue && bodyController != null)
        {
            // Move root toward target position
            // TODO: Replace with NavMeshAgent.SetDestination when NavMesh is baked
            var speed = frame.movement.speed?.ToLowerInvariant() switch
            {
                "walk" => 1.0f,
                "approach" => 0.5f,
                "quick" => 2.0f,
                "teleport" => 100f,
                _ => 1.0f
            };

            // For now, lerp toward target. NavMesh will replace this.
            if (bodyController.partnerRoot != null)
            {
                var direction = target.Value - bodyController.partnerRoot.position;
                if (direction.magnitude > 0.05f)
                {
                    bodyController.partnerRoot.position = Vector3.MoveTowards(
                        bodyController.partnerRoot.position,
                        target.Value,
                        Time.deltaTime * speed);

                    // Face direction of travel
                    if (direction.sqrMagnitude > 0.01f)
                    {
                        var moveRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                        bodyController.partnerRoot.rotation = Quaternion.Slerp(
                            bodyController.partnerRoot.rotation,
                            moveRotation,
                            Time.deltaTime * 4f);
                    }
                }
            }

            // Target rotation — explicit Y-axis rotation override
            if (frame.movement.targetRotation != 0f && bodyController.partnerRoot != null)
            {
                var targetRot = Quaternion.Euler(0f, frame.movement.targetRotation, 0f);
                bodyController.partnerRoot.rotation = Quaternion.Slerp(
                    bodyController.partnerRoot.rotation,
                    targetRot,
                    Time.deltaTime * 4f);
            }

            // Arrived action
            if (!string.IsNullOrWhiteSpace(frame.movement.arrivedAction))
            {
                var arrivedPose = frame.movement.arrivedAction.ToLowerInvariant() switch
                {
                    "stand" => PartnerPoseIntent.Idle,
                    "sit" => PartnerPoseIntent.LeanBack,
                    "lie_down" => PartnerPoseIntent.Reclined,
                    "kneel" => PartnerPoseIntent.Kneeling,
                    _ => PartnerPoseIntent.Idle
                };
                // Queue the arrived pose — it will trigger when close enough
                runtimeState.poseIntent = arrivedPose;
            }
        }
    }

    private Vector3? ResolveNamedLocation(string location)
    {
        if (avatarDriver == null) return null;

        return location.ToLowerInvariant() switch
        {
            "bed" => avatarDriver.bedTransform != null ? avatarDriver.bedTransform.position : null,
            "beside_user" => trackingMerge != null ? trackingMerge.HeadPosition + Vector3.left * 0.5f : null,
            "foot_of_bed" => avatarDriver.bedTransform != null
                ? avatarDriver.bedTransform.position + Vector3.forward * 1.0f
                : null,
            "doorway" => avatarDriver.bedTransform != null
                ? avatarDriver.bedTransform.position + Vector3.back * 3.0f
                : null,
            "standing_near" => trackingMerge != null ? trackingMerge.HeadPosition + Vector3.back * 1.0f : null,
            _ => null
        };
    }

    // ── Posture Details (lean, openness, facing) ──

    private void ApplyPostureDetails(ControlFrame frame)
    {
        if (frame.posture == null) return;

        // Facing — override attention target from posture
        if (!string.IsNullOrWhiteSpace(frame.posture.facing))
        {
            var attTarget = frame.posture.facing.ToLowerInvariant() switch
            {
                "user_head" => PartnerAttentionTarget.UserFace,
                "user_body" => PartnerAttentionTarget.UserChest,
                "away" => PartnerAttentionTarget.None,
                "forward" => PartnerAttentionTarget.None,
                _ => PartnerAttentionTarget.UserFace
            };
            var worldTarget = ResolveAttentionTarget(new ControlFrame { gaze = new ControlGaze { target = frame.posture.facing } });
            bodyController?.SetAttentionTarget(attTarget, worldTarget, defaultBlendTime);
        }

        // Lean — modulate forward/back offset on body controller
        if (frame.posture.lean > 0.01f && bodyController is JoyBodyController joyBody)
        {
            var leanOffset = new Vector3(0f, 0f, -frame.posture.lean * 0.3f);
            joyBody.SetPoseIntent(PartnerPoseIntent.LeanIn, defaultBlendTime);
        }

        // Openness — wider stance / arm position (subtle)
        // Handled through the existing pose intent system — higher openness = more relaxed pose
        if (frame.posture.openness > 0.6f)
        {
            // Open body language — if not already in a specific pose, use Comforting
            if (runtimeState.poseIntent == PartnerPoseIntent.Idle)
            {
                bodyController?.SetPoseIntent(PartnerPoseIntent.Comforting, defaultBlendTime);
            }
        }
    }

    // ── Gaze Details (intensity, behavior) ──

    private void ApplyGazeDetails(ControlFrame frame)
    {
        if (frame.gaze == null) return;

        // Behavior affects face and body response
        if (!string.IsNullOrWhiteSpace(frame.gaze.behavior))
        {
            switch (frame.gaze.behavior.ToLowerInvariant())
            {
                case "soft_contact":
                    faceController?.SetFacePreset(PartnerFacePreset.Gentle, 0.5f);
                    break;
                case "intense_contact":
                    faceController?.SetFacePreset(PartnerFacePreset.Focused, 0.3f);
                    break;
                case "body_glance":
                    // Brief look at body then back to eyes — shift attention target
                    bodyController?.SetAttentionTarget(PartnerAttentionTarget.UserChest, null, 0.2f);
                    break;
                case "look_away":
                    bodyController?.SetAttentionTarget(PartnerAttentionTarget.None, null, 0.4f);
                    break;
                case "eyes_closed":
                    faceController?.SetFacePreset(PartnerFacePreset.IntensePleasure, 0.3f);
                    bodyController?.SetAttentionTarget(PartnerAttentionTarget.None, null, 0.3f);
                    break;
                case "follow_user":
                    // Continuous head tracking — set to face with high lerp
                    bodyController?.SetAttentionTarget(PartnerAttentionTarget.UserFace,
                        trackingMerge != null ? trackingMerge.HeadPosition : null, 0.1f);
                    break;
            }
        }

        // Intensity modulates how quickly the head snaps to target
        // High intensity = fast tracking, low = lazy/casual gaze
        // This is handled by the body controller's rotationLerpSpeed
        // For now, we modulate the blend time inversely with intensity
    }

    // ── Adjust Mode (delta values, not absolute) ──

    private void ApplyAdjustMode(ControlFrame frame)
    {
        if (frame.mode == null || frame.mode.ToLowerInvariant() != "adjust") return;
        if (frame.physical == null) return;

        // Delta rhythm — ADD to current value, don't replace
        if (Mathf.Abs(frame.physical.rhythmHz) > 0.01f)
        {
            runtimeState.physicalRhythmHz = Mathf.Clamp(
                runtimeState.physicalRhythmHz + frame.physical.rhythmHz,
                0.3f, 4.0f);
        }

        // Delta intensity — ADD to current value
        if (Mathf.Abs(frame.physical.intensity) > 0.01f)
        {
            runtimeState.physicalIntensity = Mathf.Clamp01(
                runtimeState.physicalIntensity + frame.physical.intensity);
        }

        // Push updated values to body controller
        if (bodyController is JoyBodyController joyBody)
        {
            joyBody.SetRhythm(runtimeState.physicalRhythmHz, runtimeState.physicalIntensity, runtimeState.physicalAmplitude);
        }
    }

    // ── Reactions ──

    private void ApplyReaction(ControlFrame frame)
    {
        if (frame.reaction == null || string.IsNullOrWhiteSpace(frame.reaction.type))
            return;

        var intensity = Mathf.Clamp01(frame.reaction.intensity);
        var reactionType = frame.reaction.type.ToLowerInvariant();

        // Map reactions to body + face + voice responses
        switch (reactionType)
        {
            case "gasp":
            case "breath_catch":
                faceController?.SetSpeechState(0.6f * intensity, "gasp");
                bodyController?.SetPoseIntent(PartnerPoseIntent.Brace, 0.15f);
                break;

            case "moan_soft":
                faceController?.SetSpeechState(0.3f * intensity, "soft");
                faceController?.SetFacePreset(PartnerFacePreset.Pleasure, 0.3f);
                voiceController?.Speak("mmm", "soft");
                break;

            case "moan_intense":
                faceController?.SetSpeechState(0.7f * intensity, "intense");
                faceController?.SetFacePreset(PartnerFacePreset.IntensePleasure, 0.2f);
                voiceController?.Speak("ahh", "intense");
                break;

            case "whimper":
                faceController?.SetSpeechState(0.2f * intensity, "soft");
                faceController?.SetFacePreset(PartnerFacePreset.Gentle, 0.3f);
                break;

            case "sigh":
                faceController?.SetSpeechState(0.15f * intensity, "soft");
                break;

            case "laugh_soft":
                faceController?.SetFacePreset(PartnerFacePreset.Teasing, 0.3f);
                faceController?.SetSpeechState(0.25f * intensity, "playful");
                break;

            case "shiver":
                // Brief body jitter — offset then return
                bodyController?.SetPoseIntent(PartnerPoseIntent.Brace, 0.1f);
                break;

            case "arch_back":
                bodyController?.SetPoseIntent(PartnerPoseIntent.LeanBack, 0.2f);
                faceController?.SetFacePreset(PartnerFacePreset.IntensePleasure, 0.2f);
                break;

            case "grip":
                // Hands reach toward user
                var gripTarget = trackingMerge != null ? trackingMerge.HeadPosition + Vector3.down * 0.3f : (Vector3?)null;
                bodyController?.SetHandTargets(gripTarget, gripTarget, 0.2f);
                break;

            case "writhe":
                bodyController?.SetPoseIntent(PartnerPoseIntent.Brace, 0.15f);
                faceController?.SetFacePreset(PartnerFacePreset.IntensePleasure, 0.15f);
                break;

            case "go_limp":
                bodyController?.SetPoseIntent(PartnerPoseIntent.Reclined, 0.5f);
                bodyController?.ClearTargets(0.5f);
                faceController?.SetFacePreset(PartnerFacePreset.Gentle, 0.8f);
                break;

            case "tense_up":
                bodyController?.SetPoseIntent(PartnerPoseIntent.Brace, 0.1f);
                faceController?.SetFacePreset(PartnerFacePreset.Focused, 0.15f);
                break;
        }

        runtimeState.activeGesture = reactionType;
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
