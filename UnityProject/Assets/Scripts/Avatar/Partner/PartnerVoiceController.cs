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

    [Header("Runtime")]
    public string lastSpeechText;
    public string lastSpeechStyle;

    private float _speechEndTime = -1f;
    private float _speechStartTime = -1f;
    private float _speechDuration;
    private readonly List<VisemeCue> _visemeCues = new();

    public bool IsSpeaking => Time.time < _speechEndTime;

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

        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Stop();
            audioSource.Play();
        }

        Debug.Log($"[PartnerVoice] {text}");
    }

    public void Stop()
    {
        _speechEndTime = -1f;
        _speechStartTime = -1f;
        _speechDuration = 0f;
        _visemeCues.Clear();
        audioSource?.Stop();
        faceController?.SetSpeechState(0f, lastSpeechStyle);
        faceController?.ClearVisemes();
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
