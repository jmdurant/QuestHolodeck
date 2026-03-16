// SuggestConfirmationHandler.cs
// SexKit Quest App
//
// Handles the user confirmation flow when the agent calls suggest().
// Body B pauses and waits. User responds via voice, nod, shake, or timeout.
// Response sent upstream to iPhone → MCP agent.
//
// Detection methods:
//   1. Voice: "yes"/"yeah"/"ok"/"sure" or "no"/"not yet"/"wait" via SpeechCapture
//   2. Nod: repeated downward head pitch (Quest head tracking at 90fps)
//   3. Head shake: repeated left-right yaw rotation
//   4. Timeout: no response = declined
//

using System;
using UnityEngine;

public class SuggestConfirmationHandler : MonoBehaviour
{
    public static SuggestConfirmationHandler Instance { get; private set; }

    [Header("References")]
    public QuestTrackingMerge trackingMerge;
    public SpeechCapture speechCapture;
    public SexKitWebSocketClient wsClient;

    [Header("Nod/Shake Detection")]
    public float nodThreshold = 12f;         // degrees of pitch change to count as nod
    public float shakeThreshold = 15f;        // degrees of yaw change to count as shake
    public int requiredMotions = 2;           // need 2+ nods or shakes to confirm
    public float motionWindow = 2.0f;         // seconds to accumulate motions

    [Header("Timeout")]
    public float defaultTimeout = 15f;

    [Header("Status")]
    public bool isWaitingForResponse = false;
    public string pendingAction = "";
    public float timeRemaining = 0;

    // Events
    public event Action<bool, string> OnConfirmationResult;  // (accepted, method)

    // Nod/shake tracking
    private float _lastPitch;
    private float _lastYaw;
    private int _nodCount;
    private int _shakeCount;
    private float _motionStartTime;
    private bool _pitchGoingDown;
    private bool _yawGoingRight;

    // Voice tracking
    private bool _listeningForConfirmation;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        trackingMerge ??= FindFirstObjectByType<QuestTrackingMerge>();
        speechCapture ??= SpeechCapture.Instance;
        wsClient ??= SexKitWebSocketClient.Instance;
    }

    // MARK: - Start/Cancel Confirmation

    /// Called by PartnerDirector when a suggest ControlFrame arrives
    public void StartConfirmation(string action, string verbalPrompt, float timeout)
    {
        pendingAction = action;
        timeRemaining = timeout > 0 ? timeout : defaultTimeout;
        isWaitingForResponse = true;

        // Reset tracking
        _nodCount = 0;
        _shakeCount = 0;
        _motionStartTime = Time.time;
        if (trackingMerge != null)
        {
            var euler = trackingMerge.HeadRotation.eulerAngles;
            _lastPitch = euler.x;
            _lastYaw = euler.y;
        }

        // Listen for voice confirmation
        _listeningForConfirmation = true;
        if (speechCapture != null)
        {
            speechCapture.OnSpeechRecognized += HandleSpeechDuringConfirmation;
        }

        Debug.Log($"[Suggest] Waiting for confirmation: \"{action}\" (timeout: {timeout}s)");
    }

    public void CancelConfirmation()
    {
        Cleanup();
        Debug.Log("[Suggest] Confirmation cancelled");
    }

    void Update()
    {
        if (!isWaitingForResponse) return;

        // Countdown
        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0)
        {
            Resolve(false, "timeout");
            return;
        }

        // Check nod/shake from head tracking
        if (trackingMerge != null)
        {
            DetectHeadMotion();
        }
    }

    // MARK: - Head Motion Detection

    private void DetectHeadMotion()
    {
        var euler = trackingMerge.HeadRotation.eulerAngles;
        float pitch = NormalizeAngle(euler.x);  // nod = pitch change
        float yaw = NormalizeAngle(euler.y);    // shake = yaw change

        float pitchDelta = pitch - _lastPitch;
        float yawDelta = yaw - _lastYaw;

        // Reset window if too much time passed
        if (Time.time - _motionStartTime > motionWindow)
        {
            _nodCount = 0;
            _shakeCount = 0;
            _motionStartTime = Time.time;
        }

        // Nod detection: pitch goes down then up (or up then down)
        if (Mathf.Abs(pitchDelta) > nodThreshold)
        {
            bool goingDown = pitchDelta > 0;
            if (goingDown != _pitchGoingDown)
            {
                _nodCount++;
                _pitchGoingDown = goingDown;

                if (_nodCount >= requiredMotions)
                {
                    Resolve(true, "nod");
                    return;
                }
            }
        }

        // Shake detection: yaw goes left then right
        if (Mathf.Abs(yawDelta) > shakeThreshold)
        {
            bool goingRight = yawDelta > 0;
            if (goingRight != _yawGoingRight)
            {
                _shakeCount++;
                _yawGoingRight = goingRight;

                if (_shakeCount >= requiredMotions)
                {
                    Resolve(false, "head_shake");
                    return;
                }
            }
        }

        _lastPitch = pitch;
        _lastYaw = yaw;
    }

    // MARK: - Voice Confirmation

    private void HandleSpeechDuringConfirmation(string text)
    {
        if (!isWaitingForResponse) return;

        var lower = text.ToLowerInvariant().Trim();

        // Affirmative
        if (lower is "yes" or "yeah" or "yep" or "ok" or "okay" or "sure"
            or "do it" or "go ahead" or "yes please" or "mmhm" or "uh huh")
        {
            Resolve(true, "voice");
            return;
        }

        // Negative
        if (lower is "no" or "nah" or "not yet" or "wait" or "stop"
            or "no thanks" or "hold on" or "not now")
        {
            Resolve(false, "voice");
            return;
        }

        // Didn't match a clear yes/no — keep listening
        Debug.Log($"[Suggest] Speech not recognized as yes/no: \"{text}\"");
    }

    // MARK: - Resolve

    private void Resolve(bool accepted, string method)
    {
        Debug.Log($"[Suggest] Resolved: {(accepted ? "ACCEPTED" : "DECLINED")} via {method} for \"{pendingAction}\"");

        // Send upstream to iPhone
        SendResponseUpstream(accepted, method);

        // Fire event for PartnerDirector
        OnConfirmationResult?.Invoke(accepted, method);

        Cleanup();
    }

    private void Cleanup()
    {
        isWaitingForResponse = false;
        _listeningForConfirmation = false;
        pendingAction = "";
        timeRemaining = 0;
        _nodCount = 0;
        _shakeCount = 0;

        if (speechCapture != null)
        {
            speechCapture.OnSpeechRecognized -= HandleSpeechDuringConfirmation;
        }
    }

    // MARK: - Send to iPhone

    private void SendResponseUpstream(bool accepted, string method)
    {
        if (wsClient == null || !wsClient.isConnected) return;

        var response = new SuggestResponse
        {
            type = "suggest_response",
            accepted = accepted,
            method = method,
            action = pendingAction,
            timestamp = Time.realtimeSinceStartupAsDouble
        };

        string json = JsonUtility.ToJson(response);
        wsClient.SendCommand(json);
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    void OnDestroy()
    {
        if (speechCapture != null)
        {
            speechCapture.OnSpeechRecognized -= HandleSpeechDuringConfirmation;
        }
    }
}

[System.Serializable]
public class SuggestResponse
{
    public string type;       // always "suggest_response"
    public bool accepted;
    public string method;     // "voice", "nod", "head_shake", "timeout"
    public string action;     // the action that was proposed
    public double timestamp;
}
