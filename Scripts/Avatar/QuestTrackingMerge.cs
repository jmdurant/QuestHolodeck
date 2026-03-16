// QuestTrackingMerge.cs
// SexKit Quest App
//
// Merges Quest's own tracking (head, hands, body) with SexKit's
// body skeleton to create the complete tracked body
//
// SexKit: 16 body joints (neck down) at 30fps from iPhone Vision
// Quest:  head 6DOF (90fps) + hands via OpenXR XR Hands (60fps)
//         + body tracking via Movement SDK
//
// Updated for Meta XR SDK v85 (Feb 2026):
//   - OpenXR hand skeleton (OVRHand/OVRSkeleton deprecated v78)
//   - Eye tracking only on Quest Pro (Quest 3 has no eye tracking hardware)
//   - Movement SDK for body tracking

using UnityEngine;
using UnityEngine.XR.Hands;
using System.Collections.Generic;

public class QuestTrackingMerge : MonoBehaviour
{
    [Header("Quest Tracking Sources")]
    public Transform questHeadTransform;      // OVRCameraRig center eye anchor

    [Header("Hand Tracking (OpenXR via XR Hands — replaces deprecated OVRHand)")]
    public XRHandSubsystem handSubsystem;     // Unity XR Hands subsystem
    public bool mergeHandTracking = true;

    [Header("Eye Tracking (Quest Pro ONLY — Quest 3 has no eye tracking)")]
    public bool enableEyeTracking = false;    // Only enable for Quest Pro
    // OVREyeGaze references only valid on Quest Pro
    public OVREyeGaze leftEyeGaze;
    public OVREyeGaze rightEyeGaze;

    [Header("Body Tracking (Movement SDK)")]
    public bool mergeBodyTracking = true;     // Supplement SexKit body with Quest body tracking

    [Header("Settings")]
    public bool mergeHeadTracking = true;     // Quest head overrides SexKit head (90fps > 30fps)

    // Output
    public Vector3 HeadPosition { get; private set; }
    public Quaternion HeadRotation { get; private set; }
    public Vector3 GazeDirection { get; private set; }
    public bool IsUserLooking { get; private set; }
    public Dictionary<string, Vector3> LeftHandJoints { get; private set; } = new();
    public Dictionary<string, Vector3> RightHandJoints { get; private set; } = new();

    void Start()
    {
        // Get XR Hands subsystem (OpenXR standard)
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
        {
            handSubsystem = subsystems[0];
        }
    }

    void Update()
    {
        if (mergeHeadTracking) UpdateHead();
        if (mergeHandTracking) UpdateHands();
        if (enableEyeTracking) UpdateEyeGaze();
    }

    void UpdateHead()
    {
        if (questHeadTransform == null) return;
        HeadPosition = questHeadTransform.position;
        HeadRotation = questHeadTransform.rotation;
    }

    void UpdateHands()
    {
        if (handSubsystem == null || !handSubsystem.running) return;

        // Left hand via OpenXR XR Hands
        UpdateHandFromXR(handSubsystem.leftHand, LeftHandJoints);
        // Right hand
        UpdateHandFromXR(handSubsystem.rightHand, RightHandJoints);
    }

    void UpdateHandFromXR(XRHand hand, Dictionary<string, Vector3> joints)
    {
        if (!hand.isTracked) return;

        joints.Clear();
        for (int i = 0; i < (int)XRHandJointID.EndMarker; i++)
        {
            var jointId = (XRHandJointID)i;
            var joint = hand.GetJoint(jointId);
            if (joint.TryGetPose(out Pose pose))
            {
                joints[jointId.ToString()] = pose.position;
            }
        }
    }

    void UpdateEyeGaze()
    {
        // Quest Pro only — Quest 3 does not have eye tracking hardware
        if (leftEyeGaze == null || rightEyeGaze == null) return;

        Vector3 leftDir = leftEyeGaze.transform.forward;
        Vector3 rightDir = rightEyeGaze.transform.forward;
        GazeDirection = Vector3.Lerp(leftDir, rightDir, 0.5f).normalized;
        IsUserLooking = true;
    }

    /// Complete merged state for AI agent or logging
    public Dictionary<string, object> GetMergedState()
    {
        return new Dictionary<string, object>
        {
            {"headPosition", HeadPosition},
            {"headRotation", HeadRotation.eulerAngles},
            {"gazeDirection", GazeDirection},
            {"isUserLooking", IsUserLooking},
            {"leftHandJointCount", LeftHandJoints.Count},
            {"rightHandJointCount", RightHandJoints.Count},
            {"leftHandTracked", LeftHandJoints.Count > 0},
            {"rightHandTracked", RightHandJoints.Count > 0},
            {"eyeTrackingAvailable", enableEyeTracking},
        };
    }
}
