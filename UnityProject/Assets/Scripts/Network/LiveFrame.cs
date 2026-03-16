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
    // 16 joints, each with [x, y, z]
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

    public Vector3 GetJoint(string name)
    {
        float[] data = name switch
        {
            "head" => head, "neck" => neck,
            "leftShoulder" => leftShoulder, "rightShoulder" => rightShoulder,
            "leftElbow" => leftElbow, "rightElbow" => rightElbow,
            "leftWrist" => leftWrist, "rightWrist" => rightWrist,
            "spine" => spine, "hip" => hip,
            "leftHip" => leftHip, "rightHip" => rightHip,
            "leftKnee" => leftKnee, "rightKnee" => rightKnee,
            "leftAnkle" => leftAnkle, "rightAnkle" => rightAnkle,
            _ => null
        };

        if (data == null || data.Length < 3) return Vector3.zero;
        return new Vector3(data[0], data[1], data[2]);
    }

    public static readonly string[] JointNames = {
        "head", "neck", "leftShoulder", "rightShoulder",
        "leftElbow", "rightElbow", "leftWrist", "rightWrist",
        "spine", "hip", "leftHip", "rightHip",
        "leftKnee", "rightKnee", "leftAnkle", "rightAnkle"
    };
}
