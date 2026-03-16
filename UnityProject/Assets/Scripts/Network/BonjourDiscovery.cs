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
    private NsdDiscoveryProxy _discoveryListener;
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
            // Use Android NsdManager directly via JNI — no custom Java plugin needed
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            _nsdManager = activity.Call<AndroidJavaObject>("getSystemService", "servicediscovery");

            // Create anonymous NsdManager.DiscoveryListener via AndroidJavaProxy
            _discoveryListener = new NsdDiscoveryProxy(this);

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
            OnScanTimeout?.Invoke();
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

    public void OnServiceResolved(string host, int port)
    {
        discoveredHost = host;
        discoveredPort = port;
        discoveredAddress = $"ws://{discoveredHost}:{discoveredPort}";
        isFound = true;
        isScanning = false;

        Debug.Log($"[Bonjour] Found SexKit server at {discoveredAddress}");

        UnityMainThreadDispatcher.Enqueue(() => {
            OnServiceFound?.Invoke(discoveredHost, discoveredPort);
        });
    }

    // AndroidJavaProxy implementing NsdManager.DiscoveryListener — no AAR/JAR needed
    class NsdDiscoveryProxy : AndroidJavaProxy
    {
        private BonjourDiscovery _owner;

        public NsdDiscoveryProxy(BonjourDiscovery owner)
            : base("android.net.nsd.NsdManager$DiscoveryListener")
        {
            _owner = owner;
        }

        void onDiscoveryStarted(string serviceType)
        {
            Debug.Log($"[NSD] Discovery started for {serviceType}");
        }

        void onServiceFound(AndroidJavaObject serviceInfo)
        {
            Debug.Log("[NSD] Service found — resolving...");
            // Resolve the service to get host + port
            var resolveProxy = new NsdResolveProxy(_owner);
            _owner._nsdManager.Call("resolveService", serviceInfo, resolveProxy);
        }

        void onServiceLost(AndroidJavaObject serviceInfo) { }
        void onDiscoveryStopped(string serviceType) { }

        void onStartDiscoveryFailed(string serviceType, int errorCode)
        {
            Debug.LogError($"[NSD] Discovery failed: error {errorCode}");
            UnityMainThreadDispatcher.Enqueue(() => _owner.OnScanTimeout?.Invoke());
        }

        void onStopDiscoveryFailed(string serviceType, int errorCode) { }
    }

    // AndroidJavaProxy implementing NsdManager.ResolveListener
    class NsdResolveProxy : AndroidJavaProxy
    {
        private BonjourDiscovery _owner;

        public NsdResolveProxy(BonjourDiscovery owner)
            : base("android.net.nsd.NsdManager$ResolveListener")
        {
            _owner = owner;
        }

        void onServiceResolved(AndroidJavaObject serviceInfo)
        {
            string host = serviceInfo.Call<AndroidJavaObject>("getHost").Call<string>("getHostAddress");
            int port = serviceInfo.Call<int>("getPort");
            _owner.OnServiceResolved(host, port);
        }

        void onResolveFailed(AndroidJavaObject serviceInfo, int errorCode)
        {
            Debug.LogWarning($"[NSD] Resolve failed: error {errorCode}");
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
