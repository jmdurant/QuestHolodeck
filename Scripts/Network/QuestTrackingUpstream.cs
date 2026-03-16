// QuestTrackingUpstream.cs
// SexKit Quest App
//
// Sends Quest's own tracking data BACK to the iPhone server
// Two-way WebSocket: iPhone sends LiveFrame down, Quest sends tracking up
// Server merges Quest data with Vision data for better skeleton accuracy
//
// Quest adds: head (90fps), hands (60fps), body tracking (Movement SDK)
// iPhone has: Vision skeleton (30fps) from side angle
// Combined: two angles = more complete skeleton for everyone

using UnityEngine;
using System.Collections.Generic;

public class QuestTrackingUpstream : MonoBehaviour
{
    [Header("References")]
    public QuestTrackingMerge tracking;
    public SexKitWebSocketClient wsClient;

    [Header("Settings")]
    public bool sendUpstream = true;
    public float sendRate = 30f;  // match iPhone's 30fps

    private float _lastSendTime;

    void Update()
    {
        if (!sendUpstream || tracking == null || wsClient == null) return;
        if (!wsClient.isConnected) return;
        if (Time.time - _lastSendTime < 1f / sendRate) return;

        _lastSendTime = Time.time;
        SendTrackingFrame();
    }

    void SendTrackingFrame()
    {
        var frame = new QuestTrackingFrame();

        // Head (90fps on Quest, sending at 30fps)
        frame.headPosX = tracking.HeadPosition.x;
        frame.headPosY = tracking.HeadPosition.y;
        frame.headPosZ = tracking.HeadPosition.z;
        frame.headRotX = tracking.HeadRotation.eulerAngles.x;
        frame.headRotY = tracking.HeadRotation.eulerAngles.y;
        frame.headRotZ = tracking.HeadRotation.eulerAngles.z;

        // Gaze (Quest Pro only)
        frame.gazeX = tracking.GazeDirection.x;
        frame.gazeY = tracking.GazeDirection.y;
        frame.gazeZ = tracking.GazeDirection.z;
        frame.isLooking = tracking.IsUserLooking;

        // Hands (OpenXR XR Hands — 26 joints per hand)
        frame.leftHandJoints = SerializeHandJoints(tracking.LeftHandJoints);
        frame.rightHandJoints = SerializeHandJoints(tracking.RightHandJoints);
        frame.leftHandTracked = tracking.LeftHandJoints.Count > 0;
        frame.rightHandTracked = tracking.RightHandJoints.Count > 0;

        frame.timestamp = Time.realtimeSinceStartupAsDouble;

        string json = JsonUtility.ToJson(frame);
        wsClient.SendCommand(json);
    }

    string SerializeHandJoints(Dictionary<string, Vector3> joints)
    {
        if (joints.Count == 0) return "";

        // Compact format: "jointName:x,y,z|jointName:x,y,z|..."
        var sb = new System.Text.StringBuilder();
        foreach (var kvp in joints)
        {
            if (sb.Length > 0) sb.Append('|');
            sb.Append($"{kvp.Key}:{kvp.Value.x:F3},{kvp.Value.y:F3},{kvp.Value.z:F3}");
        }
        return sb.ToString();
    }
}

// Data sent upstream from Quest to iPhone
[System.Serializable]
public class QuestTrackingFrame
{
    public double timestamp;

    // Head 6DOF
    public float headPosX, headPosY, headPosZ;
    public float headRotX, headRotY, headRotZ;

    // Gaze (Quest Pro only)
    public float gazeX, gazeY, gazeZ;
    public bool isLooking;

    // Hands (serialized as compact strings)
    public string leftHandJoints;
    public string rightHandJoints;
    public bool leftHandTracked;
    public bool rightHandTracked;
}
