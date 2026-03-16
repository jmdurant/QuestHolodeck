using UnityEngine;

public class PartnerBodyController : MonoBehaviour, IPartnerBodyController
{
    [Header("References")]
    public SexKitAvatarDriver avatarDriver;
    public QuestTrackingMerge trackingMerge;
    public Transform partnerRoot;
    public Transform headBone;
    public Transform chestBone;
    public Transform leftHandBone;
    public Transform rightHandBone;

    [Header("Tuning")]
    public float positionLerpSpeed = 4f;
    public float rotationLerpSpeed = 6f;

    [Header("Head Tracking")]
    public bool followUserHeadByDefault = true;
    public bool clampHeadTracking = true;
    public float maxHeadYawDegrees = 70f;
    public float maxHeadPitchUpDegrees = 28f;
    public float maxHeadPitchDownDegrees = 38f;

    protected Vector3 baseRootLocalPosition;
    protected Quaternion baseRootRotation;
    protected Quaternion baseHeadLocalRotation;
    protected Vector3 targetLocalOffset;
    protected Vector3? lookTarget;
    protected Vector3? headLookTarget;
    protected Vector3? leftHandTarget;
    protected Vector3? rightHandTarget;

    protected bool initialized;
    public bool rotateRootTowardLookTarget = true;

    protected virtual void Awake()
    {
        InitializeIfNeeded();
    }

    public virtual void SetPoseIntent(PartnerPoseIntent intent, float blendTime)
    {
        InitializeIfNeeded();
        targetLocalOffset = intent switch
        {
            PartnerPoseIntent.LeanIn => new Vector3(0f, 0f, -0.2f),
            PartnerPoseIntent.LeanBack => new Vector3(0f, 0f, 0.18f),
            PartnerPoseIntent.Kneeling => new Vector3(0f, -0.28f, -0.1f),
            PartnerPoseIntent.Reclined => new Vector3(0f, -0.15f, 0.1f),
            PartnerPoseIntent.ReachLeft => new Vector3(-0.08f, 0f, -0.1f),
            PartnerPoseIntent.ReachRight => new Vector3(0.08f, 0f, -0.1f),
            PartnerPoseIntent.Brace => new Vector3(0f, -0.08f, -0.05f),
            PartnerPoseIntent.Comforting => new Vector3(0f, 0f, -0.12f),
            _ => Vector3.zero,
        };
    }

    public virtual void SetAttentionTarget(PartnerAttentionTarget target, Vector3? worldTarget, float blendTime)
    {
        lookTarget = worldTarget;
        headLookTarget = worldTarget;
    }

    public virtual void SetHandTargets(Vector3? leftTarget, Vector3? rightTarget, float blendTime)
    {
        leftHandTarget = leftTarget;
        rightHandTarget = rightTarget;
    }

    public virtual void PlayGesture(string gestureName, float blendTime)
    {
        if (string.IsNullOrWhiteSpace(gestureName))
        {
            return;
        }

        switch (gestureName.ToLowerInvariant())
        {
            case "reach":
                SetPoseIntent(PartnerPoseIntent.ReachRight, blendTime);
                break;

            case "wave":
                SetPoseIntent(PartnerPoseIntent.ReachRight, blendTime);
                break;

            case "beckon":
                SetPoseIntent(PartnerPoseIntent.LeanIn, blendTime);
                break;

            case "touch_face":
                SetPoseIntent(PartnerPoseIntent.ReachRight, blendTime);
                if (lookTarget.HasValue)
                {
                    rightHandTarget = lookTarget.Value;
                }
                break;

            case "hair_flip":
                SetPoseIntent(PartnerPoseIntent.LeanBack, blendTime);
                break;

            case "stretch":
                SetPoseIntent(PartnerPoseIntent.LeanBack, blendTime);
                break;

            case "nod":
                SetPoseIntent(PartnerPoseIntent.LeanIn, Mathf.Min(blendTime, 0.15f));
                break;

            case "shake_head":
                SetPoseIntent(PartnerPoseIntent.Idle, blendTime);
                break;

            case "shrug":
                SetPoseIntent(PartnerPoseIntent.LeanBack, Mathf.Min(blendTime, 0.2f));
                break;

            default:
                var lowered = gestureName.ToLowerInvariant();
                if (lowered.Contains("left")) SetPoseIntent(PartnerPoseIntent.ReachLeft, blendTime);
                else if (lowered.Contains("right")) SetPoseIntent(PartnerPoseIntent.ReachRight, blendTime);
                else if (lowered.Contains("comfort")) SetPoseIntent(PartnerPoseIntent.Comforting, blendTime);
                else if (lowered.Contains("brace")) SetPoseIntent(PartnerPoseIntent.Brace, blendTime);
                break;
        }
    }

    public virtual void ClearTargets(float blendTime)
    {
        lookTarget = null;
        headLookTarget = null;
        leftHandTarget = null;
        rightHandTarget = null;
        targetLocalOffset = Vector3.zero;
    }

    public virtual void Tick(float deltaTime)
    {
        InitializeIfNeeded();
        if (partnerRoot == null)
        {
            return;
        }

        partnerRoot.localPosition = Vector3.Lerp(
            partnerRoot.localPosition,
            baseRootLocalPosition + targetLocalOffset,
            deltaTime * positionLerpSpeed);

        if (lookTarget.HasValue && rotateRootTowardLookTarget)
        {
            ApplyLookTarget(partnerRoot, lookTarget.Value, deltaTime);
        }

        var effectiveHeadTarget = GetEffectiveHeadLookTarget();
        if (effectiveHeadTarget.HasValue && headBone != null)
        {
            ApplyHeadLookTarget(headBone, effectiveHeadTarget.Value, deltaTime);
        }
        else if (!lookTarget.HasValue)
        {
            partnerRoot.localRotation = Quaternion.Slerp(partnerRoot.localRotation, baseRootRotation, deltaTime * rotationLerpSpeed);
            if (headBone != null)
            {
                headBone.localRotation = Quaternion.Slerp(headBone.localRotation, baseHeadLocalRotation, deltaTime * rotationLerpSpeed);
            }
        }

        if (leftHandTarget.HasValue && leftHandBone != null)
        {
            leftHandBone.position = Vector3.Lerp(leftHandBone.position, leftHandTarget.Value, deltaTime * positionLerpSpeed);
        }

        if (rightHandTarget.HasValue && rightHandBone != null)
        {
            rightHandBone.position = Vector3.Lerp(rightHandBone.position, rightHandTarget.Value, deltaTime * positionLerpSpeed);
        }
    }

    protected void InitializeIfNeeded()
    {
        if (initialized)
        {
            if (partnerRoot == null)
            {
                ResolvePartnerRoot();
            }

            return;
        }

        avatarDriver ??= GetComponent<SexKitAvatarDriver>();
        trackingMerge ??= FindFirstObjectByType<QuestTrackingMerge>();
        ResolvePartnerRoot();

        if (partnerRoot != null)
        {
            baseRootLocalPosition = partnerRoot.localPosition;
            baseRootRotation = partnerRoot.localRotation;
        }

        if (headBone != null)
        {
            baseHeadLocalRotation = headBone.localRotation;
        }

        initialized = true;
    }

    protected virtual void ResolvePartnerRoot()
    {
        if (partnerRoot == null && avatarDriver != null)
        {
            partnerRoot = avatarDriver.bodyBRoot;
        }
    }

    protected void ApplyLookTarget(Transform source, Vector3 target, float deltaTime)
    {
        var direction = target - source.position;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        source.rotation = Quaternion.Slerp(source.rotation, targetRotation, deltaTime * rotationLerpSpeed);
    }

    protected void SetHeadLookTarget(Vector3? worldTarget)
    {
        headLookTarget = worldTarget;
    }

    protected Vector3? GetEffectiveHeadLookTarget()
    {
        if (headLookTarget.HasValue)
        {
            return headLookTarget;
        }

        if (lookTarget.HasValue)
        {
            return lookTarget;
        }

        if (followUserHeadByDefault && trackingMerge != null && trackingMerge.HeadPosition != Vector3.zero)
        {
            return trackingMerge.HeadPosition;
        }

        return null;
    }

    protected void ApplyHeadLookTarget(Transform source, Vector3 target, float deltaTime)
    {
        if (!clampHeadTracking || source.parent == null)
        {
            ApplyLookTarget(source, target, deltaTime);
            return;
        }

        var worldDirection = target - source.position;
        if (worldDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var parentSpaceDirection = source.parent.InverseTransformDirection(worldDirection.normalized);
        var baseSpaceDirection = Quaternion.Inverse(baseHeadLocalRotation) * parentSpaceDirection;
        var yaw = Mathf.Atan2(baseSpaceDirection.x, baseSpaceDirection.z) * Mathf.Rad2Deg;
        var flatDistance = Mathf.Max(0.0001f, new Vector2(baseSpaceDirection.x, baseSpaceDirection.z).magnitude);
        var pitch = -Mathf.Atan2(baseSpaceDirection.y, flatDistance) * Mathf.Rad2Deg;

        yaw = Mathf.Clamp(yaw, -maxHeadYawDegrees, maxHeadYawDegrees);
        pitch = Mathf.Clamp(pitch, -maxHeadPitchUpDegrees, maxHeadPitchDownDegrees);

        var clampedLocalRotation = baseHeadLocalRotation * Quaternion.Euler(pitch, yaw, 0f);
        source.localRotation = Quaternion.Slerp(source.localRotation, clampedLocalRotation, deltaTime * rotationLerpSpeed);
    }
}
