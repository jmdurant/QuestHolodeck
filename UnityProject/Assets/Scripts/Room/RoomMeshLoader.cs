// RoomMeshLoader.cs
// SexKit Quest App
//
// Loads room mesh from SexKit's LiDAR scan (OBJ/FBX)
// OR loads Hyperscape Gaussian splat room capture
// Places as spatial anchor for the immersive scene

using UnityEngine;

public class RoomMeshLoader : MonoBehaviour
{
    [Header("Room Mesh")]
    public GameObject roomMeshPrefab;        // Pre-imported OBJ/FBX from SexKit LiDAR scan
    public Material roomMeshMaterial;         // Semi-transparent for mixed reality
    public float roomMeshOpacity = 0.25f;

    [Header("Bed")]
    public GameObject bedPrefab;
    public Vector3 bedOffset = new Vector3(0, 0.6f, -2f); // default placement

    [Header("Placement")]
    public bool usePassthrough = true;        // Quest 3 passthrough mixed reality
    public bool autoPlaceFromCalibration = true;

    private GameObject _roomInstance;
    private GameObject _bedInstance;

    void Start()
    {
        SexKitWebSocketClient.Instance.OnConnected += OnConnected;
        SexKitWebSocketClient.Instance.OnFrameReceived += OnFirstFrame;
    }

    void OnConnected()
    {
        // Request room mesh data from iPhone (future: transfer mesh over WebSocket)
        Debug.Log("[SexKit] Connected — room mesh will load from prefab");
        LoadRoomMesh();
    }

    void OnFirstFrame(LiveFrame frame)
    {
        // Use first frame's calibration data to size/place the bed
        if (frame.bedWidth > 0)
        {
            PlaceBed(frame.bedWidth, frame.bedLength, frame.mattressHeight);
            SexKitWebSocketClient.Instance.OnFrameReceived -= OnFirstFrame;
        }
    }

    void LoadRoomMesh()
    {
        if (roomMeshPrefab != null)
        {
            _roomInstance = Instantiate(roomMeshPrefab, Vector3.zero, Quaternion.identity);

            // Apply semi-transparent material for mixed reality
            if (roomMeshMaterial != null)
            {
                foreach (var renderer in _roomInstance.GetComponentsInChildren<Renderer>())
                {
                    var mat = new Material(roomMeshMaterial);
                    Color c = mat.color;
                    c.a = roomMeshOpacity;
                    mat.color = c;
                    renderer.material = mat;
                }
            }
        }

        // If using Quest passthrough, the real room IS the environment
        // Room mesh becomes optional visual aid / occlusion geometry
        if (usePassthrough)
        {
            // Enable Quest passthrough
            // OVRManager.instance.isInsightPassthroughEnabled = true;
            Debug.Log("[SexKit] Passthrough mode — real room visible");
        }
    }

    void PlaceBed(float width, float length, float mattressHeight)
    {
        if (bedPrefab != null && _bedInstance == null)
        {
            _bedInstance = Instantiate(bedPrefab);
        }

        if (_bedInstance != null)
        {
            _bedInstance.transform.localScale = new Vector3(width, 0.05f, length);
            _bedInstance.transform.position = new Vector3(0, mattressHeight, -2f);
        }
        else
        {
            // Create primitive bed if no prefab
            _bedInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _bedInstance.name = "Bed";
            _bedInstance.transform.localScale = new Vector3(width, 0.05f, length);
            _bedInstance.transform.position = new Vector3(0, mattressHeight, -2f);

            var mat = _bedInstance.GetComponent<Renderer>().material;
            mat.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);

            Destroy(_bedInstance.GetComponent<Collider>());
        }
    }
}
