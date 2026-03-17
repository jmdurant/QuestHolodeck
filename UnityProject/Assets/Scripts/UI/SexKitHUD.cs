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
    public float distanceFromHead = 0.65f;
    public float heightOffset = 0.2f;       // above eye level
    public float horizontalOffset = -0.18f; // negative = left of camera
    public bool followHead = true;
    public bool anchorToTarget = false;
    public Transform placementAnchor;
    public Vector3 anchorLocalOffset = new(-0.45f, 1.45f, 0.0f);
    public bool anchorFaceCamera = true;
    public float anchorYawOffset = 0f;
    public bool anchorFaceCameraFlip = true;
    public bool anchorTowardCameraFromTarget = false;
    public float anchorTowardCameraDistance = 0.55f;
    public float anchorVerticalOffset = 1.42f;
    public float anchorHorizontalOffset = 0f;
    public bool smoothPlacement = true;
    public float placementLerpSpeed = 4f;

    private Camera _mainCamera;
    private JoyBodyController _joyBodyController;
    private bool _conversationHrOnly;
    private bool _cachedTextStyle;
    private float _defaultPartnerFontSize;
    private bool _defaultPartnerAutoSize;

    void Start()
    {
        _mainCamera = Camera.main;
        _joyBodyController = FindFirstObjectByType<JoyBodyController>();
        SexKitWebSocketClient.Instance.OnFrameReceived += UpdateHUD;
        SexKitWebSocketClient.Instance.OnConnected += () => SetConnectionStatus(true);
        SexKitWebSocketClient.Instance.OnDisconnected += () => SetConnectionStatus(false);
    }

    void Update()
    {
        if (anchorToTarget && placementAnchor != null)
        {
            Vector3 targetPos;
            if (anchorTowardCameraFromTarget && _mainCamera != null)
            {
                Vector3 toCamera = _mainCamera.transform.position - placementAnchor.position;
                toCamera = Vector3.ProjectOnPlane(toCamera, Vector3.up);
                if (toCamera.sqrMagnitude < 0.0001f)
                    toCamera = -Vector3.ProjectOnPlane(_mainCamera.transform.forward, Vector3.up);

                Vector3 towardCamera = toCamera.normalized;
                Vector3 side = Vector3.Cross(Vector3.up, towardCamera).normalized;
                targetPos = placementAnchor.position
                    + Vector3.up * anchorVerticalOffset
                    + towardCamera * anchorTowardCameraDistance
                    + side * anchorHorizontalOffset;
            }
            else
            {
                targetPos = placementAnchor.TransformPoint(anchorLocalOffset);
            }

            if (smoothPlacement)
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * placementLerpSpeed);
            else
                transform.position = targetPos;

            Quaternion targetRotation;
            if (anchorFaceCamera && _mainCamera != null)
            {
                Vector3 toCamera = _mainCamera.transform.position - transform.position;
                toCamera = Vector3.ProjectOnPlane(toCamera, Vector3.up);
                if (toCamera.sqrMagnitude < 0.0001f)
                    toCamera = -Vector3.ProjectOnPlane(_mainCamera.transform.forward, Vector3.up);
                targetRotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
                if (anchorFaceCameraFlip)
                    targetRotation *= Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                Vector3 forward = Vector3.ProjectOnPlane(placementAnchor.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.0001f)
                    forward = Vector3.forward;
                targetRotation = Quaternion.LookRotation(forward.normalized, Vector3.up) * Quaternion.Euler(0f, anchorYawOffset, 0f);
            }

            if (smoothPlacement)
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 6f);
            else
                transform.rotation = targetRotation;
            return;
        }

        if (followHead && _mainCamera != null)
        {
            // Pin HUD to a top-left offset in camera space.
            Vector3 targetPos = _mainCamera.transform.position
                + _mainCamera.transform.forward * distanceFromHead
                + _mainCamera.transform.up * heightOffset
                + _mainCamera.transform.right * horizontalOffset;

            var targetRotation = Quaternion.LookRotation(
                targetPos - _mainCamera.transform.position,
                _mainCamera.transform.up
            );

            if (smoothPlacement)
            {
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * placementLerpSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 6f);
            }
            else
            {
                transform.position = targetPos;
                transform.rotation = targetRotation;
            }
        }
    }

    public void SetHeadFollowPlacement(bool enabled)
    {
        followHead = enabled;
        if (enabled)
        {
            anchorToTarget = false;
            placementAnchor = null;
        }
    }

    public void SetAnchoredPlacement(Transform anchor, Vector3 localOffset)
    {
        placementAnchor = anchor;
        anchorLocalOffset = localOffset;
        anchorTowardCameraFromTarget = false;
        anchorToTarget = anchor != null;
        followHead = !anchorToTarget;
    }

    public void SetAnchorTowardCameraPlacement(Transform anchor, float verticalOffset, float towardCameraDistance, float horizontalOffset = 0f)
    {
        placementAnchor = anchor;
        anchorVerticalOffset = verticalOffset;
        anchorTowardCameraDistance = towardCameraDistance;
        anchorHorizontalOffset = horizontalOffset;
        anchorTowardCameraFromTarget = anchor != null;
        anchorToTarget = anchor != null;
        followHead = false;
    }

    void UpdateHUD(LiveFrame frame)
    {
        if (_conversationHrOnly)
        {
            if (heartRateText != null)
            {
                var localHr = frame.heartRate > 0 ? frame.heartRate.ToString() : "--";
                heartRateText.text = $"HR {localHr}\nRR {FormatRespiratoryRate(frame.respiratoryRate)}";
            }

            if (partnerHeartRateText != null)
            {
                var partnerHr = frame.partnerHeartRate > 0 ? frame.partnerHeartRate.ToString() : "--";
                partnerHeartRateText.text = $"Joy HR {partnerHr}\nJoy RR {FormatRespiratoryRate(GetPartnerRespiratoryRate())}";
            }

            return;
        }

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

    public void SetConversationHrOnly(bool enabled)
    {
        _conversationHrOnly = enabled;
        SetUiElementVisible(positionText, !enabled);
        SetUiElementVisible(timerText, !enabled);
        SetUiElementVisible(intensityText, !enabled);
        SetUiElementVisible(connectionText, !enabled);
        SetUiElementVisible(dataSourceText, !enabled);
        SetUiElementVisible(heartRateIcon, !enabled);
        SetUiElementVisible(intensityBar, !enabled);
        SetUiElementVisible(partnerIntensityBar, !enabled);

        CacheDefaultPartnerTextStyle();

        if (enabled)
        {
            if (heartRateText != null)
                heartRateText.text = "HR --\nRR --";
            if (partnerHeartRateText != null)
                partnerHeartRateText.text = "Joy HR --\nJoy RR --";

            // Keep both columns visually aligned in conversation mode.
            if (heartRateText != null)
                heartRateText.alignment = TextAlignmentOptions.TopLeft;
            if (partnerHeartRateText != null)
            {
                partnerHeartRateText.alignment = TextAlignmentOptions.TopLeft;
                partnerHeartRateText.enableAutoSizing = false;
                if (heartRateText != null)
                    partnerHeartRateText.fontSize = heartRateText.fontSize;
            }
            ConfigureConversationColumns();
        }
        else
        {
            // Restore default right-column alignment outside conversation mode.
            if (partnerHeartRateText != null)
            {
                partnerHeartRateText.alignment = TextAlignmentOptions.TopRight;
                partnerHeartRateText.enableAutoSizing = _defaultPartnerAutoSize;
                partnerHeartRateText.fontSize = _defaultPartnerFontSize;
            }
        }
    }

    private double GetPartnerRespiratoryRate()
    {
        if (_joyBodyController == null)
            _joyBodyController = FindFirstObjectByType<JoyBodyController>();

        return _joyBodyController != null ? _joyBodyController.breathingRate : 0d;
    }

    private static string FormatRespiratoryRate(double rr)
    {
        return rr > 0.01d ? rr.ToString("0") : "--";
    }

    private static void SetUiElementVisible(Component component, bool visible)
    {
        if (component != null && component.gameObject != null)
            component.gameObject.SetActive(visible);
    }

    private void ConfigureConversationColumns()
    {
        if (heartRateText != null && heartRateText.rectTransform != null)
        {
            var left = heartRateText.rectTransform;
            left.anchorMin = new Vector2(0f, 1f);
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 1f);
            left.anchoredPosition = new Vector2(24f, -24f);
        }

        if (partnerHeartRateText != null && partnerHeartRateText.rectTransform != null)
        {
            var right = partnerHeartRateText.rectTransform;
            right.anchorMin = new Vector2(0f, 1f);
            right.anchorMax = new Vector2(0f, 1f);
            right.pivot = new Vector2(0f, 1f);
            right.anchoredPosition = new Vector2(200f, -24f);
        }
    }

    private void CacheDefaultPartnerTextStyle()
    {
        if (_cachedTextStyle || partnerHeartRateText == null)
            return;

        _defaultPartnerFontSize = partnerHeartRateText.fontSize;
        _defaultPartnerAutoSize = partnerHeartRateText.enableAutoSizing;
        _cachedTextStyle = true;
    }
}
