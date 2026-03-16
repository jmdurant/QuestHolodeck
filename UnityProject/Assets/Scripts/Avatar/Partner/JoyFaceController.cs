using UnityEngine;
using System.Collections.Generic;

public class JoyFaceController : PartnerFaceController
{
    [Header("Joy Model")]
    public Transform modelRoot;
    public string modelRootName = "JoyPartner";

    [Header("Joy Face Bones")]
    public Transform jawBone;
    public Transform leftUpperLid;
    public Transform rightUpperLid;
    public Transform leftLowerLid;
    public Transform rightLowerLid;
    public Transform leftBrow;
    public Transform rightBrow;
    public Transform leftLipCorner;
    public Transform rightLipCorner;
    public Transform visemeAI;
    public Transform visemeE;
    public Transform visemeFV;
    public Transform visemeL;
    public Transform visemeMBP;
    public Transform visemeO;
    public Transform visemeShCh;
    public Transform visemeU;
    public Transform visemeWQ;

    private Vector3 _leftUpperLidBase;
    private Vector3 _rightUpperLidBase;
    private Vector3 _leftLowerLidBase;
    private Vector3 _rightLowerLidBase;
    private Vector3 _leftBrowBase;
    private Vector3 _rightBrowBase;
    private Vector3 _leftLipCornerBase;
    private Vector3 _rightLipCornerBase;
    private Quaternion _jawBaseRotation;

    private float _targetSmile;
    private float _targetBlink;
    private float _targetJawOpen;
    private float _targetBrowLift;
    private readonly Dictionary<string, Transform> _visemeBones = new();
    private readonly Dictionary<string, Vector3> _visemeBasePositions = new();
    private readonly Dictionary<string, float> _visemeWeights = new();
    private readonly Dictionary<string, float> _visemeTargetWeights = new();

    private static readonly Dictionary<string, Vector3> VisemeOffsets = new()
    {
        { "AI", new Vector3(0f, 0.0036f, -0.0028f) },
        { "E", new Vector3(0.0026f, 0.0022f, -0.0012f) },
        { "FV", new Vector3(0f, 0.0012f, 0.0026f) },
        { "L", new Vector3(0f, 0.0015f, 0.0012f) },
        { "MBP", new Vector3(0f, -0.0022f, -0.0024f) },
        { "O", new Vector3(0f, -0.0016f, 0.0038f) },
        { "SHCH", new Vector3(0.0014f, 0.0018f, 0.0024f) },
        { "U", new Vector3(0f, -0.0008f, 0.0023f) },
        { "WQ", new Vector3(0f, -0.001f, 0.003f) },
    };

    void Awake()
    {
        AutoBind();
        CacheBasePose();
    }

    public override void SetFacePreset(PartnerFacePreset preset, float blendTime)
    {
        switch (preset)
        {
            case PartnerFacePreset.SoftSmile:
                _targetSmile = 0.35f;
                _targetBlink = 0.05f;
                _targetBrowLift = 0.08f;
                break;

            case PartnerFacePreset.Warm:
                _targetSmile = 0.45f;
                _targetBlink = 0.08f;
                _targetBrowLift = 0.12f;
                break;

            case PartnerFacePreset.Teasing:
                _targetSmile = 0.55f;
                _targetBlink = 0.03f;
                _targetBrowLift = 0.05f;
                break;

            case PartnerFacePreset.Concerned:
                _targetSmile = 0.05f;
                _targetBlink = 0.12f;
                _targetBrowLift = 0.18f;
                break;

            case PartnerFacePreset.Focused:
                _targetSmile = 0.08f;
                _targetBlink = 0.02f;
                _targetBrowLift = 0.03f;
                break;

            case PartnerFacePreset.Pleasure:
                _targetSmile = 0.42f;
                _targetBlink = 0.06f;
                _targetBrowLift = 0.1f;
                break;

            case PartnerFacePreset.IntensePleasure:
                _targetSmile = 0.55f;
                _targetBlink = 0.1f;
                _targetBrowLift = 0.14f;
                break;

            case PartnerFacePreset.Breathless:
                _targetSmile = 0.18f;
                _targetBlink = 0.04f;
                _targetBrowLift = 0.08f;
                _targetJawOpen = Mathf.Max(_targetJawOpen, 0.25f);
                break;

            case PartnerFacePreset.Gentle:
                _targetSmile = 0.22f;
                _targetBlink = 0.08f;
                _targetBrowLift = 0.06f;
                break;

            case PartnerFacePreset.Neutral:
            default:
                _targetSmile = 0f;
                _targetBlink = 0f;
                _targetBrowLift = 0f;
                break;
        }
    }

    public override void SetEmotion(string emotion, float intensity)
    {
        var normalized = Mathf.Clamp01(intensity);
        var lower = string.IsNullOrWhiteSpace(emotion) ? string.Empty : emotion.ToLowerInvariant();

        if (lower.Contains("warm") || lower.Contains("happy") || lower.Contains("tease"))
        {
            _targetSmile = Mathf.Max(_targetSmile, 0.25f + normalized * 0.4f);
        }
        else if (lower.Contains("concern"))
        {
            _targetBrowLift = Mathf.Max(_targetBrowLift, 0.12f + normalized * 0.12f);
            _targetSmile = Mathf.Min(_targetSmile, 0.08f);
        }
        else if (lower.Contains("focus"))
        {
            _targetBlink = Mathf.Min(_targetBlink, 0.03f);
        }
    }

    public override void SetSpeechState(float mouthOpen, string speechStyle)
    {
        _targetJawOpen = Mathf.Clamp01(mouthOpen);
        if (!string.IsNullOrWhiteSpace(speechStyle) && speechStyle.ToLowerInvariant().Contains("soft"))
        {
            _targetSmile = Mathf.Max(_targetSmile, 0.08f);
        }
    }

    public override void SetViseme(string viseme, float weight)
    {
        var key = NormalizeViseme(viseme);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        foreach (var current in new List<string>(_visemeTargetWeights.Keys))
        {
            _visemeTargetWeights[current] = current == key ? Mathf.Clamp01(weight) : 0f;
        }

        base.SetViseme(key, weight);
    }

    public override void ClearVisemes(float blendTime = 0.1f)
    {
        foreach (var key in new List<string>(_visemeTargetWeights.Keys))
        {
            _visemeTargetWeights[key] = 0f;
        }

        base.ClearVisemes(blendTime);
    }

    public override void Tick(float deltaTime)
    {
        if (jawBone == null)
        {
            return;
        }

        if (jawBone != null)
        {
            var jawRotation = _jawBaseRotation * Quaternion.Euler(_targetJawOpen * 18f, 0f, 0f);
            jawBone.localRotation = Quaternion.Slerp(jawBone.localRotation, jawRotation, deltaTime * blendshapeLerpSpeed);
        }

        ApplyLid(leftUpperLid, _leftUpperLidBase, new Vector3(0f, -0.004f * _targetBlink, 0f), deltaTime);
        ApplyLid(rightUpperLid, _rightUpperLidBase, new Vector3(0f, -0.004f * _targetBlink, 0f), deltaTime);
        ApplyLid(leftLowerLid, _leftLowerLidBase, new Vector3(0f, 0.002f * _targetBlink, 0f), deltaTime);
        ApplyLid(rightLowerLid, _rightLowerLidBase, new Vector3(0f, 0.002f * _targetBlink, 0f), deltaTime);

        ApplyLocalPosition(leftBrow, _leftBrowBase, new Vector3(0f, 0.004f * _targetBrowLift, 0f), deltaTime);
        ApplyLocalPosition(rightBrow, _rightBrowBase, new Vector3(0f, 0.004f * _targetBrowLift, 0f), deltaTime);
        ApplyLocalPosition(leftLipCorner, _leftLipCornerBase, new Vector3(0.0025f * _targetSmile, 0.002f * _targetSmile, 0f), deltaTime);
        ApplyLocalPosition(rightLipCorner, _rightLipCornerBase, new Vector3(-0.0025f * _targetSmile, 0.002f * _targetSmile, 0f), deltaTime);
        ApplyVisemes(deltaTime);

        _targetJawOpen = Mathf.MoveTowards(_targetJawOpen, 0f, deltaTime * 2f);
        _targetBlink = Mathf.MoveTowards(_targetBlink, 0f, deltaTime * 1.5f);
    }

    private void AutoBind()
    {
        if (modelRoot == null)
        {
            var directModel = transform.Find(modelRootName);
            if (directModel != null)
            {
                modelRoot = directModel;
            }
        }

        if (modelRoot == null)
        {
            return;
        }

        jawBone ??= FindBone("jaw");
        leftUpperLid ??= FindBone("DEF-upper_eye_lid.002.L");
        rightUpperLid ??= FindBone("DEF-upper_eye_lid.002.R");
        leftLowerLid ??= FindBone("DEF-lower_eye_lid.002.L");
        rightLowerLid ??= FindBone("DEF-lower_eye_lid.002.R");
        leftBrow ??= FindBone("DEF-brow_upper.002.L");
        rightBrow ??= FindBone("DEF-brow_upper.002.R");
        leftLipCorner ??= FindBone("DEF-corner_up_lip.L");
        rightLipCorner ??= FindBone("DEF-corner_up_lip.R");
        visemeAI ??= FindBone("AI");
        visemeE ??= FindBone("E");
        visemeFV ??= FindBone("FV");
        visemeL ??= FindBone("L");
        visemeMBP ??= FindBone("MBP");
        visemeO ??= FindBone("O");
        visemeShCh ??= FindBone("ShCh");
        visemeU ??= FindBone("U");
        visemeWQ ??= FindBone("WQ");

        faceRenderer ??= FindRenderer("body");
        debugRenderer ??= faceRenderer;
        CacheVisemeBones();
    }

    private void CacheBasePose()
    {
        if (jawBone != null)
        {
            _jawBaseRotation = jawBone.localRotation;
        }

        _leftUpperLidBase = leftUpperLid != null ? leftUpperLid.localPosition : Vector3.zero;
        _rightUpperLidBase = rightUpperLid != null ? rightUpperLid.localPosition : Vector3.zero;
        _leftLowerLidBase = leftLowerLid != null ? leftLowerLid.localPosition : Vector3.zero;
        _rightLowerLidBase = rightLowerLid != null ? rightLowerLid.localPosition : Vector3.zero;
        _leftBrowBase = leftBrow != null ? leftBrow.localPosition : Vector3.zero;
        _rightBrowBase = rightBrow != null ? rightBrow.localPosition : Vector3.zero;
        _leftLipCornerBase = leftLipCorner != null ? leftLipCorner.localPosition : Vector3.zero;
        _rightLipCornerBase = rightLipCorner != null ? rightLipCorner.localPosition : Vector3.zero;
        CacheVisemeBasePose();
    }

    private Transform FindBone(string boneName)
    {
        if (modelRoot == null)
        {
            return null;
        }

        var transforms = modelRoot.GetComponentsInChildren<Transform>(true);
        foreach (var current in transforms)
        {
            if (current.name == boneName)
            {
                return current;
            }
        }

        return null;
    }

    private SkinnedMeshRenderer FindRenderer(string rendererName)
    {
        if (modelRoot == null)
        {
            return null;
        }

        var renderers = modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var current in renderers)
        {
            if (current.name == rendererName)
            {
                return current;
            }
        }

        return null;
    }

    private void ApplyLid(Transform target, Vector3 basePosition, Vector3 offset, float deltaTime)
    {
        ApplyLocalPosition(target, basePosition, offset, deltaTime);
    }

    private void ApplyLocalPosition(Transform target, Vector3 basePosition, Vector3 offset, float deltaTime)
    {
        if (target == null)
        {
            return;
        }

        target.localPosition = Vector3.Lerp(target.localPosition, basePosition + offset, deltaTime * blendshapeLerpSpeed);
    }

    private void CacheVisemeBones()
    {
        _visemeBones.Clear();

        AddVisemeBone("AI", visemeAI);
        AddVisemeBone("E", visemeE);
        AddVisemeBone("FV", visemeFV);
        AddVisemeBone("L", visemeL);
        AddVisemeBone("MBP", visemeMBP);
        AddVisemeBone("O", visemeO);
        AddVisemeBone("SHCH", visemeShCh);
        AddVisemeBone("U", visemeU);
        AddVisemeBone("WQ", visemeWQ);
    }

    private void CacheVisemeBasePose()
    {
        _visemeBasePositions.Clear();
        _visemeWeights.Clear();
        _visemeTargetWeights.Clear();

        foreach (var entry in _visemeBones)
        {
            if (entry.Value == null)
            {
                continue;
            }

            _visemeBasePositions[entry.Key] = entry.Value.localPosition;
            _visemeWeights[entry.Key] = 0f;
            _visemeTargetWeights[entry.Key] = 0f;
        }
    }

    private void ApplyVisemes(float deltaTime)
    {
        foreach (var entry in _visemeBones)
        {
            if (entry.Value == null || !_visemeBasePositions.ContainsKey(entry.Key))
            {
                continue;
            }

            var current = _visemeWeights.TryGetValue(entry.Key, out var existingWeight) ? existingWeight : 0f;
            var target = _visemeTargetWeights.TryGetValue(entry.Key, out var targetWeight) ? targetWeight : 0f;
            current = Mathf.Lerp(current, target, deltaTime * blendshapeLerpSpeed);
            _visemeWeights[entry.Key] = current;

            var basePosition = _visemeBasePositions[entry.Key];
            var offset = VisemeOffsets.TryGetValue(entry.Key, out var configuredOffset) ? configuredOffset : Vector3.zero;
            entry.Value.localPosition = Vector3.Lerp(
                entry.Value.localPosition,
                basePosition + offset * current,
                deltaTime * blendshapeLerpSpeed);
        }
    }

    private void AddVisemeBone(string key, Transform bone)
    {
        if (bone != null)
        {
            _visemeBones[key] = bone;
        }
    }

    private static string NormalizeViseme(string viseme)
    {
        if (string.IsNullOrWhiteSpace(viseme))
        {
            return null;
        }

        return viseme.Trim().ToUpperInvariant() switch
        {
            "SHCH" => "SHCH",
            "SHCHH" => "SHCH",
            "SHCH_" => "SHCH",
            _ => viseme.Trim().ToUpperInvariant(),
        };
    }
}
