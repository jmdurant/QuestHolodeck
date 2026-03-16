// BonjourDiscovery.cs
// SexKit Quest App
//
// Auto-discovers SexKit iPhone WebSocket server on local network
// Uses Android NsdManager (Network Service Discovery) which speaks
// the same mDNS/DNS-SD protocol as Apple's Bonjour
//
// iPhone advertises: _sexkit-stream._tcp
// Quest discovers it: no manual IP entry needed

using UnityEngine;
using System;

public class BonjourDiscovery : MonoBehaviour
{
    public static BonjourDiscovery Instance { get; private set; }

    [Header("Settings")]
    public string serviceType = "_sexkit-stream._tcp.";
    public float scanTimeout = 10f;

    [Header("Status")]
    public bool isScanning = false;
    public bool isFound = false;
    public string discoveredHost = "";
    public int discoveredPort = 0;
    public string discoveredAddress = "";

    // Events
    public event Action<string, int> OnServiceFound;  // host, port
    public event Action OnScanTimeout;

    private float _scanStartTime;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _nsdManager;
    private AndroidJavaObject _discoveryListener;
#endif

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// Start scanning for SexKit server on local network
    public void StartDiscovery()
    {
        isScanning = true;
        isFound = false;
        discoveredHost = "";
        discoveredPort = 0;
        _scanStartTime = Time.time;

        Debug.Log($"[Bonjour] Scanning for {serviceType}...");

#if UNITY_ANDROID && !UNITY_EDITOR
        StartAndroidNsdDiscovery();
#else
        // Editor/non-Android fallback: manual entry
        Debug.Log("[Bonjour] NSD only available on Android. Use manual IP entry.");
        isScanning = false;
#endif
    }

    public void StopDiscovery()
    {
        isScanning = false;

#if UNITY_ANDROID && !UNITY_EDITOR
        StopAndroidNsdDiscovery();
#endif
    }

    void Update()
    {
        if (isScanning && !isFound && Time.time - _scanStartTime > scanTimeout)
        {
            Debug.Log("[Bonjour] Scan timeout — no SexKit server found");
            isScanning = false;
            OnScanTimeout?.Invoke();
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR

    void StartAndroidNsdDiscovery()
    {
        try
        {
            // Get Android NsdManager system service
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            _nsdManager = activity.Call<AndroidJavaObject>("getSystemService", "servicediscovery");

            // Create discovery listener
            _discoveryListener = new AndroidJavaObject(
                "com.sexkit.quest.NsdDiscoveryListener",
                gameObject.name  // callback target
            );

            // Start discovery
            _nsdManager.Call("discoverServices",
                "_sexkit-stream._tcp.",
                1,  // NsdManager.PROTOCOL_DNS_SD
                _discoveryListener
            );

            Debug.Log("[Bonjour] Android NSD discovery started");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Bonjour] Failed to start NSD: {e.Message}");
            isScanning = false;
        }
    }

    void StopAndroidNsdDiscovery()
    {
        try
        {
            if (_nsdManager != null && _discoveryListener != null)
            {
                _nsdManager.Call("stopServiceDiscovery", _discoveryListener);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Bonjour] Stop NSD: {e.Message}");
        }
    }

    // Called from Android NsdDiscoveryListener via UnitySendMessage
    public void OnNsdServiceFound(string serviceInfo)
    {
        // serviceInfo format: "host:port"
        var parts = serviceInfo.Split(':');
        if (parts.Length >= 2)
        {
            discoveredHost = parts[0];
            discoveredPort = int.Parse(parts[1]);
            discoveredAddress = $"ws://{discoveredHost}:{discoveredPort}";
            isFound = true;
            isScanning = false;

            Debug.Log($"[Bonjour] Found SexKit server at {discoveredAddress}");
            OnServiceFound?.Invoke(discoveredHost, discoveredPort);
        }
    }

#endif

    /// Get the WebSocket URL (discovered or manual)
    public string GetServerURL()
    {
        if (isFound && !string.IsNullOrEmpty(discoveredAddress))
        {
            return discoveredAddress;
        }
        return "";  // not found — user must enter manually
    }
}
