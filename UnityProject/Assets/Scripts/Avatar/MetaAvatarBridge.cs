using System.Collections.Generic;
using Oculus.Avatar2;
using UnityEngine;

public class MetaAvatarBridge : MonoBehaviour
{
    [Header("Meta Avatar")]
    public bool useMetaAvatars = true;
    public bool hidePrimitiveLocalBody = true;
    public OVRCameraRig cameraRig;
    public OvrAvatarManager avatarManager;
    public SexKitMetaAvatarInputManager avatarInputManager;
    public SampleAvatarEntity avatarA;

    [Header("Humanoid Fallback")]
    public Animator humanoidAnimatorA;
    public Animator humanoidAnimatorB;

    [Header("Quest Tracking Merge")]
    public QuestTrackingMerge questTracking;

    private SexKitAvatarDriver _avatarDriver;

    private static readonly Dictionary<string, HumanBodyBones> HumanoidMapping = new()
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

        if (useMetaAvatars)
            InitializeLocalMetaAvatar();
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
        if (useMetaAvatars)
        {
            ApplyPartnerFallback(frame);
            return;
        }

        if (humanoidAnimatorA != null || humanoidAnimatorB != null)
            ApplyToHumanoid(frame);
    }

    private void ApplyPartnerFallback(LiveFrame frame)
    {
        if (frame.skeletonB != null && humanoidAnimatorB != null)
            ApplySkeletonToAnimator(frame.skeletonB, humanoidAnimatorB);
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
        foreach (var mapping in HumanoidMapping)
        {
            Vector3 pos = skeleton.GetJoint(mapping.Key);
            if (pos == Vector3.zero) continue;

            Transform bone = animator.GetBoneTransform(mapping.Value);
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
