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

    [Header("Bedside Tables")]
    public bool createBedsideTables = true;
    public Vector2 bedsideTableTopSize = new Vector2(0.6096f, 0.6096f); // 2ft x 2ft
    public float bedsideTableHeight = 0.6096f;
    public float bedsideTableGap = 0.08f;

    [Header("Placement")]
    public bool usePassthrough = true;        // Quest 3 passthrough mixed reality
    public bool autoPlaceFromCalibration = true;

    private GameObject _roomInstance;
    private GameObject _bedInstance;
    private GameObject _leftBedsideTable;
    private GameObject _rightBedsideTable;

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

        PlaceBedsideTables(width, length, mattressHeight);
    }

    private void PlaceBedsideTables(float width, float length, float mattressHeight)
    {
        if (!createBedsideTables)
        {
            return;
        }

        _leftBedsideTable ??= CreateBedsideTable("BedsideTableLeft");
        _rightBedsideTable ??= CreateBedsideTable("BedsideTableRight");

        var bedCenter = new Vector3(0f, mattressHeight, -2f);
        var halfWidth = width * 0.5f;
        var halfLength = length * 0.5f;
        var halfTableWidth = bedsideTableTopSize.x * 0.5f;
        var tableCenterY = mattressHeight - 0.025f + bedsideTableHeight * 0.5f;
        var headZ = bedCenter.z + halfLength - halfTableWidth;

        var leftX = bedCenter.x - (halfWidth + bedsideTableGap + halfTableWidth);
        var rightX = bedCenter.x + (halfWidth + bedsideTableGap + halfTableWidth);

        ConfigureBedsideTable(_leftBedsideTable, new Vector3(leftX, tableCenterY, headZ));
        ConfigureBedsideTable(_rightBedsideTable, new Vector3(rightX, tableCenterY, headZ));
    }

    private GameObject CreateBedsideTable(string objectName)
    {
        var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = objectName;
        var mat = table.GetComponent<Renderer>().material;
        mat.color = new Color(0.22f, 0.18f, 0.14f, 1f);
        return table;
    }

    private void ConfigureBedsideTable(GameObject table, Vector3 centerPosition)
    {
        if (table == null)
        {
            return;
        }

        table.transform.localScale = new Vector3(bedsideTableTopSize.x, bedsideTableHeight, bedsideTableTopSize.y);
        table.transform.position = centerPosition;
    }
}
