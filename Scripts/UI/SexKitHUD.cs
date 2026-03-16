// SexKitHUD.cs
// SexKit Quest App
//
// Head-up display for Quest — shows session data floating in space
// Heart rate, position, timer, connection status

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SexKitHUD : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI heartRateText;
    public TextMeshProUGUI partnerHeartRateText;
    public TextMeshProUGUI positionText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI intensityText;
    public TextMeshProUGUI connectionText;
    public TextMeshProUGUI dataSourceText;

    [Header("Visual")]
    public Image heartRateIcon;
    public Slider intensityBar;
    public Slider partnerIntensityBar;

    [Header("Placement")]
    public float distanceFromHead = 1.5f;
    public float heightOffset = -0.3f;      // slightly below eye level
    public bool followHead = true;

    private Camera _mainCamera;

    void Start()
    {
        _mainCamera = Camera.main;
        SexKitWebSocketClient.Instance.OnFrameReceived += UpdateHUD;
        SexKitWebSocketClient.Instance.OnConnected += () => SetConnectionStatus(true);
        SexKitWebSocketClient.Instance.OnDisconnected += () => SetConnectionStatus(false);
    }

    void Update()
    {
        if (followHead && _mainCamera != null)
        {
            // Float HUD in front of user's gaze
            Vector3 targetPos = _mainCamera.transform.position
                + _mainCamera.transform.forward * distanceFromHead
                + Vector3.up * heightOffset;

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 3f);
            transform.rotation = Quaternion.LookRotation(transform.position - _mainCamera.transform.position);
        }
    }

    void UpdateHUD(LiveFrame frame)
    {
        // Heart rates
        if (heartRateText != null)
            heartRateText.text = frame.heartRate > 0 ? $"{frame.heartRate}" : "--";

        if (partnerHeartRateText != null)
            partnerHeartRateText.text = frame.partnerHeartRate > 0 ? $"{frame.partnerHeartRate}" : "--";

        // Position
        if (positionText != null)
            positionText.text = !string.IsNullOrEmpty(frame.detectedPosition) ? frame.detectedPosition : "";

        // Timer
        if (timerText != null)
        {
            int mins = (int)(frame.sessionElapsed / 60);
            int secs = (int)(frame.sessionElapsed % 60);
            timerText.text = $"{mins:D2}:{secs:D2}";
        }

        // Intensity
        if (intensityBar != null)
            intensityBar.value = (float)frame.localIntensity;

        if (partnerIntensityBar != null)
            partnerIntensityBar.value = (float)frame.partnerIntensity;

        if (intensityText != null)
        {
            string label = frame.localIntensity switch
            {
                < 0.2 => "Gentle",
                < 0.5 => "Moderate",
                < 0.8 => "Active",
                _ => "Intense"
            };
            intensityText.text = label;
        }

        // Data source
        if (dataSourceText != null)
        {
            string tier = frame.dataSourceTier switch
            {
                1 => "ARKit Skeleton",
                2 => "Vision Pose",
                3 => "UWB Spatial",
                4 => "Watch Only",
                5 => "Inferred",
                _ => "Unknown"
            };
            dataSourceText.text = $"Tier {frame.dataSourceTier}: {tier}";
            if (frame.partnerIsInferred)
                dataSourceText.text += " (Partner inferred)";
        }
    }

    void SetConnectionStatus(bool connected)
    {
        if (connectionText != null)
            connectionText.text = connected ? "Connected" : "Disconnected";
    }

    void OnDestroy()
    {
        if (SexKitWebSocketClient.Instance != null)
            SexKitWebSocketClient.Instance.OnFrameReceived -= UpdateHUD;
    }
}
