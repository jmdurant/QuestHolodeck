using UnityEngine;
using System.Collections.Generic;
using System.Text;

public class PartnerVoiceController : MonoBehaviour, IPartnerVoiceController
{
    [Header("References")]
    public AudioSource audioSource;
    public PartnerFaceController faceController;
    public Transform followTarget;
    public Transform followSearchRoot;
    public string followBoneName = "DEF-spine.006";

    [Header("Tuning")]
    public float wordsPerSecond = 2.6f;
    public float minimumSpeechDuration = 1f;
    public bool useEstimatedVisemes = true;
    public float visemeWeight = 0.85f;

    [Header("TTS")]
    public bool useTTS = true;
    public float ttsPitch = 1.2f;      // slightly higher for female voice
    public float ttsRate = 0.9f;       // slightly slower for clarity

    [Header("Runtime")]
    public string lastSpeechText;
    public string lastSpeechStyle;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _tts;
    private bool _ttsReady = false;
#endif

    private float _speechEndTime = -1f;
    private float _speechStartTime = -1f;
    private float _speechDuration;
    private readonly List<VisemeCue> _visemeCues = new();

    public bool IsSpeaking => Time.time < _speechEndTime;

    void Start()
    {
        InitializeTTS();
    }

    public void Speak(string text, string style)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lastSpeechText = text;
        lastSpeechStyle = style;

        var estimatedDuration = Mathf.Max(minimumSpeechDuration, text.Split(' ').Length / Mathf.Max(0.1f, wordsPerSecond));
        _speechDuration = estimatedDuration;
        _speechStartTime = Time.time;
        _speechEndTime = Time.time + estimatedDuration;
        BuildEstimatedVisemes(text, estimatedDuration);

        // Play pre-assigned audio clip if available (pre-generated reactions, moans, etc.)
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Stop();
            audioSource.Play();
        }

        // Android TTS for dynamic speech
        SpeakTTS(text, style);

        Debug.Log($"[PartnerVoice] \"{text}\" style={style}");
    }

    // MARK: - Android TTS

    private void InitializeTTS()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!useTTS) return;

        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, new TTSInitListener(this));
            Debug.Log("[PartnerVoice] Android TTS initializing...");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PartnerVoice] TTS init failed: {e.Message}");
        }
#endif
    }

    private void SpeakTTS(string text, string style)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!useTTS || !_ttsReady || _tts == null) return;

        // Adjust pitch/rate based on emotion style
        float pitch = ttsPitch;
        float rate = ttsRate;

        if (!string.IsNullOrWhiteSpace(style))
        {
            var lower = style.ToLowerInvariant();
            if (lower.Contains("breathless") || lower.Contains("urgent"))
            {
                rate = Mathf.Min(rate + 0.2f, 1.5f);
                pitch = Mathf.Min(pitch + 0.1f, 1.5f);
            }
            else if (lower.Contains("gentle") || lower.Contains("slow"))
            {
                rate = Mathf.Max(rate - 0.2f, 0.5f);
            }
            else if (lower.Contains("playful") || lower.Contains("teasing"))
            {
                pitch = Mathf.Min(pitch + 0.15f, 1.6f);
            }
            else if (lower.Contains("commanding") || lower.Contains("bold"))
            {
                pitch = Mathf.Max(pitch - 0.1f, 0.8f);
                rate = Mathf.Min(rate + 0.1f, 1.3f);
            }
        }

        _tts.Call<int>("setPitch", pitch);
        _tts.Call<int>("setSpeechRate", rate);

        // QUEUE_FLUSH = 0 — interrupts any current speech
        // Use HashMap for params (required by API)
        _tts.Call<int>("speak", text, 0, null, System.Guid.NewGuid().ToString());

        Debug.Log($"[PartnerVoice] TTS speaking: \"{text}\" pitch={pitch} rate={rate}");
#endif
    }

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_tts != null)
        {
            _tts.Call("stop");
            _tts.Call("shutdown");
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private class TTSInitListener : AndroidJavaProxy
    {
        private readonly PartnerVoiceController _controller;

        public TTSInitListener(PartnerVoiceController controller)
            : base("android.speech.tts.TextToSpeech$OnInitListener")
        {
            _controller = controller;
        }

        void onInit(int status)
        {
            if (status == 0) // TextToSpeech.SUCCESS
            {
                _controller._ttsReady = true;

                // Set to English female voice if available
                var locale = new AndroidJavaObject("java.util.Locale", "en", "US");
                _controller._tts.Call<int>("setLanguage", locale);

                Debug.Log("[PartnerVoice] Android TTS ready");
            }
            else
            {
                Debug.LogWarning($"[PartnerVoice] TTS init failed with status: {status}");
            }
        }
    }
#endif

    public void Stop()
    {
        _speechEndTime = -1f;
        _speechStartTime = -1f;
        _speechDuration = 0f;
        _visemeCues.Clear();
        audioSource?.Stop();
        faceController?.SetSpeechState(0f, lastSpeechStyle);
        faceController?.ClearVisemes();

#if UNITY_ANDROID && !UNITY_EDITOR
        _tts?.Call<int>("stop");
#endif
    }

    public void Tick(float deltaTime)
    {
        ResolveFollowTarget();

        if (audioSource != null && followTarget != null)
        {
            audioSource.transform.position = followTarget.position;
        }

        if (faceController == null)
        {
            return;
        }

        if (IsSpeaking)
        {
            var mouthOpen = 0.35f + Mathf.PingPong(Time.time * 3.5f, 0.45f);
            faceController.SetSpeechState(mouthOpen, lastSpeechStyle);
            ApplyEstimatedViseme();
        }
        else
        {
            faceController.SetSpeechState(0f, lastSpeechStyle);
            faceController.ClearVisemes();
        }
    }

    private void ApplyEstimatedViseme()
    {
        if (!useEstimatedVisemes || _visemeCues.Count == 0 || _speechDuration <= 0f)
        {
            return;
        }

        var elapsed = Mathf.Clamp(Time.time - _speechStartTime, 0f, _speechDuration);
        var normalizedTime = Mathf.Clamp01(elapsed / _speechDuration);

        for (var i = 0; i < _visemeCues.Count; i++)
        {
            var cue = _visemeCues[i];
            if (normalizedTime >= cue.startNormalized && normalizedTime <= cue.endNormalized)
            {
                faceController.SetViseme(cue.viseme, visemeWeight);
                return;
            }
        }

        faceController.ClearVisemes();
    }

    private void ResolveFollowTarget()
    {
        if (followTarget != null || followSearchRoot == null || string.IsNullOrWhiteSpace(followBoneName))
        {
            return;
        }

        var transforms = followSearchRoot.GetComponentsInChildren<Transform>(true);
        foreach (var current in transforms)
        {
            if (current.name == followBoneName)
            {
                followTarget = current;
                return;
            }
        }
    }

    private void BuildEstimatedVisemes(string text, float duration)
    {
        _visemeCues.Clear();
        if (!useEstimatedVisemes || string.IsNullOrWhiteSpace(text) || duration <= 0f)
        {
            return;
        }

        var visemes = ExtractVisemeSequence(text);
        if (visemes.Count == 0)
        {
            return;
        }

        var segmentLength = 1f / visemes.Count;
        for (var i = 0; i < visemes.Count; i++)
        {
            var start = i * segmentLength;
            var end = (i + 1) * segmentLength;
            _visemeCues.Add(new VisemeCue
            {
                viseme = visemes[i],
                startNormalized = start,
                endNormalized = end,
            });
        }
    }

    private static List<string> ExtractVisemeSequence(string text)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        var cleaned = new StringBuilder(text.Length);
        foreach (var current in text)
        {
            if (char.IsLetter(current) || char.IsWhiteSpace(current))
            {
                cleaned.Append(char.ToLowerInvariant(current));
            }
        }

        var normalized = cleaned.ToString();
        for (var i = 0; i < normalized.Length; i++)
        {
            if (char.IsWhiteSpace(normalized[i]))
            {
                continue;
            }

            string viseme;
            if (i + 1 < normalized.Length)
            {
                var pair = normalized.Substring(i, 2);
                if (pair is "sh" or "ch")
                {
                    viseme = "SHCH";
                    AppendViseme(result, viseme);
                    i++;
                    continue;
                }
            }

            viseme = CharToViseme(normalized[i]);
            AppendViseme(result, viseme);
        }

        return result;
    }

    private static void AppendViseme(List<string> sequence, string viseme)
    {
        if (string.IsNullOrWhiteSpace(viseme))
        {
            return;
        }

        if (sequence.Count == 0 || sequence[sequence.Count - 1] != viseme)
        {
            sequence.Add(viseme);
        }
    }

    private static string CharToViseme(char current)
    {
        return current switch
        {
            'a' or 'i' or 'y' => "AI",
            'e' => "E",
            'f' or 'v' => "FV",
            'l' => "L",
            'm' or 'b' or 'p' => "MBP",
            'o' => "O",
            'u' => "U",
            'w' or 'q' => "WQ",
            's' or 'z' or 'j' or 'c' or 'x' => "SHCH",
            _ => "AI",
        };
    }

    private struct VisemeCue
    {
        public string viseme;
        public float startNormalized;
        public float endNormalized;
    }
}
