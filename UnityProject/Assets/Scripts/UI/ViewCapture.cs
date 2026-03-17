// ViewCapture.cs
// SexKit Quest App
//
// Captures rendered views from Quest and sends upstream to iPhone.
// Multiple camera angles: user POV, partner POV, overhead debug.
// iPhone stores latest frame for MCP resource body://view/*
//
// Agent can literally see through JOY's eyes or the user's perspective.

using System;
using UnityEngine;

public class ViewCapture : MonoBehaviour
{
    public static ViewCapture Instance { get; private set; }

    [Header("References")]
    public SexKitWebSocketClient wsClient;
    public QuestTrackingMerge trackingMerge;
    public SexKitAvatarDriver avatarDriver;

    [Header("Capture Settings")]
    public int captureWidth = 640;
    public int captureHeight = 480;
    public int jpegQuality = 60;            // 0-100, lower = smaller
    public float captureInterval = 2.0f;    // seconds between auto-captures
    public bool autoCapture = false;

    [Header("Cameras")]
    public Camera userPOVCamera;            // assign or auto-created from Quest head
    public Camera partnerPOVCamera;         // auto-created at JOY's head
    public Camera overheadCamera;           // auto-created above the bed

    [Header("Status")]
    public string lastCaptureView = "";
    public int capturesSent = 0;

    private RenderTexture _renderTexture;
    private Texture2D _readbackTexture;
    private float _lastCaptureTime;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        wsClient ??= SexKitWebSocketClient.Instance;
        trackingMerge ??= FindFirstObjectByType<QuestTrackingMerge>();
        avatarDriver ??= FindFirstObjectByType<SexKitAvatarDriver>();

        _renderTexture = new RenderTexture(captureWidth, captureHeight, 24);
        _readbackTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);

        SetupCameras();
    }

    void Update()
    {
        if (autoCapture && Time.time - _lastCaptureTime >= captureInterval)
        {
            CaptureAndSend("user_pov");
            _lastCaptureTime = Time.time;
        }
    }

    // MARK: - Public API

    /// Capture a specific view and send upstream
    public void CaptureAndSend(string viewName)
    {
        Camera cam = GetCamera(viewName);
        if (cam == null)
        {
            Debug.LogWarning($"[ViewCapture] No camera for view: {viewName}");
            return;
        }

        byte[] jpeg = CaptureCamera(cam);
        if (jpeg != null && jpeg.Length > 0)
        {
            SendUpstream(viewName, jpeg);
            lastCaptureView = viewName;
            capturesSent++;
        }
    }

    /// Capture and return as base64 (for local use)
    public string CaptureAsBase64(string viewName)
    {
        Camera cam = GetCamera(viewName);
        if (cam == null) return null;

        byte[] jpeg = CaptureCamera(cam);
        return jpeg != null ? Convert.ToBase64String(jpeg) : null;
    }

    // MARK: - Camera Setup

    private void SetupCameras()
    {
        // User POV — uses Quest's main camera
        if (userPOVCamera == null)
        {
            userPOVCamera = Camera.main;
        }

        // Partner POV — camera at JOY's head looking at user
        if (partnerPOVCamera == null)
        {
            var partnerCamObj = new GameObject("PartnerPOVCamera");
            partnerPOVCamera = partnerCamObj.AddComponent<Camera>();
            partnerPOVCamera.enabled = false;  // only renders on demand
            partnerPOVCamera.fieldOfView = 70;
        }

        // Overhead — camera above the bed looking down
        if (overheadCamera == null)
        {
            var overheadCamObj = new GameObject("OverheadCamera");
            overheadCamera = overheadCamObj.AddComponent<Camera>();
            overheadCamera.enabled = false;
            overheadCamera.fieldOfView = 90;

            // Position above bed if available
            if (avatarDriver != null && avatarDriver.bedTransform != null)
            {
                overheadCamObj.transform.position = avatarDriver.bedTransform.position + Vector3.up * 3f;
                overheadCamObj.transform.rotation = Quaternion.Euler(90, 0, 0);  // look straight down
            }
            else
            {
                overheadCamObj.transform.position = new Vector3(0, 3, 0);
                overheadCamObj.transform.rotation = Quaternion.Euler(90, 0, 0);
            }
        }
    }

    private Camera GetCamera(string viewName)
    {
        // Update partner POV position to JOY's head
        if (viewName == "partner_pov" && partnerPOVCamera != null)
        {
            UpdatePartnerCamera();
        }

        // Update overhead position
        if (viewName == "overhead" && overheadCamera != null)
        {
            UpdateOverheadCamera();
        }

        return viewName switch
        {
            "user_pov" => userPOVCamera,
            "partner_pov" => partnerPOVCamera,
            "overhead" => overheadCamera,
            _ => userPOVCamera
        };
    }

    private void UpdatePartnerCamera()
    {
        // Position at JOY's head, looking at user
        var partnerDirector = FindFirstObjectByType<PartnerDirector>();
        if (partnerDirector != null && partnerDirector.bodyController != null
            && partnerDirector.bodyController.headBone != null)
        {
            partnerPOVCamera.transform.position = partnerDirector.bodyController.headBone.position;

            // Look at user's head
            if (trackingMerge != null && trackingMerge.HeadPosition != Vector3.zero)
            {
                partnerPOVCamera.transform.LookAt(trackingMerge.HeadPosition);
            }
        }
    }

    private void UpdateOverheadCamera()
    {
        if (avatarDriver != null && avatarDriver.bedTransform != null)
        {
            overheadCamera.transform.position = avatarDriver.bedTransform.position + Vector3.up * 3f;
        }
    }

    // MARK: - Render + Encode

    private byte[] CaptureCamera(Camera cam)
    {
        if (cam == null) return null;

        var previousRT = cam.targetTexture;
        cam.targetTexture = _renderTexture;
        cam.Render();
        cam.targetTexture = previousRT;

        RenderTexture.active = _renderTexture;
        _readbackTexture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        _readbackTexture.Apply();
        RenderTexture.active = null;

        return _readbackTexture.EncodeToJPG(jpegQuality);
    }

    // MARK: - Send Upstream

    private void SendUpstream(string viewName, byte[] jpeg)
    {
        if (wsClient == null || !wsClient.isConnected) return;

        var frame = new ViewCaptureFrame
        {
            type = "view_capture",
            view = viewName,
            width = captureWidth,
            height = captureHeight,
            jpegBase64 = Convert.ToBase64String(jpeg),
            timestamp = Time.realtimeSinceStartupAsDouble
        };

        string json = JsonUtility.ToJson(frame);
        wsClient.SendCommand(json);

        Debug.Log($"[ViewCapture] Sent {viewName}: {jpeg.Length / 1024}KB");
    }

    void OnDestroy()
    {
        if (_renderTexture != null) Destroy(_renderTexture);
        if (_readbackTexture != null) Destroy(_readbackTexture);
    }
}

[System.Serializable]
public class ViewCaptureFrame
{
    public string type;       // always "view_capture"
    public string view;       // "user_pov", "partner_pov", "overhead"
    public int width;
    public int height;
    public string jpegBase64; // base64 encoded JPEG
    public double timestamp;
}
