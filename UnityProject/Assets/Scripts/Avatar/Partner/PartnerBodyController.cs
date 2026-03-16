using UnityEngine;

public class PartnerBodyController : MonoBehaviour, IPartnerBodyController
{
    [Header("References")]
    public SexKitAvatarDriver avatarDriver;
    public Transform partnerRoot;
    public Transform headBone;
    public Transform chestBone;
    public Transform leftHandBone;
    public Transform rightHandBone;

    [Header("Tuning")]
    public float positionLerpSpeed = 4f;
    public float rotationLerpSpeed = 6f;

    protected Vector3 baseRootLocalPosition;
    protected Quaternion baseRootRotation;
    protected Vector3 targetLocalOffset;
    protected Vector3? lookTarget;
    protected Vector3? leftHandTarget;
    protected Vector3? rightHandTarget;

    protected bool initialized;

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

        var lowered = gestureName.ToLowerInvariant();
        if (lowered.Contains("left"))
        {
            SetPoseIntent(PartnerPoseIntent.ReachLeft, blendTime);
        }
        else if (lowered.Contains("right"))
        {
            SetPoseIntent(PartnerPoseIntent.ReachRight, blendTime);
        }
        else if (lowered.Contains("comfort"))
        {
            SetPoseIntent(PartnerPoseIntent.Comforting, blendTime);
        }
        else if (lowered.Contains("brace"))
        {
            SetPoseIntent(PartnerPoseIntent.Brace, blendTime);
        }
    }

    public virtual void ClearTargets(float blendTime)
    {
        lookTarget = null;
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

        if (lookTarget.HasValue)
        {
            ApplyLookTarget(partnerRoot, lookTarget.Value, deltaTime);

            if (headBone != null)
            {
                ApplyLookTarget(headBone, lookTarget.Value, deltaTime);
            }
        }
        else
        {
            partnerRoot.localRotation = Quaternion.Slerp(partnerRoot.localRotation, baseRootRotation, deltaTime * rotationLerpSpeed);
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
        ResolvePartnerRoot();

        if (partnerRoot != null)
        {
            baseRootLocalPosition = partnerRoot.localPosition;
            baseRootRotation = partnerRoot.localRotation;
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
}
