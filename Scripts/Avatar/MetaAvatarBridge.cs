// MetaAvatarBridge.cs
// SexKit Quest App
//
// Bridges SexKit LiveFrame data to Meta Avatars SDK
// Drives Meta Avatars using joint overrides from WebSocket skeleton data
//
// Requires: Meta Avatars SDK (com.meta.xr.sdk.avatars)
//           Meta XR Core SDK (com.meta.xr.sdk.core)

using UnityEngine;
using System.Collections.Generic;
// Uncomment when Meta Avatars SDK is imported:
// using Oculus.Avatar2;

public class MetaAvatarBridge : MonoBehaviour
{
    [Header("Avatar References")]
    // Uncomment when Meta Avatars SDK is imported:
    // public OvrAvatarEntity avatarA;  // User's avatar
    // public OvrAvatarEntity avatarB;  // Partner's avatar

    [Header("Settings")]
    public bool useMetaAvatars = true;
    public bool mirrorUserToAvatar = true;

    // SexKit joint name → Meta Avatar joint mapping
    private static readonly Dictionary<string, string> JointMapping = new()
    {
        {"head", "Head"},
        {"neck", "Neck"},
        {"leftShoulder", "LeftShoulder"},
        {"rightShoulder", "RightShoulder"},
        {"leftElbow", "LeftForeArm"},
        {"rightElbow", "RightForeArm"},
        {"leftWrist", "LeftHand"},
        {"rightWrist", "RightHand"},
        {"spine", "Chest"},
        {"hip", "Hips"},
        {"leftHip", "LeftUpLeg"},
        {"rightHip", "RightUpLeg"},
        {"leftKnee", "LeftLeg"},
        {"rightKnee", "RightLeg"},
        {"leftAnkle", "LeftFoot"},
        {"rightAnkle", "RightFoot"},
    };

    // SexKit joint name → Unity HumanBodyBones (for Humanoid rig fallback)
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

    [Header("Humanoid Fallback")]
    public Animator humanoidAnimatorA;
    public Animator humanoidAnimatorB;

    [Header("Quest Tracking Merge")]
    public QuestTrackingMerge questTracking;

    // OpenXR hand joint → Unity HumanBodyBones finger mapping
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
        SexKitWebSocketClient.Instance.OnFrameReceived += OnFrame;
    }

    void OnFrame(LiveFrame frame)
    {
        if (useMetaAvatars)
        {
            ApplyToMetaAvatar(frame);
        }
        else if (humanoidAnimatorA != null)
        {
            ApplyToHumanoid(frame);
        }
    }

    // MARK: - Meta Avatars SDK Integration

    private void ApplyToMetaAvatar(LiveFrame frame)
    {
        // STUB: Meta Avatars SDK not yet imported.
        // When com.meta.xr.sdk.avatars is added to the project:
        //
        // 1. Uncomment OvrAvatarEntity fields at top of class
        // 2. Implement joint override from LiveFrame skeleton data
        // 3. The joint mapping table above (JointMapping) is ready
        //
        // For now, fall back to Humanoid mode which works immediately.

        if (humanoidAnimatorA != null)
        {
            ApplyToHumanoid(frame);
            return;
        }

        Debug.LogWarning("[SexKit] Meta Avatars SDK not imported — falling back to Humanoid. Import com.meta.xr.sdk.avatars to enable.");
    }

    // MARK: - Unity Humanoid Fallback

    private void ApplyToHumanoid(LiveFrame frame)
    {
        if (frame.skeletonA != null && humanoidAnimatorA != null)
        {
            ApplySkeletonToAnimator(frame.skeletonA, humanoidAnimatorA);
            // Merge Quest tracking on top (higher fidelity for head + hands)
            if (questTracking != null)
            {
                ApplyQuestHead(humanoidAnimatorA);
                ApplyQuestHands(humanoidAnimatorA);
            }
        }

        if (frame.skeletonB != null && humanoidAnimatorB != null)
        {
            ApplySkeletonToAnimator(frame.skeletonB, humanoidAnimatorB);
        }
    }

    private void ApplySkeletonToAnimator(SkeletonData skeleton, Animator animator)
    {
        // SexKit body joints (16 joints from iPhone Vision)
        foreach (var mapping in HumanoidMapping)
        {
            Vector3 pos = skeleton.GetJoint(mapping.Key);
            if (pos == Vector3.zero) continue;

            Transform bone = animator.GetBoneTransform(mapping.Value);
            if (bone != null)
            {
                bone.position = Vector3.Lerp(bone.position, pos, Time.deltaTime * 4f);
            }
        }
    }

    private void ApplyQuestHead(Animator animator)
    {
        if (questTracking == null) return;

        // Quest head tracking at 90fps overrides SexKit's 30fps head
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

        // Left hand fingers (26 joints from Quest → 15 Humanoid finger bones)
        if (questTracking.LeftHandJoints.Count > 0)
        {
            ApplyFingers(animator, questTracking.LeftHandJoints, LeftFingerMapping);
        }

        // Right hand fingers
        if (questTracking.RightHandJoints.Count > 0)
        {
            ApplyFingers(animator, questTracking.RightHandJoints, RightFingerMapping);
        }
    }

    private void ApplyFingers(Animator animator, Dictionary<string, Vector3> handJoints, Dictionary<string, HumanBodyBones> fingerMap)
    {
        foreach (var mapping in fingerMap)
        {
            // Find matching Quest joint (OpenXR names contain the finger bone name)
            foreach (var questJoint in handJoints)
            {
                if (questJoint.Key.Contains(mapping.Key))
                {
                    Transform bone = animator.GetBoneTransform(mapping.Value);
                    if (bone != null)
                    {
                        bone.position = Vector3.Lerp(bone.position, questJoint.Value, Time.deltaTime * 6f);
                    }
                    break;
                }
            }
        }
    }

    void OnDestroy()
    {
        if (SexKitWebSocketClient.Instance != null)
            SexKitWebSocketClient.Instance.OnFrameReceived -= OnFrame;
    }
}
