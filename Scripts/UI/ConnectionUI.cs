// ConnectionUI.cs
// SexKit Quest App
//
// Connection screen — auto-discovers iPhone via Bonjour or manual IP entry
// Shows connection status, frame count, data preview

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConnectionUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField addressInput;
    public Button connectButton;
    public Button disconnectButton;
    public Button scanButton;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI frameCountText;
    public TextMeshProUGUI dataPreviewText;
    public GameObject connectPanel;
    public GameObject connectedPanel;

    [Header("Auto-Discovery")]
    public BonjourDiscovery bonjourDiscovery;

    void Start()
    {
        connectButton?.onClick.AddListener(OnConnect);
        disconnectButton?.onClick.AddListener(OnDisconnect);
        scanButton?.onClick.AddListener(OnScan);

        SexKitWebSocketClient.Instance.OnConnected += OnConnected;
        SexKitWebSocketClient.Instance.OnDisconnected += OnDisconnected;
        SexKitWebSocketClient.Instance.OnFrameReceived += OnFrame;

        if (bonjourDiscovery != null)
        {
            bonjourDiscovery.OnServiceFound += OnServiceFound;
            bonjourDiscovery.OnScanTimeout += OnScanTimeout;
        }

        ShowConnectPanel();

        // Auto-scan on start
        OnScan();
    }

    void ShowConnectPanel()
    {
        if (connectPanel != null) connectPanel.SetActive(true);
        if (connectedPanel != null) connectedPanel.SetActive(false);
    }

    void ShowConnectedPanel()
    {
        if (connectPanel != null) connectPanel.SetActive(false);
        if (connectedPanel != null) connectedPanel.SetActive(true);
    }

    // MARK: - Actions

    void OnConnect()
    {
        string address = addressInput != null ? addressInput.text : "";
        if (string.IsNullOrEmpty(address))
        {
            SetStatus("Enter an address or tap Scan");
            return;
        }

        SexKitWebSocketClient.Instance.serverAddress = address;
        SexKitWebSocketClient.Instance.Connect();
        SetStatus($"Connecting to {address}...");
    }

    void OnDisconnect()
    {
        SexKitWebSocketClient.Instance.Disconnect();
    }

    void OnScan()
    {
        if (bonjourDiscovery != null)
        {
            SetStatus("Scanning for SexKit on your network...");
            bonjourDiscovery.StartDiscovery();
        }
        else
        {
            SetStatus("Bonjour not available — enter IP manually");
        }
    }

    // MARK: - Bonjour Callbacks

    void OnServiceFound(string host, int port)
    {
        string address = $"ws://{host}:{port}";
        SetStatus($"Found SexKit at {address}");

        if (addressInput != null)
            addressInput.text = address;

        // Auto-connect
        SexKitWebSocketClient.Instance.serverAddress = address;
        SexKitWebSocketClient.Instance.Connect();
    }

    void OnScanTimeout()
    {
        SetStatus("No SexKit server found — enter IP manually");
    }

    // MARK: - WebSocket Callbacks

    void OnConnected()
    {
        SetStatus("Connected");
        ShowConnectedPanel();
    }

    void OnDisconnected()
    {
        SetStatus("Disconnected");
        ShowConnectPanel();
    }

    void OnFrame(LiveFrame frame)
    {
        if (frameCountText != null)
            frameCountText.text = $"{SexKitWebSocketClient.Instance.framesReceived} frames";

        if (dataPreviewText != null)
        {
            string preview = "";
            if (frame.heartRate > 0) preview += $"HR: {frame.heartRate}  ";
            if (!string.IsNullOrEmpty(frame.detectedPosition)) preview += $"Pos: {frame.detectedPosition}  ";
            if (!string.IsNullOrEmpty(frame.pacingPhase)) preview += $"Phase: {frame.pacingPhase}  ";
            if (frame.dataSourceTier > 0) preview += $"Tier: {frame.dataSourceTier}";
            dataPreviewText.text = preview;
        }
    }

    void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
        Debug.Log($"[ConnectionUI] {text}");
    }

    void OnDestroy()
    {
        if (SexKitWebSocketClient.Instance != null)
        {
            SexKitWebSocketClient.Instance.OnConnected -= OnConnected;
            SexKitWebSocketClient.Instance.OnDisconnected -= OnDisconnected;
            SexKitWebSocketClient.Instance.OnFrameReceived -= OnFrame;
        }
    }
}
