using UnityEngine;

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

    [Header("Runtime")]
    public string lastSpeechText;
    public string lastSpeechStyle;

    private float _speechEndTime = -1f;

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
        _speechEndTime = Time.time + estimatedDuration;

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
        audioSource?.Stop();
        faceController?.SetSpeechState(0f, lastSpeechStyle);
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
        }
        else
        {
            faceController.SetSpeechState(0f, lastSpeechStyle);
        }
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
}
