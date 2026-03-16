using UnityEngine;

public class UserStartStagingController : MonoBehaviour
{
    [Header("References")]
    public OVRCameraRig cameraRig;
    public SexKitAvatarDriver avatarDriver;

    [Header("Startup Staging")]
    public bool stageOnStart = true;
    public bool stageOnlyOnce = true;
    public float defaultEyeHeight = 1.55f;
    public float partnerLookHeight = 0.65f;

    private bool _hasStaged;

    void LateUpdate()
    {
        if (!stageOnStart || (stageOnlyOnce && _hasStaged))
        {
            return;
        }

        cameraRig ??= FindFirstObjectByType<OVRCameraRig>();
        avatarDriver ??= FindFirstObjectByType<SexKitAvatarDriver>();
        if (cameraRig == null || avatarDriver == null)
        {
            return;
        }

        if (!avatarDriver.TryGetDefaultStandingAnchor(true, out var userAnchorPosition, out var userAnchorRotation))
        {
            return;
        }

        var targetEyeHeight = defaultEyeHeight;
        if (cameraRig.centerEyeAnchor != null && cameraRig.centerEyeAnchor.localPosition.y > 0.5f)
        {
            targetEyeHeight = cameraRig.centerEyeAnchor.localPosition.y;
        }

        var targetEyePosition = userAnchorPosition + Vector3.up * targetEyeHeight;
        var desiredLookDirection = ResolveDesiredLookDirection(userAnchorPosition, userAnchorRotation);
        ApplyRigPose(targetEyePosition, desiredLookDirection);
        _hasStaged = true;
    }

    private Vector3 ResolveDesiredLookDirection(Vector3 userAnchorPosition, Quaternion userAnchorRotation)
    {
        if (avatarDriver != null && avatarDriver.TryGetDefaultStandingAnchor(false, out var partnerAnchorPosition, out _))
        {
            var towardPartner = Vector3.ProjectOnPlane(
                (partnerAnchorPosition + Vector3.up * partnerLookHeight) - userAnchorPosition,
                Vector3.up);
            if (towardPartner.sqrMagnitude > 0.001f)
            {
                return towardPartner.normalized;
            }
        }

        var fallbackForward = Vector3.ProjectOnPlane(userAnchorRotation * Vector3.forward, Vector3.up);
        if (fallbackForward.sqrMagnitude > 0.001f)
        {
            return fallbackForward.normalized;
        }

        return Vector3.forward;
    }

    private void ApplyRigPose(Vector3 targetEyePosition, Vector3 desiredLookDirection)
    {
        var rigTransform = cameraRig.transform;
        var eyeTransform = cameraRig.centerEyeAnchor != null ? cameraRig.centerEyeAnchor : rigTransform;
        var currentEyeForward = Vector3.ProjectOnPlane(eyeTransform.forward, Vector3.up);
        if (currentEyeForward.sqrMagnitude < 0.001f)
        {
            currentEyeForward = Vector3.forward;
        }

        var yawDelta = Quaternion.FromToRotation(currentEyeForward.normalized, desiredLookDirection);
        var eyePositionBeforeRotation = eyeTransform.position;
        rigTransform.RotateAround(eyePositionBeforeRotation, Vector3.up, yawDelta.eulerAngles.y);

        var translatedEyePosition = eyeTransform.position;
        rigTransform.position += targetEyePosition - translatedEyePosition;
    }
}
