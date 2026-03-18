// PassthroughCameraCapture.cs
// SexKit Quest App
//
// Captures real-world camera frames from Quest 3's passthrough cameras
// using the official Passthrough Camera API (PCA, v74+).
//
// The AI agent sees the REAL room — not rendered content, the actual
// camera feed from the headset. Sent upstream to iPhone via WebSocket.
//
// Two capture modes:
//   1. Real camera (PCA) — what the cameras actually see (physical room)
//   2. Rendered scene (ViewCapture) — virtual content (JOY, skybox, etc.)
//
// Both available simultaneously — different MCP resources.
//
// Requires: Meta XR SDK v74+, Quest 3/3S, camera permission granted
// AndroidManifest: android.permission.CAMERA or horizonos.permission.HEADSET_CAMERA

using System;
using System.Collections;
using UnityEngine;

public class PassthroughCameraCapture : MonoBehaviour
{
    public static PassthroughCameraCapture Instance { get; private set; }

    [Header("Settings")]
    public bool autoCapture = false;
    public float captureInterval = 2.0f;       // seconds between auto-captures
    public int jpegQuality = 60;
    public PassthroughCameraEye eye = PassthroughCameraEye.Left;

    [Header("Status")]
    public bool cameraReady = false;
    public bool permissionGranted = false;
    public int capturesSent = 0;
    public Vector2Int resolution;

    [Header("References")]
    public SexKitWebSocketClient wsClient;

    // The camera texture from PCA
    private WebCamTexture _webCamTexture;
    private Texture2D _readbackTexture;
    private float _lastCaptureTime;
    private bool _initializing = false;

    // Camera eye enum (matches Meta's PCA)
    public enum PassthroughCameraEye { Left, Right }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        wsClient ??= SexKitWebSocketClient.Instance;

#if UNITY_ANDROID && !UNITY_EDITOR
        RequestCameraPermission();
#else
        Debug.Log("[PCA] Passthrough Camera only available on Quest hardware");
#endif
    }

    void Update()
    {
        if (autoCapture && cameraReady && Time.time - _lastCaptureTime >= captureInterval)
        {
            CaptureAndSend();
            _lastCaptureTime = Time.time;
        }
    }

    // MARK: - Permission

    private void RequestCameraPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.CAMERA"))
        {
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += (perm) => {
                permissionGranted = true;
                StartCoroutine(InitializeCamera());
            };
            callbacks.PermissionDenied += (perm) => {
                Debug.LogWarning("[PCA] Camera permission denied");
            };
            UnityEngine.Android.Permission.RequestUserPermission("android.permission.CAMERA", callbacks);
        }
        else
        {
            permissionGranted = true;
            StartCoroutine(InitializeCamera());
        }
#endif
    }

    // MARK: - Initialize PCA Camera

    private IEnumerator InitializeCamera()
    {
        if (_initializing) yield break;
        _initializing = true;

        Debug.Log("[PCA] Initializing passthrough camera...");

        // Wait for WebCamTexture devices to be available
        int attempts = 0;
        while (WebCamTexture.devices.Length == 0 && attempts < 30)
        {
            yield return new WaitForSeconds(0.5f);
            attempts++;
        }

        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogError("[PCA] No cameras found after 15 seconds");
            _initializing = false;
            yield break;
        }

        // Log available cameras
        foreach (var device in WebCamTexture.devices)
        {
            Debug.Log($"[PCA] Found camera: {device.name}");
        }

        // Select camera based on eye preference
        // PCA exposes left and right passthrough cameras as separate WebCamTexture devices
        int cameraIndex = (eye == PassthroughCameraEye.Left) ? 0 : 1;
        cameraIndex = Mathf.Min(cameraIndex, WebCamTexture.devices.Length - 1);

        var deviceName = WebCamTexture.devices[cameraIndex].name;
        Debug.Log($"[PCA] Using camera: {deviceName} (index {cameraIndex})");

        // Create WebCamTexture at highest resolution
        // v85: supports 1280x960 and 1280x1280
        _webCamTexture = new WebCamTexture(deviceName, 1280, 1280);

        // Wait a frame before calling Play() (Meta bug workaround from QuestCameraKit)
        yield return null;
        yield return new WaitForSeconds(1);

        _webCamTexture.Play();

        // Wait for first frame
        while (!_webCamTexture.didUpdateThisFrame)
        {
            yield return null;
        }

        resolution = new Vector2Int(_webCamTexture.width, _webCamTexture.height);
        _readbackTexture = new Texture2D(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGB24, false);

        cameraReady = true;
        _initializing = false;
        Debug.Log($"[PCA] Camera ready: {resolution.x}x{resolution.y}");
    }

    // MARK: - Capture

    /// Capture current passthrough camera frame and send upstream
    public void CaptureAndSend()
    {
        if (!cameraReady || _webCamTexture == null || !_webCamTexture.isPlaying) return;

        // Read pixels from WebCamTexture
        _readbackTexture.SetPixels(_webCamTexture.GetPixels());
        _readbackTexture.Apply();

        // Encode to JPEG
        byte[] jpeg = _readbackTexture.EncodeToJPG(jpegQuality);
        if (jpeg == null || jpeg.Length == 0) return;

        // Send upstream with "real_camera" type to distinguish from rendered ViewCapture
        SendUpstream(jpeg);
        capturesSent++;
    }

    /// Capture and return as Texture2D (for local ML inference)
    public Texture2D CaptureAsTexture()
    {
        if (!cameraReady || _webCamTexture == null) return null;

        _readbackTexture.SetPixels(_webCamTexture.GetPixels());
        _readbackTexture.Apply();
        return _readbackTexture;
    }

    /// Capture and return as base64 JPEG
    public string CaptureAsBase64()
    {
        if (!cameraReady || _webCamTexture == null) return null;

        _readbackTexture.SetPixels(_webCamTexture.GetPixels());
        _readbackTexture.Apply();
        byte[] jpeg = _readbackTexture.EncodeToJPG(jpegQuality);
        return jpeg != null ? Convert.ToBase64String(jpeg) : null;
    }

    // MARK: - Send Upstream

    private void SendUpstream(byte[] jpeg)
    {
        if (wsClient == null || !wsClient.isConnected) return;

        var frame = new RealCameraFrame
        {
            type = "real_camera",
            eye = eye == PassthroughCameraEye.Left ? "left" : "right",
            width = resolution.x,
            height = resolution.y,
            jpegBase64 = Convert.ToBase64String(jpeg),
            timestamp = Time.realtimeSinceStartupAsDouble
        };

        string json = JsonUtility.ToJson(frame);
        wsClient.SendCommand(json);

        Debug.Log($"[PCA] Real camera frame sent: {jpeg.Length / 1024}KB");
    }

    // MARK: - Public Control

    public void StartAutoCapture(float interval = 2f)
    {
        captureInterval = interval;
        autoCapture = true;
    }

    public void StopAutoCapture()
    {
        autoCapture = false;
    }

    public void SwitchEye(PassthroughCameraEye newEye)
    {
        if (newEye == eye) return;
        eye = newEye;

        if (_webCamTexture != null)
        {
            _webCamTexture.Stop();
            Destroy(_webCamTexture);
            _webCamTexture = null;
            cameraReady = false;
        }

        StartCoroutine(InitializeCamera());
    }

    void OnDestroy()
    {
        if (_webCamTexture != null)
        {
            _webCamTexture.Stop();
            Destroy(_webCamTexture);
        }
        if (_readbackTexture != null) Destroy(_readbackTexture);
    }
}

[System.Serializable]
public class RealCameraFrame
{
    public string type;       // always "real_camera"
    public string eye;        // "left" or "right"
    public int width;
    public int height;
    public string jpegBase64;
    public double timestamp;
}
