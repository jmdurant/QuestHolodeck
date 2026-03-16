using UnityEngine;

public class JoyBodyController : PartnerBodyController
{
    [Header("Joy Model")]
    public Transform modelRoot;
    public string modelRootName = "JoyPartner";
    public bool stageAtBedSideByDefault = true;
    public bool defaultHeadTurnTowardUser = true;
    public float defaultUserLookHeight = 1.35f;

    [Header("Breathing")]
    public Transform spineBone;       // spine_fk.002 or similar — chest expansion
    public float breathingRate = 14f; // breaths per minute
    public float breathingDepth = 0.3f;
    private Vector3 _spineBaseScale;
    private float _breathPhase;

    [Header("Rhythm")]
    public float rhythmHz;
    public float rhythmIntensity;
    public float rhythmAmplitude;
    private float _rhythmPhase;
    private Vector3 _rhythmBasePosition;
    private bool _defaultStageApplied;

    protected override void Awake()
    {
        AutoBind();
        base.Awake();

        avatarDriver ??= GetComponent<SexKitAvatarDriver>();
        if (avatarDriver != null && avatarDriver.bodyBRoot != null)
        {
            avatarDriver.bodyBRoot.gameObject.SetActive(false);
        }

        var voiceController = FindFirstObjectByType<PartnerVoiceController>();
        if (voiceController != null && headBone != null)
        {
            voiceController.followTarget = headBone;
        }
    }

    protected override void ResolvePartnerRoot()
    {
        if (partnerRoot == null && modelRoot != null)
        {
            partnerRoot = modelRoot;
            return;
        }

        base.ResolvePartnerRoot();
    }

    public override void Tick(float deltaTime)
    {
        TryApplyDefaultBedSideStage();
        base.Tick(deltaTime);

        // Breathing — chest bone scale oscillation
        if (spineBone != null && breathingRate > 0)
        {
            float breathCycleHz = breathingRate / 60f;  // breaths/sec
            _breathPhase += deltaTime * breathCycleHz * Mathf.PI * 2f;
            float breathScale = 1f + Mathf.Sin(_breathPhase) * breathingDepth * 0.03f;
            spineBone.localScale = Vector3.Lerp(
                spineBone.localScale,
                new Vector3(_spineBaseScale.x * breathScale, _spineBaseScale.y, _spineBaseScale.z * breathScale),
                deltaTime * 8f);
        }

        // Rhythm — sinusoidal body oscillation for physical mode
        if (partnerRoot != null && rhythmHz > 0.1f && rhythmIntensity > 0.01f)
        {
            _rhythmPhase += deltaTime * rhythmHz * Mathf.PI * 2f;
            float oscillation = Mathf.Sin(_rhythmPhase) * rhythmAmplitude * 0.05f;
            var rhythmOffset = new Vector3(0f, oscillation, oscillation * 0.5f);
            partnerRoot.localPosition = Vector3.Lerp(
                partnerRoot.localPosition,
                baseRootLocalPosition + targetLocalOffset + rhythmOffset,
                deltaTime * positionLerpSpeed);
        }
    }

    /// Update breathing parameters from PartnerDirector runtime state
    public void SetBreathing(float rate, float depth)
    {
        breathingRate = rate;
        breathingDepth = Mathf.Clamp01(depth);
    }

    /// Update rhythm parameters from ControlFrame physical data
    public void SetRhythm(float hz, float intensity, float amplitude)
    {
        rhythmHz = hz;
        rhythmIntensity = Mathf.Clamp01(intensity);
        rhythmAmplitude = Mathf.Clamp01(amplitude);
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

        partnerRoot = modelRoot;
        headBone ??= FindBone("DEF-spine.006");
        chestBone ??= FindBone("spine_fk.003") ?? FindBone("spine_fk.002");
        spineBone ??= FindBone("spine_fk.002") ?? chestBone;
        leftHandBone ??= FindBone("DEF-hand.L");
        rightHandBone ??= FindBone("DEF-hand.R");

        // Cache base scale for breathing
        if (spineBone != null)
        {
            _spineBaseScale = spineBone.localScale;
        }
        if (partnerRoot != null)
        {
            _rhythmBasePosition = partnerRoot.localPosition;
        }
    }

    private void TryApplyDefaultBedSideStage()
    {
        if (_defaultStageApplied || !stageAtBedSideByDefault || avatarDriver == null || partnerRoot == null)
        {
            return;
        }

        if (avatarDriver.TryGetDefaultStandingAnchor(false, out var position, out var rotation))
        {
            partnerRoot.position = position;
            partnerRoot.rotation = rotation;
            baseRootLocalPosition = partnerRoot.localPosition;
            baseRootRotation = partnerRoot.localRotation;
            _rhythmBasePosition = partnerRoot.localPosition;
            rotateRootTowardLookTarget = false;

            if (defaultHeadTurnTowardUser && avatarDriver.TryGetDefaultStandingAnchor(true, out var userPosition, out _))
            {
                SetHeadLookTarget(userPosition + Vector3.up * defaultUserLookHeight);
            }

            _defaultStageApplied = true;
        }
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
}
