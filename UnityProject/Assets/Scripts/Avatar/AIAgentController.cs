// AIAgentController.cs
// SexKit Quest App
//
// Controls Body B (the AI agent / virtual partner)
// Receives intelligence from: rule-based, Claude API, or game scripting
// Outputs 16 joint positions per frame to drive the avatar

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class AIAgentController : MonoBehaviour
{
    [Header("Intelligence Source")]
    public AgentMode mode = AgentMode.RuleBased;

    [Header("Rule-Based Settings")]
    public float rhythmResponseDelay = 0.1f;
    public float proximityDistance = 0.4f;

    [Header("Cloud AI Settings")]
    public string aiEndpoint = "";
    public string aiApiKey = "";
    public float aiUpdateInterval = 0.5f;  // AI decisions at 2Hz, interpolated to 30fps

    [Header("References")]
    public QuestTrackingMerge userTracking;
    public SexKitAvatarDriver avatarDriver;

    public enum AgentMode
    {
        RuleBased,      // Position complement + rhythm matching
        CloudAI,        // Claude API or custom AI endpoint
        GameScript,     // Scripted scenario with triggers
        Hybrid          // Rules for body, AI for decisions
    }

    // Agent state
    public Vector3 AgentPosition { get; private set; }
    public string AgentActivity { get; private set; } = "idle";
    private Dictionary<string, Vector3> _agentJoints = new();
    private float _rhythmPhase = 0;
    private float _lastAIUpdate = 0;
    private string _lastAIDirective = "";

    // Position complements (matches SexKit's PartnerInference)
    private static readonly Dictionary<string, PositionComplement> Complements = new()
    {
        {"Missionary", new PositionComplement(new Vector3(0, -0.25f, 0), "faceUp", true, 0.2f)},
        {"Cowgirl", new PositionComplement(new Vector3(0, -0.3f, 0), "faceUp", false, 0.15f)},
        {"Doggy Style", new PositionComplement(new Vector3(0, -0.15f, -0.4f), "faceDown", true, 0.5f)},
        {"Spooning", new PositionComplement(new Vector3(0, 0, -0.3f), "onSide", true, 0.3f)},
        {"Standing", new PositionComplement(new Vector3(0, 0, -0.5f), "upright", true, 0.6f)},
        {"Seated", new PositionComplement(new Vector3(0, 0.2f, -0.15f), "upright", true, 0.7f)},
        {"Prone", new PositionComplement(new Vector3(0, 0.25f, 0), "faceDown", true, 0.6f)},
        {"Lotus", new PositionComplement(new Vector3(0, 0.1f, -0.15f), "upright", true, 0.6f)},
        {"Oral", new PositionComplement(new Vector3(0, -0.3f, -0.4f), "upright", false, 0.4f)},
        {"69", new PositionComplement(new Vector3(0, 0.05f, 0), "faceDown", false, 0.3f)},
    };

    struct PositionComplement
    {
        public Vector3 offset;
        public string orientation;
        public bool mirrorRhythm;
        public float rhythmAmplitude;

        public PositionComplement(Vector3 o, string or_, bool mr, float ra)
        { offset = o; orientation = or_; mirrorRhythm = mr; rhythmAmplitude = ra; }
    }

    void Update()
    {
        var frame = SexKitWebSocketClient.Instance?.latestFrame;
        if (frame == null) return;

        switch (mode)
        {
            case AgentMode.RuleBased:
                UpdateRuleBased(frame);
                break;
            case AgentMode.CloudAI:
                UpdateCloudAI(frame);
                break;
            case AgentMode.GameScript:
                UpdateGameScript(frame);
                break;
            case AgentMode.Hybrid:
                UpdateHybrid(frame);
                break;
        }

        // Eye contact + gaze response (all modes)
        UpdateGazeBehavior();
    }

    // MARK: - Rule-Based (matches SexKit PartnerInference)

    void UpdateRuleBased(LiveFrame frame)
    {
        if (string.IsNullOrEmpty(frame.detectedPosition)) return;
        if (!Complements.TryGetValue(frame.detectedPosition, out var complement)) return;

        // Get user's hip position as reference
        Vector3 userHip = frame.skeletonA?.GetJoint("hip") ?? Vector3.zero;
        if (userHip == Vector3.zero) return;

        // Rhythm oscillation
        _rhythmPhase += Time.deltaTime * (float)frame.rhythmHz * Mathf.PI * 2f;
        float rhythmOffset = complement.mirrorRhythm
            ? Mathf.Sin(_rhythmPhase) * complement.rhythmAmplitude * 0.05f
            : Mathf.Sin(_rhythmPhase + Mathf.PI) * complement.rhythmAmplitude * 0.03f;

        // Place agent relative to user
        AgentPosition = userHip + complement.offset + new Vector3(0, rhythmOffset, 0);
        AgentActivity = frame.detectedPosition;

        // Build skeleton from orientation
        BuildAgentSkeleton(AgentPosition, complement.orientation, rhythmOffset);
    }

    // MARK: - Cloud AI (Claude API or custom)

    void UpdateCloudAI(LiveFrame frame)
    {
        // AI makes decisions at lower frequency, body interpolates
        if (Time.time - _lastAIUpdate > aiUpdateInterval)
        {
            _lastAIUpdate = Time.time;
            _ = RequestAIDecision(frame);
        }

        // Between AI updates, use rule-based for smooth movement
        UpdateRuleBased(frame);
    }

    async Task RequestAIDecision(LiveFrame frame)
    {
        if (string.IsNullOrEmpty(aiEndpoint)) return;

        try
        {
            // Send current state to AI
            string json = JsonUtility.ToJson(frame);
            // POST to AI endpoint, get back decision
            // Decision could include: position change, verbal response, gesture
            // For now, log the intent
            Debug.Log($"[AI Agent] Requesting decision for position: {frame.detectedPosition}, HR: {frame.heartRate}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AI Agent] Decision request failed: {e.Message}");
        }
    }

    // MARK: - Game Script

    void UpdateGameScript(LiveFrame frame)
    {
        // Scripted scenarios with triggers
        // e.g., "When user lies down, agent approaches from the right"
        // e.g., "When HR > 130 for 30s, agent changes position"
        // Implement scenario-specific logic here
        UpdateRuleBased(frame);  // fallback to rule-based
    }

    // MARK: - Hybrid (rules for body, AI for decisions)

    void UpdateHybrid(LiveFrame frame)
    {
        // AI decides WHAT to do (position, approach, retreat)
        // Rules handle HOW to move (complement table, rhythm matching)
        UpdateCloudAI(frame);
    }

    // MARK: - Eye Contact & Gaze Response

    [Header("Eye Contact")]
    public bool enableEyeContact = true;
    public float gazeResponseSpeed = 3f;
    public float eyeContactBreakInterval = 8f;  // natural break every ~8s

    private Vector3 _agentLookTarget;
    private float _lastEyeContactBreak = 0;
    private bool _isHoldingEyeContact = true;
    private GazeBehavior _currentGazeBehavior = GazeBehavior.SoftContact;

    enum GazeBehavior
    {
        SoftContact,      // gentle gaze, warmup/resolution
        IntenseContact,   // locked in, building/plateau
        BodyGlance,       // looks at user's body briefly
        LookAway,         // natural break, averts gaze
        EyesClosed,       // during edge pull-back
        FollowUser,       // tracks where user is looking
    }

    void UpdateGazeBehavior()
    {
        if (!enableEyeContact || userTracking == null) return;

        var frame = SexKitWebSocketClient.Instance?.latestFrame;
        string phase = frame?.pacingPhase ?? "Warmup";

        // Set gaze behavior based on pacing phase
        _currentGazeBehavior = phase switch
        {
            "Warmup" => GazeBehavior.SoftContact,
            "Building" => GazeBehavior.IntenseContact,
            "Plateau" => Time.time % 12f < 8f ? GazeBehavior.IntenseContact : GazeBehavior.BodyGlance,
            "Edge" => GazeBehavior.EyesClosed,
            "Release" => GazeBehavior.IntenseContact,
            "Resolution" => GazeBehavior.SoftContact,
            _ => GazeBehavior.SoftContact,
        };

        // Calculate look target based on behavior
        Vector3 userHead = userTracking.HeadPosition;
        Vector3 userBody = userHead + Vector3.down * 0.4f;  // approximate chest
        Vector3 agentHead = GetAgentJoint("head");
        if (agentHead == Vector3.zero) return;

        switch (_currentGazeBehavior)
        {
            case GazeBehavior.SoftContact:
                // Gentle gaze at user's face, slight natural drift
                float drift = Mathf.Sin(Time.time * 0.3f) * 0.05f;
                _agentLookTarget = userHead + new Vector3(drift, drift * 0.5f, 0);
                break;

            case GazeBehavior.IntenseContact:
                // Locked on user's eyes
                if (userTracking.IsUserLooking)
                {
                    // User is looking at us — lock eye contact
                    _agentLookTarget = userHead;
                }
                else
                {
                    // User looking elsewhere — follow their gaze direction
                    _agentLookTarget = userHead + userTracking.GazeDirection * 0.3f;
                }
                break;

            case GazeBehavior.BodyGlance:
                // Brief look at user's body then back to face
                _agentLookTarget = Vector3.Lerp(userBody, userHead, Mathf.PingPong(Time.time * 0.5f, 1f));
                break;

            case GazeBehavior.LookAway:
                // Avert gaze naturally — look to the side
                Vector3 side = Vector3.Cross(userHead - agentHead, Vector3.up).normalized;
                _agentLookTarget = agentHead + side * 0.5f + Vector3.down * 0.2f;
                break;

            case GazeBehavior.EyesClosed:
                // During edge — "close eyes" by looking slightly down
                _agentLookTarget = agentHead + Vector3.down * 0.3f + (userHead - agentHead).normalized * 0.1f;
                break;

            case GazeBehavior.FollowUser:
                // Track where the user is looking
                _agentLookTarget = userHead + userTracking.GazeDirection * 2f;
                break;
        }

        // Apply look direction to agent's head joint
        ApplyHeadLookAt(agentHead, _agentLookTarget);

        // Natural eye contact breaks (every ~8s, look away briefly then back)
        if (_currentGazeBehavior == GazeBehavior.IntenseContact ||
            _currentGazeBehavior == GazeBehavior.SoftContact)
        {
            if (Time.time - _lastEyeContactBreak > eyeContactBreakInterval)
            {
                _lastEyeContactBreak = Time.time;
                // Brief look-away for 1-2 seconds (natural behavior)
                StartCoroutine(BriefLookAway());
            }
        }
    }

    System.Collections.IEnumerator BriefLookAway()
    {
        var saved = _currentGazeBehavior;
        _currentGazeBehavior = GazeBehavior.LookAway;
        yield return new WaitForSeconds(UnityEngine.Random.Range(0.8f, 1.5f));
        _currentGazeBehavior = saved;
    }

    void ApplyHeadLookAt(Vector3 headPos, Vector3 lookTarget)
    {
        // Calculate head rotation to look at target
        Vector3 dir = (lookTarget - headPos).normalized;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);

        // Store for the avatar driver to apply to the head bone
        _agentJoints["headRotation_fwd"] = dir;  // forward direction for head
    }

    // MARK: - Build Skeleton

    void BuildAgentSkeleton(Vector3 center, string orientation, float rhythmOffset)
    {
        float shoulderW = 0.20f;
        float torsoLen = 0.25f;
        float armLen = 0.30f;
        float neckLen = 0.08f;
        float legLen = 0.45f;
        float shinLen = 0.40f;
        float hipW = 0.18f;

        _agentJoints["hip"] = center;

        switch (orientation)
        {
            case "faceUp":
            case "faceDown":
                _agentJoints["spine"] = center + new Vector3(0, 0, -torsoLen / 2);
                _agentJoints["neck"] = center + new Vector3(0, 0, -torsoLen);
                _agentJoints["head"] = center + new Vector3(0, 0, -torsoLen - neckLen);
                _agentJoints["leftShoulder"] = _agentJoints["neck"] + new Vector3(-shoulderW, 0, 0);
                _agentJoints["rightShoulder"] = _agentJoints["neck"] + new Vector3(shoulderW, 0, 0);
                _agentJoints["leftElbow"] = _agentJoints["leftShoulder"] + new Vector3(-armLen / 2, 0, 0);
                _agentJoints["rightElbow"] = _agentJoints["rightShoulder"] + new Vector3(armLen / 2, 0, 0);
                _agentJoints["leftWrist"] = _agentJoints["leftElbow"] + new Vector3(-armLen / 2, 0, 0);
                _agentJoints["rightWrist"] = _agentJoints["rightElbow"] + new Vector3(armLen / 2, 0, 0);
                _agentJoints["leftHip"] = center + new Vector3(-hipW, 0, 0);
                _agentJoints["rightHip"] = center + new Vector3(hipW, 0, 0);
                _agentJoints["leftKnee"] = _agentJoints["leftHip"] + new Vector3(0, 0, legLen);
                _agentJoints["rightKnee"] = _agentJoints["rightHip"] + new Vector3(0, 0, legLen);
                _agentJoints["leftAnkle"] = _agentJoints["leftKnee"] + new Vector3(0, 0, shinLen);
                _agentJoints["rightAnkle"] = _agentJoints["rightKnee"] + new Vector3(0, 0, shinLen);
                break;

            case "upright":
                _agentJoints["spine"] = center + new Vector3(0, torsoLen / 2, 0);
                _agentJoints["neck"] = center + new Vector3(0, torsoLen, 0);
                _agentJoints["head"] = center + new Vector3(0, torsoLen + neckLen, 0);
                _agentJoints["leftShoulder"] = _agentJoints["neck"] + new Vector3(-shoulderW, 0, 0);
                _agentJoints["rightShoulder"] = _agentJoints["neck"] + new Vector3(shoulderW, 0, 0);
                _agentJoints["leftElbow"] = _agentJoints["leftShoulder"] + new Vector3(0, -armLen / 2, 0);
                _agentJoints["rightElbow"] = _agentJoints["rightShoulder"] + new Vector3(0, -armLen / 2, 0);
                _agentJoints["leftWrist"] = _agentJoints["leftElbow"] + new Vector3(0, -armLen / 2, 0);
                _agentJoints["rightWrist"] = _agentJoints["rightElbow"] + new Vector3(0, -armLen / 2, 0);
                _agentJoints["leftHip"] = center + new Vector3(-hipW, 0, 0);
                _agentJoints["rightHip"] = center + new Vector3(hipW, 0, 0);
                _agentJoints["leftKnee"] = _agentJoints["leftHip"] + new Vector3(0, -legLen, 0);
                _agentJoints["rightKnee"] = _agentJoints["rightHip"] + new Vector3(0, -legLen, 0);
                _agentJoints["leftAnkle"] = _agentJoints["leftKnee"] + new Vector3(0, -shinLen, 0);
                _agentJoints["rightAnkle"] = _agentJoints["rightKnee"] + new Vector3(0, -shinLen, 0);
                break;

            case "onSide":
                _agentJoints["spine"] = center + new Vector3(0, 0, -torsoLen / 2);
                _agentJoints["neck"] = center + new Vector3(0, 0, -torsoLen);
                _agentJoints["head"] = center + new Vector3(0, 0, -torsoLen - neckLen);
                _agentJoints["leftShoulder"] = _agentJoints["neck"] + new Vector3(0, shoulderW, 0);
                _agentJoints["rightShoulder"] = _agentJoints["neck"] + new Vector3(0, -shoulderW, 0);
                _agentJoints["leftElbow"] = _agentJoints["leftShoulder"] + new Vector3(0, armLen / 2, 0);
                _agentJoints["rightElbow"] = _agentJoints["rightShoulder"] + new Vector3(0, -armLen / 2, 0);
                _agentJoints["leftWrist"] = _agentJoints["leftElbow"] + new Vector3(0, armLen / 2, 0);
                _agentJoints["rightWrist"] = _agentJoints["rightElbow"] + new Vector3(0, -armLen / 2, 0);
                _agentJoints["leftHip"] = center + new Vector3(0, hipW, 0);
                _agentJoints["rightHip"] = center + new Vector3(0, -hipW, 0);
                _agentJoints["leftKnee"] = _agentJoints["leftHip"] + new Vector3(0, 0, legLen);
                _agentJoints["rightKnee"] = _agentJoints["rightHip"] + new Vector3(0, 0, legLen);
                _agentJoints["leftAnkle"] = _agentJoints["leftKnee"] + new Vector3(0, 0, shinLen);
                _agentJoints["rightAnkle"] = _agentJoints["rightKnee"] + new Vector3(0, 0, shinLen);
                break;
        }
    }

    /// Expose agent joints for the avatar driver to render
    public Vector3 GetAgentJoint(string name)
    {
        return _agentJoints.TryGetValue(name, out var pos) ? pos : Vector3.zero;
    }
}
