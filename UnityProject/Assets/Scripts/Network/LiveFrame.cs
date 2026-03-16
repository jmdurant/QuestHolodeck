// LiveFrame.cs
// SexKit Quest App
//
// Data model matching SexKit's LiveFrame JSON schema
// Deserialized from WebSocket messages

using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LiveFrame
{
    // Timing
    public double timestamp;
    public double sessionElapsed;

    // Biometrics
    public int heartRate;
    public int partnerHeartRate;
    public int localCalories;

    // Accelerometer
    public double localAccelX, localAccelY, localAccelZ;
    public double partnerAccelX, partnerAccelY, partnerAccelZ;

    // Gravity
    public float localGravX, localGravY, localGravZ;
    public float partnerGravX, partnerGravY, partnerGravZ;

    // Intensity
    public double localIntensity;
    public double partnerIntensity;
    public double rhythmHz;

    // Position
    public string detectedPosition;
    public double positionConfidence;

    // Skeleton joints (serialized as flat arrays for JsonUtility compatibility)
    public SkeletonData skeletonA;
    public SkeletonData skeletonB;

    // UWB
    public float wristDistance;
    public float[] bodyAPosition;
    public float[] bodyBPosition;
    public float distancePhoneAToWatchA;
    public float distancePhoneAToWatchB;
    public float distancePhoneBToWatchA;
    public float distancePhoneBToWatchB;
    public float distancePhoneToPhone;

    // Watch metadata
    public string localWristSide;
    public string partnerWristSide;
    public string userGender;

    // Bed calibration
    public string bedSize;
    public float bedWidth;
    public float bedLength;
    public string userSleepSide;
    public string phoneSide;
    public float phoneHeight;
    public float mattressHeight;
    public bool isOnBed;
    public float heightAboveMattress;

    // Context
    public bool isPaused;
    public bool isSolo;
    public string currentPlanStep;
    public int planStepTimeRemaining;
    public bool partnerIsInferred;
    public int dataSourceTier;

    // Biometric pacing
    public string pacingPhase;             // Warmup, Building, Plateau, Edge, Release, Resolution
    public double pacingTargetRhythm;
    public double pacingTargetIntensity;
    public int edgeCount;

    // Verbal cue (spatial audio)
    public string verbalCueText;
    public double verbalCueUrgency;

    // Biometrics (extended)
    public double heartRateVariability;
    public double respiratoryRate;
}

[Serializable]
public class SkeletonData
{
    // Tier metadata
    public int tier;            // 1=ARKit(91), 2=Vision(19), 3-4=estimated(16)
    public int jointCount;

    // ── Core 16 joints (always present at all tiers) ──
    public float[] head;
    public float[] neck;
    public float[] leftShoulder;
    public float[] rightShoulder;
    public float[] leftElbow;
    public float[] rightElbow;
    public float[] leftWrist;
    public float[] rightWrist;
    public float[] spine;
    public float[] hip;
    public float[] leftHip;
    public float[] rightHip;
    public float[] leftKnee;
    public float[] rightKnee;
    public float[] leftAnkle;
    public float[] rightAnkle;

    // ── Tier 2+ (Vision 19 joints adds these 3) ──
    public float[] nose;
    public float[] leftEye;
    public float[] rightEye;

    // ── Tier 1 (ARKit 91 joints — full skeleton) ──

    // Face
    public float[] jaw;
    public float[] chin;
    public float[] leftEar;
    public float[] rightEar;

    // Full spine (7 segments)
    public float[] spine1;
    public float[] spine2;
    public float[] spine3;
    public float[] spine4;
    public float[] spine5;
    public float[] spine6;
    public float[] spine7;

    // Upper/lower arms
    public float[] leftUpperArm;
    public float[] rightUpperArm;
    public float[] leftForearm;
    public float[] rightForearm;

    // Left hand fingers (15 joints)
    public float[] leftHandThumb1;
    public float[] leftHandThumb2;
    public float[] leftHandThumb3;
    public float[] leftHandIndex1;
    public float[] leftHandIndex2;
    public float[] leftHandIndex3;
    public float[] leftHandMiddle1;
    public float[] leftHandMiddle2;
    public float[] leftHandMiddle3;
    public float[] leftHandRing1;
    public float[] leftHandRing2;
    public float[] leftHandRing3;
    public float[] leftHandPinky1;
    public float[] leftHandPinky2;
    public float[] leftHandPinky3;

    // Right hand fingers (15 joints)
    public float[] rightHandThumb1;
    public float[] rightHandThumb2;
    public float[] rightHandThumb3;
    public float[] rightHandIndex1;
    public float[] rightHandIndex2;
    public float[] rightHandIndex3;
    public float[] rightHandMiddle1;
    public float[] rightHandMiddle2;
    public float[] rightHandMiddle3;
    public float[] rightHandRing1;
    public float[] rightHandRing2;
    public float[] rightHandRing3;
    public float[] rightHandPinky1;
    public float[] rightHandPinky2;
    public float[] rightHandPinky3;

    // Upper/lower legs
    public float[] leftUpperLeg;
    public float[] rightUpperLeg;
    public float[] leftLowerLeg;
    public float[] rightLowerLeg;

    // Feet
    public float[] leftFoot;
    public float[] rightFoot;
    public float[] leftToes;
    public float[] rightToes;

    // Identity tracking (for multi-person UWB matching)
    public string identifiedBy;       // "uwb_wrist", "gravity_match", etc.
    public float identityConfidence;   // 0-1

    public Vector3 GetJoint(string name)
    {
        float[] data = name switch
        {
            // Core 16
            "head" => head, "neck" => neck,
            "leftShoulder" => leftShoulder, "rightShoulder" => rightShoulder,
            "leftElbow" => leftElbow, "rightElbow" => rightElbow,
            "leftWrist" => leftWrist, "rightWrist" => rightWrist,
            "spine" => spine, "hip" => hip,
            "leftHip" => leftHip, "rightHip" => rightHip,
            "leftKnee" => leftKnee, "rightKnee" => rightKnee,
            "leftAnkle" => leftAnkle, "rightAnkle" => rightAnkle,

            // Vision extras
            "nose" => nose, "leftEye" => leftEye, "rightEye" => rightEye,

            // Face
            "jaw" => jaw, "chin" => chin,
            "leftEar" => leftEar, "rightEar" => rightEar,

            // Full spine
            "spine1" => spine1, "spine2" => spine2, "spine3" => spine3,
            "spine4" => spine4, "spine5" => spine5, "spine6" => spine6, "spine7" => spine7,

            // Arms
            "leftUpperArm" => leftUpperArm, "rightUpperArm" => rightUpperArm,
            "leftForearm" => leftForearm, "rightForearm" => rightForearm,

            // Left fingers
            "leftHandThumb1" => leftHandThumb1, "leftHandThumb2" => leftHandThumb2, "leftHandThumb3" => leftHandThumb3,
            "leftHandIndex1" => leftHandIndex1, "leftHandIndex2" => leftHandIndex2, "leftHandIndex3" => leftHandIndex3,
            "leftHandMiddle1" => leftHandMiddle1, "leftHandMiddle2" => leftHandMiddle2, "leftHandMiddle3" => leftHandMiddle3,
            "leftHandRing1" => leftHandRing1, "leftHandRing2" => leftHandRing2, "leftHandRing3" => leftHandRing3,
            "leftHandPinky1" => leftHandPinky1, "leftHandPinky2" => leftHandPinky2, "leftHandPinky3" => leftHandPinky3,

            // Right fingers
            "rightHandThumb1" => rightHandThumb1, "rightHandThumb2" => rightHandThumb2, "rightHandThumb3" => rightHandThumb3,
            "rightHandIndex1" => rightHandIndex1, "rightHandIndex2" => rightHandIndex2, "rightHandIndex3" => rightHandIndex3,
            "rightHandMiddle1" => rightHandMiddle1, "rightHandMiddle2" => rightHandMiddle2, "rightHandMiddle3" => rightHandMiddle3,
            "rightHandRing1" => rightHandRing1, "rightHandRing2" => rightHandRing2, "rightHandRing3" => rightHandRing3,
            "rightHandPinky1" => rightHandPinky1, "rightHandPinky2" => rightHandPinky2, "rightHandPinky3" => rightHandPinky3,

            // Legs
            "leftUpperLeg" => leftUpperLeg, "rightUpperLeg" => rightUpperLeg,
            "leftLowerLeg" => leftLowerLeg, "rightLowerLeg" => rightLowerLeg,

            // Feet
            "leftFoot" => leftFoot, "rightFoot" => rightFoot,
            "leftToes" => leftToes, "rightToes" => rightToes,

            _ => null
        };

        if (data == null || data.Length < 3) return Vector3.zero;
        return new Vector3(data[0], data[1], data[2]);
    }

    /// Core 16 joints — always present at all tiers. Use for basic rendering.
    public static readonly string[] CoreJointNames = {
        "head", "neck", "leftShoulder", "rightShoulder",
        "leftElbow", "rightElbow", "leftWrist", "rightWrist",
        "spine", "hip", "leftHip", "rightHip",
        "leftKnee", "rightKnee", "leftAnkle", "rightAnkle"
    };

    /// All joint names — use tier/jointCount to know which are populated.
    public static readonly string[] AllJointNames = {
        // Core 16
        "head", "neck", "leftShoulder", "rightShoulder",
        "leftElbow", "rightElbow", "leftWrist", "rightWrist",
        "spine", "hip", "leftHip", "rightHip",
        "leftKnee", "rightKnee", "leftAnkle", "rightAnkle",
        // Vision extras
        "nose", "leftEye", "rightEye",
        // Face
        "jaw", "chin", "leftEar", "rightEar",
        // Full spine
        "spine1", "spine2", "spine3", "spine4", "spine5", "spine6", "spine7",
        // Arms
        "leftUpperArm", "rightUpperArm", "leftForearm", "rightForearm",
        // Left fingers
        "leftHandThumb1", "leftHandThumb2", "leftHandThumb3",
        "leftHandIndex1", "leftHandIndex2", "leftHandIndex3",
        "leftHandMiddle1", "leftHandMiddle2", "leftHandMiddle3",
        "leftHandRing1", "leftHandRing2", "leftHandRing3",
        "leftHandPinky1", "leftHandPinky2", "leftHandPinky3",
        // Right fingers
        "rightHandThumb1", "rightHandThumb2", "rightHandThumb3",
        "rightHandIndex1", "rightHandIndex2", "rightHandIndex3",
        "rightHandMiddle1", "rightHandMiddle2", "rightHandMiddle3",
        "rightHandRing1", "rightHandRing2", "rightHandRing3",
        "rightHandPinky1", "rightHandPinky2", "rightHandPinky3",
        // Legs
        "leftUpperLeg", "rightUpperLeg", "leftLowerLeg", "rightLowerLeg",
        // Feet
        "leftFoot", "rightFoot", "leftToes", "rightToes"
    };

    // Backward compat — old code references JointNames
    public static string[] JointNames => CoreJointNames;
}
