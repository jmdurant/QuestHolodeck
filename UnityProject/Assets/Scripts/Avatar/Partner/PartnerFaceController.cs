using System;
using UnityEngine;

public class PartnerFaceController : MonoBehaviour, IPartnerFaceController
{
    [Header("References")]
    public SkinnedMeshRenderer faceRenderer;
    public Renderer debugRenderer;

    [Header("Blendshape Names")]
    public string smileBlendShape = "smile";
    public string blinkBlendShape = "blink";
    public string jawOpenBlendShape = "jaw";

    [Header("Tuning")]
    public float blendshapeLerpSpeed = 8f;

    private float _targetSmile;
    private float _targetBlink;
    private float _targetJawOpen;
    private float _smile;
    private float _blink;
    private float _jawOpen;
    private string _emotion = "neutral";

    public virtual void SetFacePreset(PartnerFacePreset preset, float blendTime)
    {
        switch (preset)
        {
            case PartnerFacePreset.SoftSmile:
                _targetSmile = 35f;
                _targetBlink = 5f;
                break;

            case PartnerFacePreset.Warm:
                _targetSmile = 45f;
                _targetBlink = 8f;
                break;

            case PartnerFacePreset.Teasing:
                _targetSmile = 55f;
                _targetBlink = 3f;
                break;

            case PartnerFacePreset.Concerned:
                _targetSmile = 5f;
                _targetBlink = 15f;
                break;

            case PartnerFacePreset.Focused:
                _targetSmile = 10f;
                _targetBlink = 2f;
                break;

            case PartnerFacePreset.Neutral:
            default:
                _targetSmile = 0f;
                _targetBlink = 0f;
                break;
        }
    }

    public virtual void SetEmotion(string emotion, float intensity)
    {
        _emotion = string.IsNullOrWhiteSpace(emotion) ? "neutral" : emotion;
        var normalized = Mathf.Clamp01(intensity);

        if (_emotion.Contains("warm") || _emotion.Contains("happy") || _emotion.Contains("tease"))
        {
            _targetSmile = Mathf.Max(_targetSmile, Mathf.Lerp(18f, 60f, normalized));
        }
        else if (_emotion.Contains("concern"))
        {
            _targetSmile = Mathf.Min(_targetSmile, 8f);
            _targetBlink = Mathf.Max(_targetBlink, 12f);
        }
        else if (_emotion.Contains("focus"))
        {
            _targetBlink = Mathf.Min(_targetBlink, 2f);
        }
    }

    public virtual void SetSpeechState(float mouthOpen, string speechStyle)
    {
        _targetJawOpen = Mathf.Clamp01(mouthOpen) * 100f;
        if (!string.IsNullOrWhiteSpace(speechStyle) && speechStyle.ToLowerInvariant().Contains("soft"))
        {
            _targetSmile = Mathf.Max(_targetSmile, 10f);
        }
    }

    public virtual void Tick(float deltaTime)
    {
        _smile = Mathf.Lerp(_smile, _targetSmile, deltaTime * blendshapeLerpSpeed);
        _blink = Mathf.Lerp(_blink, _targetBlink, deltaTime * blendshapeLerpSpeed);
        _jawOpen = Mathf.Lerp(_jawOpen, _targetJawOpen, deltaTime * blendshapeLerpSpeed);
        _targetJawOpen = Mathf.MoveTowards(_targetJawOpen, 0f, deltaTime * 80f);

        if (faceRenderer != null && faceRenderer.sharedMesh != null)
        {
            ApplyBlendShape(smileBlendShape, _smile);
            ApplyBlendShape(blinkBlendShape, _blink);
            ApplyBlendShape(jawOpenBlendShape, _jawOpen);
        }

        if (debugRenderer != null)
        {
            var targetColor = _emotion.Contains("warm")
                ? new Color(1f, 0.76f, 0.72f)
                : _emotion.Contains("concern")
                    ? new Color(0.92f, 0.86f, 0.76f)
                    : Color.white;
            debugRenderer.material.color = Color.Lerp(debugRenderer.material.color, targetColor, deltaTime * 3f);
        }
    }

    private void ApplyBlendShape(string shapeName, float weight)
    {
        var index = FindBlendShapeIndex(shapeName);
        if (index >= 0)
        {
            faceRenderer.SetBlendShapeWeight(index, weight);
        }
    }

    private int FindBlendShapeIndex(string shapeName)
    {
        if (faceRenderer == null || faceRenderer.sharedMesh == null || string.IsNullOrWhiteSpace(shapeName))
        {
            return -1;
        }

        var mesh = faceRenderer.sharedMesh;
        for (var i = 0; i < mesh.blendShapeCount; i++)
        {
            var current = mesh.GetBlendShapeName(i);
            if (current.Equals(shapeName, StringComparison.OrdinalIgnoreCase)
                || current.ToLowerInvariant().Contains(shapeName.ToLowerInvariant()))
            {
                return i;
            }
        }

        return -1;
    }
}
