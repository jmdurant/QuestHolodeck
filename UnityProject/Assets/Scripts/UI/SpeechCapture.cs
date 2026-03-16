// SpeechCapture.cs
// SexKit Quest App
//
// Captures user speech via Quest microphone, converts to text using
// Android's on-device SpeechRecognizer, and sends upstream to iPhone.
//
// This gives the AI agent ears — it can hear the user speak and respond.
// Used for: conversation mode, persona roleplay, safe word detection,
// suggest() confirmation, and session intent.
//
// Audio never leaves the device — speech-to-text runs on-device via Android API.

using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class SpeechCapture : MonoBehaviour
{
    public static SpeechCapture Instance { get; private set; }

    [Header("Settings")]
    public bool listenContinuously = true;
    public float silenceTimeout = 2.0f;           // seconds of silence before finalizing
    public float restartDelay = 0.5f;             // seconds between listen sessions
    public string safeWord = "red";               // kill switch word — checked locally first
    public bool autoRequestMicrophonePermission = true;

    [Header("Status")]
    public bool isListening = false;
    public string lastTranscription = "";
    public float lastSpeechTime;

    // Events
    public event Action<string> OnSpeechRecognized;      // final transcription
    public event Action<string> OnPartialSpeech;          // interim results
    public event Action OnSafeWordDetected;               // immediate kill switch

    // Upstream
    public SexKitWebSocketClient wsClient;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _speechRecognizer;
    private AndroidJavaObject _recognizerIntent;
    private bool _isInitialized = false;
#endif

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        wsClient ??= SexKitWebSocketClient.Instance;
        InitializeAndStartIfPermitted();
    }

    // MARK: - Public API

    public void StartListening()
    {
        if (isListening) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (_isInitialized && _speechRecognizer != null)
        {
            _speechRecognizer.Call("startListening", _recognizerIntent);
            isListening = true;
            Debug.Log("[SpeechCapture] Listening...");
        }
#else
        // Editor stub — simulate with keyboard input
        isListening = true;
        Debug.Log("[SpeechCapture] Listening (editor stub — type in console)");
#endif
    }

    public void StopListening()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_speechRecognizer != null)
            _speechRecognizer.Call("stopListening");
#endif
        isListening = false;
    }

    // MARK: - Initialize Android SpeechRecognizer

    private void InitializeSpeechRecognizer()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            // Check if speech recognition is available
            var srClass = new AndroidJavaClass("android.speech.SpeechRecognizer");
            bool available = srClass.CallStatic<bool>("isRecognitionAvailable", activity);
            if (!available)
            {
                Debug.LogWarning("[SpeechCapture] Speech recognition not available on this device");
                return;
            }

            // Create recognizer
            _speechRecognizer = srClass.CallStatic<AndroidJavaObject>("createSpeechRecognizer", activity);

            // Create intent
            var intentClass = new AndroidJavaClass("android.speech.RecognizerIntent");
            _recognizerIntent = new AndroidJavaObject("android.content.Intent",
                intentClass.GetStatic<string>("ACTION_RECOGNIZE_SPEECH"));
            _recognizerIntent.Call<AndroidJavaObject>("putExtra",
                intentClass.GetStatic<string>("EXTRA_LANGUAGE_MODEL"),
                intentClass.GetStatic<string>("LANGUAGE_MODEL_FREE_FORM"));
            _recognizerIntent.Call<AndroidJavaObject>("putExtra",
                intentClass.GetStatic<string>("EXTRA_PARTIAL_RESULTS"), true);
            _recognizerIntent.Call<AndroidJavaObject>("putExtra",
                intentClass.GetStatic<string>("EXTRA_MAX_RESULTS"), 1);

            // Set listener
            _speechRecognizer.Call("setRecognitionListener", new SpeechListener(this));
            _isInitialized = true;

            Debug.Log("[SpeechCapture] Android SpeechRecognizer initialized");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SpeechCapture] Init failed: {e.Message}");
        }
#endif
    }

    private void InitializeAndStartIfPermitted()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            if (autoRequestMicrophonePermission)
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ =>
                {
                    Debug.Log("[SpeechCapture] Microphone permission granted");
                    InitializeSpeechRecognizer();
                    if (listenContinuously)
                    {
                        StartListening();
                    }
                };
                callbacks.PermissionDenied += _ =>
                {
                    Debug.LogWarning("[SpeechCapture] Microphone permission denied");
                };
                callbacks.PermissionDeniedAndDontAskAgain += _ =>
                {
                    Debug.LogWarning("[SpeechCapture] Microphone permission denied permanently");
                };
                Permission.RequestUserPermission(Permission.Microphone, callbacks);
            }
            else
            {
                Debug.LogWarning("[SpeechCapture] Microphone permission missing");
            }

            return;
        }
#endif

        InitializeSpeechRecognizer();

        if (listenContinuously)
        {
            StartListening();
        }
    }

    // MARK: - Handle Results

    internal void HandleResult(string text, bool isFinal)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        lastSpeechTime = Time.time;

        // Check safe word FIRST — before anything else
        if (ContainsSafeWord(text))
        {
            Debug.Log($"[SpeechCapture] SAFE WORD DETECTED: \"{text}\"");
            OnSafeWordDetected?.Invoke();
            StopListening();
            return;
        }

        if (isFinal)
        {
            lastTranscription = text;
            Debug.Log($"[SpeechCapture] Final: \"{text}\"");

            OnSpeechRecognized?.Invoke(text);
            SendUpstream(text, true);

            // Restart listening if continuous mode
            if (listenContinuously)
                Invoke(nameof(StartListening), restartDelay);
        }
        else
        {
            OnPartialSpeech?.Invoke(text);
            // Don't send partials upstream — too noisy
        }
    }

    internal void HandleError(int errorCode)
    {
        isListening = false;

        // Common errors: 6 = no speech detected, 7 = no match
        if (errorCode == 6 || errorCode == 7)
        {
            // Silence — restart if continuous
            if (listenContinuously)
                Invoke(nameof(StartListening), restartDelay);
            return;
        }

        Debug.LogWarning($"[SpeechCapture] Error code: {errorCode}");
        if (listenContinuously)
            Invoke(nameof(StartListening), restartDelay * 2f);
    }

    // MARK: - Safe Word Check

    private bool ContainsSafeWord(string text)
    {
        if (string.IsNullOrWhiteSpace(safeWord)) return false;
        return text.ToLowerInvariant().Contains(safeWord.ToLowerInvariant());
    }

    /// Update safe word from AgentPreferences (called when MCP server starts)
    public void SetSafeWord(string word)
    {
        safeWord = word;
    }

    // MARK: - Send Upstream to iPhone

    private void SendUpstream(string text, bool isFinal)
    {
        if (wsClient == null || !wsClient.isConnected) return;

        var speech = new UserSpeechFrame
        {
            type = "user_speech",
            text = text,
            isFinal = isFinal,
            timestamp = Time.realtimeSinceStartupAsDouble
        };

        string json = JsonUtility.ToJson(speech);
        wsClient.SendCommand(json);
    }

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _speechRecognizer?.Call("destroy");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // Android RecognitionListener callback proxy
    private class SpeechListener : AndroidJavaProxy
    {
        private readonly SpeechCapture _capture;

        public SpeechListener(SpeechCapture capture)
            : base("android.speech.RecognitionListener")
        {
            _capture = capture;
        }

        void onResults(AndroidJavaObject results)
        {
            var matches = results.Call<AndroidJavaObject>("getStringArrayList",
                new AndroidJavaClass("android.speech.SpeechRecognizer")
                    .GetStatic<string>("RESULTS_RECOGNITION"));
            if (matches != null && matches.Call<int>("size") > 0)
            {
                string text = matches.Call<string>("get", 0);
                UnityMainThreadDispatcher.Enqueue(() => _capture.HandleResult(text, true));
            }
        }

        void onPartialResults(AndroidJavaObject partialResults)
        {
            var matches = partialResults.Call<AndroidJavaObject>("getStringArrayList",
                new AndroidJavaClass("android.speech.SpeechRecognizer")
                    .GetStatic<string>("RESULTS_RECOGNITION"));
            if (matches != null && matches.Call<int>("size") > 0)
            {
                string text = matches.Call<string>("get", 0);
                UnityMainThreadDispatcher.Enqueue(() => _capture.HandleResult(text, false));
            }
        }

        void onError(int error)
        {
            UnityMainThreadDispatcher.Enqueue(() => _capture.HandleError(error));
        }

        // Required interface methods (no-op)
        void onReadyForSpeech(AndroidJavaObject @params) { }
        void onBeginningOfSpeech() { }
        void onRmsChanged(float rmsdB) { }
        void onBufferReceived(AndroidJavaObject buffer) { }
        void onEndOfSpeech()
        {
            UnityMainThreadDispatcher.Enqueue(() => _capture.isListening = false);
        }
        void onEvent(int eventType, AndroidJavaObject @params) { }
    }
#endif
}

// Data sent upstream from Quest to iPhone when user speaks
[System.Serializable]
public class UserSpeechFrame
{
    public string type;       // always "user_speech"
    public string text;       // transcribed speech
    public bool isFinal;      // true = final result, false = partial
    public double timestamp;
}
