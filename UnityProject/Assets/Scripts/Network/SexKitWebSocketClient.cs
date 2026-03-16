// SexKitWebSocketClient.cs
// SexKit Quest App
//
// Connects to SexKit iPhone via WebSocket, receives LiveFrame data
// Uses NativeWebSocket (Unity package) or System.Net.WebSockets

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using UnityEngine;

public class SexKitWebSocketClient : MonoBehaviour
{
    public static SexKitWebSocketClient Instance { get; private set; }

    [Header("Connection")]
    public string serverAddress = "192.168.1.5:8080";
    public bool autoReconnect = true;

    [Header("Status")]
    public bool isConnected = false;
    public int framesReceived = 0;
    public LiveFrame latestFrame;

    // Events
    public event Action<LiveFrame> OnFrameReceived;
    public event Action OnConnected;
    public event Action OnDisconnected;

    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    private bool _shouldRun = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    public async void Connect()
    {
        string url = serverAddress.StartsWith("ws") ? serverAddress : $"ws://{serverAddress}";

        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();
        _shouldRun = true;

        try
        {
            await _ws.ConnectAsync(new Uri(url), _cts.Token);
            isConnected = true;
            framesReceived = 0;
            OnConnected?.Invoke();
            Debug.Log($"[SexKit] Connected to {url}");

            _ = ReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SexKit] Connection failed: {e.Message}");
            isConnected = false;
        }
    }

    public async void Disconnect()
    {
        _shouldRun = false;
        if (_ws != null && _ws.State == WebSocketState.Open)
        {
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "User disconnect", CancellationToken.None);
        }
        isConnected = false;
        OnDisconnected?.Invoke();
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[16384];  // 16KB to handle large frames
        var messageBuffer = new System.IO.MemoryStream();

        while (_shouldRun && _ws.State == WebSocketState.Open)
        {
            try
            {
                messageBuffer.SetLength(0);  // reset for new message

                // Read until EndOfMessage — handles fragmented WebSocket frames
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                    messageBuffer.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string json = Encoding.UTF8.GetString(messageBuffer.GetBuffer(), 0, (int)messageBuffer.Length);
                    var frame = JsonUtility.FromJson<LiveFrame>(json);
                    if (frame != null)
                    {
                        latestFrame = frame;
                        framesReceived++;

                        UnityMainThreadDispatcher.Enqueue(() =>
                        {
                            OnFrameReceived?.Invoke(frame);
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SexKit] Receive error: {e.Message}");
                break;
            }
        }

        isConnected = false;
        UnityMainThreadDispatcher.Enqueue(() => OnDisconnected?.Invoke());

        if (autoReconnect && _shouldRun)
        {
            Debug.Log("[SexKit] Reconnecting in 3s...");
            await Task.Delay(3000);
            if (_shouldRun) Connect();
        }
    }

    public async void SendCommand(string json)
    {
        if (_ws != null && _ws.State == WebSocketState.Open)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
        }
    }

    void OnDestroy()
    {
        _shouldRun = false;
        _cts?.Cancel();
        _ws?.Dispose();
    }
}

// Simple main thread dispatcher for Unity
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly System.Collections.Generic.Queue<Action> _queue = new();
    private static UnityMainThreadDispatcher _instance;

    void Awake()
    {
        if (_instance == null) { _instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    public static void Enqueue(Action action) { lock (_queue) _queue.Enqueue(action); }

    void Update()
    {
        lock (_queue)
        {
            while (_queue.Count > 0) _queue.Dequeue()?.Invoke();
        }
    }
}
