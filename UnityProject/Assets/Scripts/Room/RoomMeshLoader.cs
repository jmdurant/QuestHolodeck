// RoomMeshLoader.cs
// SexKit Quest App
//
// Loads room mesh from SexKit's LiDAR scan (OBJ/FBX)
// OR loads Hyperscape Gaussian splat room capture
// Places as spatial anchor for the immersive scene

using UnityEngine;

public class RoomMeshLoader : MonoBehaviour
{
    [Header("References")]
    public SexKitAvatarDriver avatarDriver;

    [Header("Room Mesh")]
    public GameObject roomMeshPrefab;        // Pre-imported OBJ/FBX from SexKit LiDAR scan
    public Material roomMeshMaterial;         // Semi-transparent for mixed reality
    public float roomMeshOpacity = 0.25f;

    [Header("Bed")]
    public GameObject bedPrefab;
    public Vector3 bedOffset = new Vector3(0, 0.6f, -2f); // default placement

    [Header("Fallback Calibration Defaults")]
    public bool spawnFallbackBedOnStart = true;
    public string fallbackBedSize = "king";
    public float fallbackBedWidth = 1.9304f;   // 76 in
    public float fallbackBedLength = 2.032f;   // 80 in
    public float fallbackMattressHeight = 0.6f;
    public string fallbackUserSleepSide = "left";

    [Header("Bedside Tables")]
    public bool createBedsideTables = true;
    public Vector2 bedsideTableTopSize = new Vector2(0.6096f, 0.6096f); // 2ft x 2ft
    public float bedsideTableHeight = 0.6096f;
    public float bedsideTableGap = 0.08f;

    [Header("Pillows")]
    public bool createPillows = true;
    public Vector3 pillowSize = new Vector3(0.66f, 0.16f, 0.4f);
    public float pillowInsetFromHead = 0.22f;
    public float pillowSideInset = 0.16f;
    public float pillowTiltDegrees = 24f;

    [Header("Placement")]
    public bool usePassthrough = true;        // Quest 3 passthrough mixed reality
    public bool autoPlaceFromCalibration = true;

    private GameObject _roomInstance;
    private GameObject _bedInstance;
    private GameObject _leftBedsideTable;
    private GameObject _rightBedsideTable;
    private GameObject _leftPillow;
    private GameObject _rightPillow;

    void Start()
    {
        avatarDriver ??= FindFirstObjectByType<SexKitAvatarDriver>();
        if (avatarDriver != null && !string.IsNullOrWhiteSpace(fallbackUserSleepSide))
        {
            avatarDriver.fallbackUserSleepSide = fallbackUserSleepSide;
        }

        if (spawnFallbackBedOnStart)
        {
            PlaceBed(fallbackBedWidth, fallbackBedLength, fallbackMattressHeight);
        }

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
            _bedInstance.transform.position = new Vector3(bedOffset.x, mattressHeight, bedOffset.z);
        }
        else
        {
            // Create primitive bed if no prefab
            _bedInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _bedInstance.name = "Bed";
            _bedInstance.transform.localScale = new Vector3(width, 0.05f, length);
            _bedInstance.transform.position = new Vector3(bedOffset.x, mattressHeight, bedOffset.z);

            var mat = _bedInstance.GetComponent<Renderer>().material;
            mat.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);

            Destroy(_bedInstance.GetComponent<Collider>());
        }

        if (avatarDriver != null)
        {
            avatarDriver.bedTransform = _bedInstance.transform;
        }

        PlaceBedsideTables(width, length, mattressHeight);
        PlacePillows(width, length, mattressHeight);
    }

    private void PlaceBedsideTables(float width, float length, float mattressHeight)
    {
        if (!createBedsideTables)
        {
            return;
        }

        _leftBedsideTable ??= CreateBedsideTable("BedsideTableLeft");
        _rightBedsideTable ??= CreateBedsideTable("BedsideTableRight");

        var bedCenter = new Vector3(bedOffset.x, mattressHeight, bedOffset.z);
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

    private void PlacePillows(float width, float length, float mattressHeight)
    {
        if (!createPillows)
        {
            return;
        }

        _leftPillow ??= CreatePillow("PillowLeft");
        _rightPillow ??= CreatePillow("PillowRight");

        var bedCenter = new Vector3(bedOffset.x, mattressHeight, bedOffset.z);
        var halfWidth = width * 0.5f;
        var halfLength = length * 0.5f;
        var pillowCenterY = mattressHeight + pillowSize.y * 0.45f;
        var headZ = bedCenter.z + halfLength - pillowInsetFromHead - pillowSize.z * 0.5f;
        var leftX = bedCenter.x - (halfWidth * 0.25f + pillowSideInset);
        var rightX = bedCenter.x + (halfWidth * 0.25f + pillowSideInset);

        ConfigurePillow(_leftPillow, new Vector3(leftX, pillowCenterY, headZ));
        ConfigurePillow(_rightPillow, new Vector3(rightX, pillowCenterY, headZ));
    }

    private GameObject CreatePillow(string objectName)
    {
        var pillow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillow.name = objectName;
        var mat = pillow.GetComponent<Renderer>().material;
        mat.color = new Color(0.92f, 0.9f, 0.86f, 1f);
        return pillow;
    }

    private void ConfigurePillow(GameObject pillow, Vector3 centerPosition)
    {
        if (pillow == null)
        {
            return;
        }

        pillow.transform.localScale = pillowSize;
        pillow.transform.position = centerPosition;
        pillow.transform.rotation = Quaternion.Euler(-pillowTiltDegrees, 0f, 0f);
    }
}
