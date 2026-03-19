using UnityEngine;

public class JoyBodyController : PartnerBodyController
{
    [Header("Joy Model")]
    public Transform modelRoot;
    public string modelRootName = "JoyPartner";
    public float conversationDistance = 0.8f;
    public bool stageAtBedSideByDefault = true;
    public bool defaultHeadTurnTowardUser = true;
    public float defaultUserLookHeight = 1.35f;

    [Header("Breathing")]
    public Transform spineBone;
    public float breathingRate = 14f;
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

    [Header("Debug")]
    public bool drawFacingDebug = true;
    public bool drawFacingDebugInEditMode = false;
    public float debugForwardLength = 0.7f;
    public float debugTargetSphereRadius = 0.06f;

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

        TryApplyDefaultStage();
        base.Tick(deltaTime);

        if (spineBone != null && breathingRate > 0)
        {
            float breathCycleHz = breathingRate / 60f;
            _breathPhase += deltaTime * breathCycleHz * Mathf.PI * 2f;
            float breathScale = 1f + Mathf.Sin(_breathPhase) * breathingDepth * 0.03f;
            spineBone.localScale = Vector3.Lerp(
                spineBone.localScale,
                new Vector3(_spineBaseScale.x * breathScale, _spineBaseScale.y, _spineBaseScale.z * breathScale),
                deltaTime * 8f);
        }

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

    public void SetBreathing(float rate, float depth)
    {
        breathingRate = rate;
        breathingDepth = Mathf.Clamp01(depth);
    }

    public void SetRhythm(float hz, float intensity, float amplitude)
    {
        rhythmHz = hz;
        rhythmIntensity = Mathf.Clamp01(intensity);
        rhythmAmplitude = Mathf.Clamp01(amplitude);
    }

    // --- Private ---

    private void AutoBind()
    {
        if (modelRoot == null)
        {
            var directModel = transform.Find(modelRootName);
            if (directModel != null)
                modelRoot = directModel;
        }

        if (modelRoot == null)
            return;

        partnerRoot = modelRoot;
        ClearDestroyedBoneReferences();
        if (spineBone == null) spineBone = null; // spineBone is on this class, not base

        headBone ??= FindBoneByPath("rig_joy/root/DEF-spine/DEF-spine.001/DEF-spine.002/DEF-spine.003/DEF-spine.004/DEF-spine.005/DEF-spine.006")
            ?? FindBone("DEF-spine.006");
        chestBone ??= FindBone("spine_fk.003") ?? FindBone("spine_fk.002");
        spineBone ??= FindBone("spine_fk.002") ?? chestBone;
        leftHandBone ??= FindBone("DEF-hand.L");
        rightHandBone ??= FindBone("DEF-hand.R");

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
            // Head bone local forward aligns with the model's face direction at rest.
            // Reference of 0 means "straight ahead in bone-local space = toward camera".
            headYawReferenceDegrees = 0f;
            _capturedBaseHeadPose = true;

            var voiceController = FindFirstObjectByType<PartnerVoiceController>();
            if (voiceController != null)
                voiceController.followTarget = headBone;
        }
    }

    /// Place Joy in front of the user on first frame. Activity mode uses bed-side anchor
    /// from the avatar driver; conversation/training mode uses the camera position.
    private void TryApplyDefaultStage()
    {
        if (_defaultStageApplied || partnerRoot == null)
            return;

        if (stageAtBedSideByDefault)
        {
            TryApplyBedSideStage();
            return;
        }

        TryApplyConversationStage();
    }

    private void TryApplyBedSideStage()
    {
        if (avatarDriver == null)
            return;

        if (!avatarDriver.TryGetDefaultStandingAnchor(false, out var position, out var rotation))
            return;

        partnerRoot.position = position;
        partnerRoot.rotation = rotation;
        CaptureBasePose();
        rotateRootTowardLookTarget = false;

        if (defaultHeadTurnTowardUser && avatarDriver.TryGetDefaultStandingAnchor(true, out var userPosition, out _))
            SetHeadLookTarget(userPosition + Vector3.up * defaultUserLookHeight);

        _defaultStageApplied = true;
    }

    private void TryApplyConversationStage()
    {
        var cam = Camera.main;
        if (cam == null)
            return;

        // Place Joy at conversation distance in front of the camera, on the floor, facing the user.
        // Model faces +Z at identity (Blender FBX with applied rotations + standard axis conversion).
        var camPos = cam.transform.position;
        var flatFwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (flatFwd.sqrMagnitude < 0.01f)
            flatFwd = Vector3.forward;

        var feetPos = camPos + flatFwd * conversationDistance;
        feetPos.y = 0f;

        var facingDir = Vector3.ProjectOnPlane(camPos - feetPos, Vector3.up);
        partnerRoot.position = feetPos;
        if (facingDir.sqrMagnitude > 0.001f)
            partnerRoot.rotation = Quaternion.LookRotation(facingDir.normalized);

        CaptureBasePose();
        _defaultStageApplied = true;
    }

    private void CaptureBasePose()
    {
        baseRootLocalPosition = partnerRoot.localPosition;
        baseRootRotation = partnerRoot.localRotation;
        _rhythmBasePosition = partnerRoot.localPosition;
        _capturedBaseRootPose = true;
    }

    private Transform FindBoneByPath(string relativePath)
    {
        if (modelRoot == null || string.IsNullOrWhiteSpace(relativePath))
            return null;
        return modelRoot.Find(relativePath);
    }

    private Transform FindBone(string boneName)
    {
        if (modelRoot == null)
            return null;

        foreach (var t in modelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == boneName)
                return t;
        }
        return null;
    }

    private void OnDrawGizmos()
    {
        if (!drawFacingDebug)
            return;

        if (!Application.isPlaying && !drawFacingDebugInEditMode)
            return;

        if (partnerRoot == null || modelRoot == null)
            AutoBind();

        var userViewPosition = ResolveUserViewPosition();
        if (!userViewPosition.HasValue || partnerRoot == null)
            return;

        var rootOrigin = partnerRoot.position + Vector3.up * 1.35f;

        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.95f);
        Gizmos.DrawLine(rootOrigin, rootOrigin + partnerRoot.forward * debugForwardLength);

        Gizmos.color = new Color(0.2f, 1f, 0.65f, 0.9f);
        Gizmos.DrawLine(rootOrigin, userViewPosition.Value);
        Gizmos.DrawWireSphere(userViewPosition.Value, debugTargetSphereRadius);

        if (headBone != null)
        {
            var effectiveHeadTarget = GetEffectiveHeadLookTarget() ?? userViewPosition.Value;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawLine(headBone.position, effectiveHeadTarget);
            Gizmos.DrawWireSphere(headBone.position, debugTargetSphereRadius * 0.75f);
        }
    }

    private Vector3? ResolveUserViewPosition()
    {
        if (trackingMerge != null && trackingMerge.HeadPosition != Vector3.zero)
            return trackingMerge.HeadPosition;

        var mainCam = Camera.main;
        if (mainCam != null)
            return mainCam.transform.position;

        var rig = FindFirstObjectByType<OVRCameraRig>();
        if (rig != null)
        {
            if (rig.centerEyeAnchor != null)
                return rig.centerEyeAnchor.position;

            return rig.transform.position;
        }

        return null;
    }
}
