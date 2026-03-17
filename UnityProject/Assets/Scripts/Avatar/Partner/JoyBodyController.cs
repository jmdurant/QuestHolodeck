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
    private bool _capturedBaseRootPose;
    private bool _capturedBaseHeadPose;
    private bool _capturedSpineScale;

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
        if (partnerRoot == null || headBone == null || spineBone == null)
        {
            AutoBind();
        }

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
        headBone ??= FindBoneByPath("rig_joy/root/DEF-spine/DEF-spine.001/DEF-spine.002/DEF-spine.003/DEF-spine.004/DEF-spine.005/DEF-spine.006")
            ?? FindBone("DEF-spine.006");
        chestBone ??= FindBoneByPath("rig_joy/root/spine_fk/spine_fk.001/spine_fk.002/spine_fk.003")
            ?? FindBone("spine_fk.003")
            ?? FindBone("spine_fk.002");
        spineBone ??= FindBoneByPath("rig_joy/root/spine_fk/spine_fk.001/spine_fk.002")
            ?? FindBone("spine_fk.002")
            ?? chestBone;
        leftHandBone ??= FindBoneByPath("rig_joy/root/DEF-spine/DEF-spine.001/DEF-spine.002/DEF-spine.003/DEF-spine.004/DEF-spine.005/DEF-spine.006/DEF-shoulder.L/DEF-upper_arm.L/DEF-upper_arm.L.001/DEF-forearm.L/DEF-forearm.L.001/DEF-hand.L")
            ?? FindBone("DEF-hand.L");
        rightHandBone ??= FindBoneByPath("rig_joy/root/DEF-spine/DEF-spine.001/DEF-spine.002/DEF-spine.003/DEF-spine.004/DEF-spine.005/DEF-spine.006/DEF-shoulder.R/DEF-upper_arm.R/DEF-upper_arm.R.001/DEF-forearm.R/DEF-forearm.R.001/DEF-hand.R")
            ?? FindBone("DEF-hand.R");

        // Cache base scale for breathing
        if (spineBone != null && !_capturedSpineScale)
        {
            _spineBaseScale = spineBone.localScale;
            _capturedSpineScale = true;
        }
        if (partnerRoot != null && !_capturedBaseRootPose)
        {
            _rhythmBasePosition = partnerRoot.localPosition;
            baseRootLocalPosition = partnerRoot.localPosition;
            baseRootRotation = partnerRoot.localRotation;
            _capturedBaseRootPose = true;
        }
        if (headBone != null && !_capturedBaseHeadPose)
        {
            baseHeadLocalRotation = headBone.localRotation;
            headYawReferenceDegrees = 180f;
            _capturedBaseHeadPose = true;

            var voiceController = FindFirstObjectByType<PartnerVoiceController>();
            if (voiceController != null)
            {
                voiceController.followTarget = headBone;
            }
        }
    }

    private Transform FindBoneByPath(string relativePath)
    {
        if (modelRoot == null || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        return modelRoot.Find(relativePath);
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
