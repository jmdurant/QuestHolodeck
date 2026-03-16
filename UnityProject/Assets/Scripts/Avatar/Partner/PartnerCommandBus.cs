using System;
using UnityEngine;

public class PartnerCommandBus : MonoBehaviour
{
    public SexKitWebSocketClient wsClient;
    public bool logCommands = true;
    public int controlFramesReceived;
    public ControlFrame latestFrame;

    public event Action<ControlFrame> OnControlFrameReceived;

    void Start()
    {
        wsClient ??= SexKitWebSocketClient.Instance;
        if (wsClient != null)
        {
            wsClient.OnControlFrameReceived += HandleControlFrame;
        }
    }

    public void Dispatch(ControlFrame frame)
    {
        HandleControlFrame(frame);
    }

    private void HandleControlFrame(ControlFrame frame)
    {
        latestFrame = frame;
        controlFramesReceived++;

        if (logCommands)
        {
            Debug.Log($"[PartnerCommandBus] mode={frame.mode} gaze={frame.gaze?.target} expression={frame.expression?.expression} speech={frame.verbal?.text}");
        }

        OnControlFrameReceived?.Invoke(frame);
    }

    void OnDestroy()
    {
        if (wsClient != null)
        {
            wsClient.OnControlFrameReceived -= HandleControlFrame;
        }
    }
}
