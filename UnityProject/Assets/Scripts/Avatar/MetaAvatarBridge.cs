using System.Collections.Generic;
using Oculus.Avatar2;
using UnityEngine;

public class MetaAvatarBridge : MonoBehaviour
{
    [Header("Avatar Mode")]
    public AvatarSourceMode avatarMode = AvatarSourceMode.MetaAvatar;

    public enum AvatarSourceMode
    {
        MetaAvatar,    // User = Meta Avatar, Partner = JOY (production)
        DualHumanoid,  // User = JOY A, Partner = JOY B (dev/wiring)
        PrimitiveOnly  // User = spheres, Partner = spheres (debug)
    }

    [Header("Meta Avatar")]
    public bool hidePrimitiveLocalBody = true;
    public OVRCameraRig cameraRig;
    public OvrAvatarManager avatarManager;
    public SexKitMetaAvatarInputManager avatarInputManager;
    public SampleAvatarEntity avatarA;

    [Header("Humanoid (JOY-on-JOY or fallback)")]
    public Animator humanoidAnimatorA;   // User body — assign second JOY instance
    public Animator humanoidAnimatorB;   // Partner body — assign JOY partner instance

    // Backward compat
    public bool useMetaAvatars
    {
        get => avatarMode == AvatarSourceMode.MetaAvatar;
        set => avatarMode = value ? AvatarSourceMode.MetaAvatar : AvatarSourceMode.DualHumanoid;
    }

    [Header("Quest Tracking Merge")]
    public QuestTrackingMerge questTracking;

    private SexKitAvatarDriver _avatarDriver;

    // Core 16 joints — always available
    private static readonly Dictionary<string, HumanBodyBones> CoreMapping = new()
    {
        {"head", HumanBodyBones.Head},
        {"neck", HumanBodyBones.Neck},
        {"leftShoulder", HumanBodyBones.LeftUpperArm},
        {"rightShoulder", HumanBodyBones.RightUpperArm},
        {"leftElbow", HumanBodyBones.LeftLowerArm},
        {"rightElbow", HumanBodyBones.RightLowerArm},
        {"leftWrist", HumanBodyBones.LeftHand},
        {"rightWrist", HumanBodyBones.RightHand},
        {"spine", HumanBodyBones.Spine},
        {"hip", HumanBodyBones.Hips},
        {"leftHip", HumanBodyBones.LeftUpperLeg},
        {"rightHip", HumanBodyBones.RightUpperLeg},
        {"leftKnee", HumanBodyBones.LeftLowerLeg},
        {"rightKnee", HumanBodyBones.RightLowerLeg},
        {"leftAnkle", HumanBodyBones.LeftFoot},
        {"rightAnkle", HumanBodyBones.RightFoot},
    };

    // Extended joints — available at Tier 1 (ARKit 91 joints)
    private static readonly Dictionary<string, HumanBodyBones> ExtendedMapping = new()
    {
        // Full spine chain
        {"spine1", HumanBodyBones.UpperChest},
        {"spine2", HumanBodyBones.Chest},
        {"spine3", HumanBodyBones.Spine},
        {"spine7", HumanBodyBones.Hips},

        // Upper/lower arms (more precise than shoulder→elbow)
        {"leftUpperArm", HumanBodyBones.LeftUpperArm},
        {"rightUpperArm", HumanBodyBones.RightUpperArm},
        {"leftForearm", HumanBodyBones.LeftLowerArm},
        {"rightForearm", HumanBodyBones.RightLowerArm},

        // Upper/lower legs
        {"leftUpperLeg", HumanBodyBones.LeftUpperLeg},
        {"rightUpperLeg", HumanBodyBones.RightUpperLeg},
        {"leftLowerLeg", HumanBodyBones.LeftLowerLeg},
        {"rightLowerLeg", HumanBodyBones.RightLowerLeg},

        // Feet and toes
        {"leftFoot", HumanBodyBones.LeftFoot},
        {"rightFoot", HumanBodyBones.RightFoot},
        {"leftToes", HumanBodyBones.LeftToes},
        {"rightToes", HumanBodyBones.RightToes},

        // Left hand fingers (from ARKit, supplements Quest hand tracking)
        {"leftHandThumb1", HumanBodyBones.LeftThumbProximal},
        {"leftHandThumb2", HumanBodyBones.LeftThumbIntermediate},
        {"leftHandThumb3", HumanBodyBones.LeftThumbDistal},
        {"leftHandIndex1", HumanBodyBones.LeftIndexProximal},
        {"leftHandIndex2", HumanBodyBones.LeftIndexIntermediate},
        {"leftHandIndex3", HumanBodyBones.LeftIndexDistal},
        {"leftHandMiddle1", HumanBodyBones.LeftMiddleProximal},
        {"leftHandMiddle2", HumanBodyBones.LeftMiddleIntermediate},
        {"leftHandMiddle3", HumanBodyBones.LeftMiddleDistal},
        {"leftHandRing1", HumanBodyBones.LeftRingProximal},
        {"leftHandRing2", HumanBodyBones.LeftRingIntermediate},
        {"leftHandRing3", HumanBodyBones.LeftRingDistal},
        {"leftHandPinky1", HumanBodyBones.LeftLittleProximal},
        {"leftHandPinky2", HumanBodyBones.LeftLittleIntermediate},
        {"leftHandPinky3", HumanBodyBones.LeftLittleDistal},

        // Right hand fingers
        {"rightHandThumb1", HumanBodyBones.RightThumbProximal},
        {"rightHandThumb2", HumanBodyBones.RightThumbIntermediate},
        {"rightHandThumb3", HumanBodyBones.RightThumbDistal},
        {"rightHandIndex1", HumanBodyBones.RightIndexProximal},
        {"rightHandIndex2", HumanBodyBones.RightIndexIntermediate},
        {"rightHandIndex3", HumanBodyBones.RightIndexDistal},
        {"rightHandMiddle1", HumanBodyBones.RightMiddleProximal},
        {"rightHandMiddle2", HumanBodyBones.RightMiddleIntermediate},
        {"rightHandMiddle3", HumanBodyBones.RightMiddleDistal},
        {"rightHandRing1", HumanBodyBones.RightRingProximal},
        {"rightHandRing2", HumanBodyBones.RightRingIntermediate},
        {"rightHandRing3", HumanBodyBones.RightRingDistal},
        {"rightHandPinky1", HumanBodyBones.RightLittleProximal},
        {"rightHandPinky2", HumanBodyBones.RightLittleIntermediate},
        {"rightHandPinky3", HumanBodyBones.RightLittleDistal},
    };

    // Combined for backward compat
    private static readonly Dictionary<string, HumanBodyBones> HumanoidMapping = CoreMapping;

    private static readonly Dictionary<string, HumanBodyBones> LeftFingerMapping = new()
    {
        {"ThumbMetacarpal", HumanBodyBones.LeftThumbProximal},
        {"ThumbProximal", HumanBodyBones.LeftThumbIntermediate},
        {"ThumbDistal", HumanBodyBones.LeftThumbDistal},
        {"IndexProximal", HumanBodyBones.LeftIndexProximal},
        {"IndexIntermediate", HumanBodyBones.LeftIndexIntermediate},
        {"IndexDistal", HumanBodyBones.LeftIndexDistal},
        {"MiddleProximal", HumanBodyBones.LeftMiddleProximal},
        {"MiddleIntermediate", HumanBodyBones.LeftMiddleIntermediate},
        {"MiddleDistal", HumanBodyBones.LeftMiddleDistal},
        {"RingProximal", HumanBodyBones.LeftRingProximal},
        {"RingIntermediate", HumanBodyBones.LeftRingIntermediate},
        {"RingDistal", HumanBodyBones.LeftRingDistal},
        {"LittleProximal", HumanBodyBones.LeftLittleProximal},
        {"LittleIntermediate", HumanBodyBones.LeftLittleIntermediate},
        {"LittleDistal", HumanBodyBones.LeftLittleDistal},
    };

    private static readonly Dictionary<string, HumanBodyBones> RightFingerMapping = new()
    {
        {"ThumbMetacarpal", HumanBodyBones.RightThumbProximal},
        {"ThumbProximal", HumanBodyBones.RightThumbIntermediate},
        {"ThumbDistal", HumanBodyBones.RightThumbDistal},
        {"IndexProximal", HumanBodyBones.RightIndexProximal},
        {"IndexIntermediate", HumanBodyBones.RightIndexIntermediate},
        {"IndexDistal", HumanBodyBones.RightIndexDistal},
        {"MiddleProximal", HumanBodyBones.RightMiddleProximal},
        {"MiddleIntermediate", HumanBodyBones.RightMiddleIntermediate},
        {"MiddleDistal", HumanBodyBones.RightMiddleDistal},
        {"RingProximal", HumanBodyBones.RightRingProximal},
        {"RingIntermediate", HumanBodyBones.RightRingIntermediate},
        {"RingDistal", HumanBodyBones.RightRingDistal},
        {"LittleProximal", HumanBodyBones.RightLittleProximal},
        {"LittleIntermediate", HumanBodyBones.RightLittleIntermediate},
        {"LittleDistal", HumanBodyBones.RightLittleDistal},
    };

    void Start()
    {
        _avatarDriver = GetComponent<SexKitAvatarDriver>();

        if (SexKitWebSocketClient.Instance != null)
            SexKitWebSocketClient.Instance.OnFrameReceived += OnFrame;

        switch (avatarMode)
        {
            case AvatarSourceMode.MetaAvatar:
                InitializeLocalMetaAvatar();
                break;

            case AvatarSourceMode.DualHumanoid:
                // JOY-on-JOY: both bodies driven by Humanoid animators
                // User (A) = iPhone 91-joint skeleton + Quest head/hands
                // Partner (B) = ControlFrame pipeline via PartnerDirector
                // Hide primitives since we have real models
                if (hidePrimitiveLocalBody && _avatarDriver != null)
                    _avatarDriver.SetPrimitiveVisibility(false, false);
                Debug.Log("[MetaAvatarBridge] Dual Humanoid mode — JOY-on-JOY");
                break;

            case AvatarSourceMode.PrimitiveOnly:
                // Debug mode — just spheres and bones
                Debug.Log("[MetaAvatarBridge] Primitive only mode");
                break;
        }
    }

    private void InitializeLocalMetaAvatar()
    {
        cameraRig ??= FindFirstObjectByType<OVRCameraRig>();
        if (cameraRig == null)
        {
            Debug.LogWarning("[SexKit] Meta Avatar disabled because no OVRCameraRig was found.");
            useMetaAvatars = false;
            return;
        }

        avatarManager = EnsureAvatarManager();
        avatarInputManager = EnsureInputManager();
        avatarA = EnsureLocalAvatar();

        avatarInputManager.SetCameraRig(cameraRig);
        avatarA.SetInputManager(avatarInputManager);

        if (hidePrimitiveLocalBody && _avatarDriver != null)
            _avatarDriver.SetPrimitiveVisibility(false, true);
    }

    private OvrAvatarManager EnsureAvatarManager()
    {
        var manager = avatarManager != null ? avatarManager : FindFirstObjectByType<OvrAvatarManager>();
        if (manager != null)
            return manager;

        var managerObject = new GameObject("OvrAvatarManager");
        manager = managerObject.AddComponent<OvrAvatarManager>();
        manager.automaticallyRequestPermissions = true;
        return manager;
    }

    private SexKitMetaAvatarInputManager EnsureInputManager()
    {
        var inputManager = avatarInputManager != null
            ? avatarInputManager
            : GetComponent<SexKitMetaAvatarInputManager>();
        if (inputManager != null)
            return inputManager;

        return gameObject.AddComponent<SexKitMetaAvatarInputManager>();
    }

    private SampleAvatarEntity EnsureLocalAvatar()
    {
        var entity = avatarA != null ? avatarA : GetComponentInChildren<SampleAvatarEntity>();
        if (entity != null)
            return entity;

        var avatarObject = new GameObject("LocalMetaAvatar");
        avatarObject.transform.SetParent(transform, false);
        entity = avatarObject.AddComponent<SampleAvatarEntity>();
        return entity;
    }

    void OnFrame(LiveFrame frame)
    {
        switch (avatarMode)
        {
            case AvatarSourceMode.MetaAvatar:
                // User = Meta Avatar (Quest tracking + iPhone skeleton supplement)
                // Partner = JOY via PartnerDirector (NOT skeleton-driven)
                if (frame.skeletonA != null && avatarInputManager != null)
                    avatarInputManager.ApplyBodySkeleton(frame.skeletonA);
                break;

            case AvatarSourceMode.DualHumanoid:
                // User = JOY A (iPhone 91-joint skeleton + Quest head/hands)
                // Partner = JOY B (skeleton if available, PartnerDirector handles ControlFrames)
                if (frame.skeletonA != null && humanoidAnimatorA != null)
                {
                    ApplySkeletonToAnimator(frame.skeletonA, humanoidAnimatorA);
                    if (questTracking != null)
                    {
                        ApplyQuestHead(humanoidAnimatorA);
                        ApplyQuestHands(humanoidAnimatorA);
                    }
                }
                // Partner body skeleton (if real partner data, not agent-controlled)
                if (frame.skeletonB != null && humanoidAnimatorB != null && !frame.partnerIsInferred)
                    ApplySkeletonToAnimator(frame.skeletonB, humanoidAnimatorB);
                break;

            case AvatarSourceMode.PrimitiveOnly:
                // Handled by SexKitAvatarDriver directly
                break;
        }
    }

    private void ApplyToHumanoid(LiveFrame frame)
    {
        if (frame.skeletonA != null && humanoidAnimatorA != null)
        {
            ApplySkeletonToAnimator(frame.skeletonA, humanoidAnimatorA);
            if (questTracking != null)
            {
                ApplyQuestHead(humanoidAnimatorA);
                ApplyQuestHands(humanoidAnimatorA);
            }
        }

        if (frame.skeletonB != null && humanoidAnimatorB != null)
            ApplySkeletonToAnimator(frame.skeletonB, humanoidAnimatorB);
    }

    private void ApplySkeletonToAnimator(SkeletonData skeleton, Animator animator)
    {
        // Always apply core 16 joints
        ApplyMapping(skeleton, animator, CoreMapping);

        // Apply extended joints when ARKit data is available (Tier 1, 91 joints)
        if (skeleton.tier <= 1 && skeleton.jointCount > 16)
        {
            ApplyMapping(skeleton, animator, ExtendedMapping);
        }
    }

    private void ApplyMapping(SkeletonData skeleton, Animator animator, Dictionary<string, HumanBodyBones> mapping)
    {
        foreach (var entry in mapping)
        {
            Vector3 pos = skeleton.GetJoint(entry.Key);
            if (pos == Vector3.zero) continue;

            Transform bone = animator.GetBoneTransform(entry.Value);
            if (bone != null)
                bone.position = Vector3.Lerp(bone.position, pos, Time.deltaTime * 4f);
        }
    }

    private void ApplyQuestHead(Animator animator)
    {
        if (questTracking == null) return;

        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head != null)
        {
            head.position = Vector3.Lerp(head.position, questTracking.HeadPosition, Time.deltaTime * 8f);
            head.rotation = Quaternion.Slerp(head.rotation, questTracking.HeadRotation, Time.deltaTime * 8f);
        }
    }

    private void ApplyQuestHands(Animator animator)
    {
        if (questTracking == null) return;

        if (questTracking.LeftHandJoints.Count > 0)
            ApplyFingers(animator, questTracking.LeftHandJoints, LeftFingerMapping);

        if (questTracking.RightHandJoints.Count > 0)
            ApplyFingers(animator, questTracking.RightHandJoints, RightFingerMapping);
    }

    private void ApplyFingers(Animator animator, Dictionary<string, Vector3> handJoints, Dictionary<string, HumanBodyBones> fingerMap)
    {
        foreach (var mapping in fingerMap)
        {
            foreach (var questJoint in handJoints)
            {
                if (!questJoint.Key.Contains(mapping.Key))
                    continue;

                Transform bone = animator.GetBoneTransform(mapping.Value);
                if (bone != null)
                    bone.position = Vector3.Lerp(bone.position, questJoint.Value, Time.deltaTime * 6f);
                break;
            }
        }
    }

    void OnDestroy()
    {
        if (SexKitWebSocketClient.Instance != null)
            SexKitWebSocketClient.Instance.OnFrameReceived -= OnFrame;
    }
}
