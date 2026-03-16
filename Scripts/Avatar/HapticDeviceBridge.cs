// HapticDeviceBridge.cs
// SexKit Quest App
//
// Bridges LiveFrame rhythm/intensity data to Bluetooth haptic devices
// Supports Lovense, Kiiroo, and generic BLE vibration devices
// Quest has Bluetooth access for peripheral connections

using UnityEngine;

public class HapticDeviceBridge : MonoBehaviour
{
    [Header("Settings")]
    public bool hapticEnabled = false;
    public float intensityMultiplier = 1.0f;
    public float minimumIntensity = 0.1f;

    [Header("Device")]
    public HapticDeviceType deviceType = HapticDeviceType.Generic;
    public string deviceAddress = "";  // BLE MAC address

    public enum HapticDeviceType
    {
        Generic,    // Any BLE vibration device
        Lovense,    // Lovense protocol
        Kiiroo,     // Kiiroo protocol
        TheHandy,   // The Handy protocol
    }

    private float _lastIntensity = 0;
    private float _lastRhythm = 0;

    void Start()
    {
        SexKitWebSocketClient.Instance.OnFrameReceived += OnFrame;
    }

    void OnFrame(LiveFrame frame)
    {
        if (!hapticEnabled) return;

        float intensity = Mathf.Clamp01((float)frame.localIntensity * intensityMultiplier);
        float rhythm = (float)frame.rhythmHz;

        // Only send updates when values change significantly (reduce BLE traffic)
        if (Mathf.Abs(intensity - _lastIntensity) < 0.05f &&
            Mathf.Abs(rhythm - _lastRhythm) < 0.1f) return;

        _lastIntensity = intensity;
        _lastRhythm = rhythm;

        switch (deviceType)
        {
            case HapticDeviceType.Lovense:
                SendLovenseCommand(intensity, rhythm);
                break;
            case HapticDeviceType.Kiiroo:
                SendKiirooCommand(intensity, rhythm);
                break;
            case HapticDeviceType.TheHandy:
                SendHandyCommand(intensity, rhythm);
                break;
            default:
                SendGenericBLE(intensity);
                break;
        }
    }

    // MARK: - Device Protocols

    private void SendLovenseCommand(float intensity, float rhythm)
    {
        // Lovense protocol: "Vibrate:X;" where X = 0-20
        int level = Mathf.RoundToInt(intensity * 20);
        string command = $"Vibrate:{level};";
        SendBLE(command);
    }

    private void SendKiirooCommand(float intensity, float rhythm)
    {
        // Kiiroo protocol: position 0-100 with speed
        int position = Mathf.RoundToInt(intensity * 100);
        int speed = Mathf.RoundToInt(rhythm * 33); // 0-99
        // Kiiroo uses FLIXT protocol or direct BLE characteristic writes
        Debug.Log($"[Kiiroo] Position: {position}, Speed: {speed}");
    }

    private void SendHandyCommand(float intensity, float rhythm)
    {
        // The Handy: stroke speed and length mapped from rhythm + intensity
        float strokeSpeed = rhythm * 100; // strokes per minute approximation
        float strokeLength = intensity * 100; // percentage of range
        Debug.Log($"[Handy] Speed: {strokeSpeed}, Length: {strokeLength}");
    }

    private void SendGenericBLE(float intensity)
    {
        // Generic BLE vibration: write intensity byte to characteristic
        byte level = (byte)(intensity * 255);
        Debug.Log($"[BLE] Vibrate: {level}/255");
    }

    private void SendBLE(string command)
    {
        // TODO: Implement actual BLE connection via AndroidJavaObject
        // Quest supports BLE via Android Bluetooth API
        //
        // var bluetoothAdapter = AndroidJavaClass("android.bluetooth.BluetoothAdapter");
        // var device = bluetoothAdapter.CallStatic("getDefaultAdapter")
        //     .Call("getRemoteDevice", deviceAddress);
        // var gatt = device.Call("connectGatt", ...);
        // gatt.Call("writeCharacteristic", ...);

        Debug.Log($"[BLE] Send: {command} to {deviceAddress}");
    }

    void OnDestroy()
    {
        if (SexKitWebSocketClient.Instance != null)
            SexKitWebSocketClient.Instance.OnFrameReceived -= OnFrame;
    }
}
