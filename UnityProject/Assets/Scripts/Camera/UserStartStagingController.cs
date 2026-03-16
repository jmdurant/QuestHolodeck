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
        var desiredLookDirection = ResolveDesiredLookDirection(targetEyePosition, userAnchorRotation);
        ApplyRigPose(targetEyePosition, desiredLookDirection);
        _hasStaged = true;
    }

    private Vector3 ResolveDesiredLookDirection(Vector3 targetEyePosition, Quaternion userAnchorRotation)
    {
        if (avatarDriver != null && avatarDriver.TryGetDefaultStandingAnchor(false, out var partnerAnchorPosition, out _))
        {
            var towardPartner = (partnerAnchorPosition + Vector3.up * partnerLookHeight) - targetEyePosition;
            if (towardPartner.sqrMagnitude > 0.001f)
            {
                return towardPartner.normalized;
            }
        }

        var fallbackForward = userAnchorRotation * Vector3.forward;
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
        var desiredEyeRotation = Quaternion.LookRotation(desiredLookDirection.normalized, Vector3.up);
        var eyeLocalRotation = Quaternion.Inverse(rigTransform.rotation) * eyeTransform.rotation;
        rigTransform.rotation = desiredEyeRotation * Quaternion.Inverse(eyeLocalRotation);

        var translatedEyePosition = eyeTransform.position;
        rigTransform.position += targetEyePosition - translatedEyePosition;
    }
}
