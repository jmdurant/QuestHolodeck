// SceneUnderstanding.cs
// SexKit Quest App
//
// Uses Meta MR Utility Kit (MRUK v85+) to detect the real room.
// Quest scans the room during guardian setup — we read that data to:
//   1. Find the bed (position, size, orientation in Quest space)
//   2. Get the floor plane
//   3. Bake a NavMesh for JOY pathfinding
//   4. Enable passthrough mixed reality
//
// This replaces hardcoded bed placement with real-world detection.
// The iPhone doesn't need to tell the Quest where the bed is —
// Quest sees it with its own cameras.

using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Meta.XR.MRUtilityKit;

public class SceneUnderstanding : MonoBehaviour
{
    [Header("References")]
    public RoomMeshLoader roomMeshLoader;
    public SexKitAvatarDriver avatarDriver;

    [Header("MRUK")]
    public MRUK mruk;

    [Header("Scene Detection")]
    public bool bedDetected = false;
    public Vector3 bedPosition;
    public Vector3 bedSize;           // width, height (mattress thickness), length
    public Quaternion bedRotation;
    public float floorY = 0f;

    [Header("NavMesh")]
    public bool bakeNavMesh = true;
    public NavMeshSurface navMeshSurface;

    [Header("Bed Surface")]
    public Transform bedSurfaceTransform;   // invisible plane at mattress height for JOY placement
    public bool bedSurfaceReady = false;
    public float bedSurfaceY = 0.6f;       // mattress top Y in world space

    [Header("Debug")]
    public bool showDebugVisuals = false;

    private MRUKRoom _currentRoom;
    private MRUKAnchor _bedAnchor;
    private MRUKAnchor _floorAnchor;
    private bool _sceneProcessed = false;

    void Start()
    {
        roomMeshLoader ??= FindFirstObjectByType<RoomMeshLoader>();
        avatarDriver ??= FindFirstObjectByType<SexKitAvatarDriver>();

        // Register for MRUK scene loaded callback
        if (MRUK.Instance != null)
        {
            MRUK.Instance.RegisterSceneLoadedCallback(OnSceneLoaded);
            Debug.Log("[SceneUnderstanding] Registered for MRUK scene callback");
        }
        else
        {
            Debug.LogWarning("[SceneUnderstanding] MRUK.Instance not found — add MRUK prefab to scene");
        }
    }

    // MARK: - Scene Loaded

    private void OnSceneLoaded()
    {
        if (_sceneProcessed) return;

        _currentRoom = MRUK.Instance.GetCurrentRoom();
        if (_currentRoom == null)
        {
            Debug.LogWarning("[SceneUnderstanding] No room found");
            return;
        }

        Debug.Log($"[SceneUnderstanding] Room loaded with {_currentRoom.Anchors.Count} anchors");

        // Find floor
        DetectFloor();

        // Find bed
        DetectBed();

        // Bake NavMesh
        if (bakeNavMesh)
        {
            BakeRuntimeNavMesh();
        }

        _sceneProcessed = true;
    }

    // MARK: - Detect Floor

    private void DetectFloor()
    {
        _floorAnchor = _currentRoom.FloorAnchors != null && _currentRoom.FloorAnchors.Count > 0
            ? _currentRoom.FloorAnchors[0]
            : null;
        if (_floorAnchor != null)
        {
            floorY = _floorAnchor.transform.position.y;
            Debug.Log($"[SceneUnderstanding] Floor detected at Y={floorY:F2}");

            // Update avatar driver standing Y
            if (avatarDriver != null)
            {
                avatarDriver.standingFloorY = floorY;
            }
        }
    }

    // MARK: - Detect Bed

    private void DetectBed()
    {
        foreach (var anchor in _currentRoom.Anchors)
        {
            if (anchor.Label == MRUKAnchor.SceneLabels.BED)
            {
                _bedAnchor = anchor;
                break;
            }

            // Fall back to COUCH if no bed label (user might have labeled it as couch)
            if (anchor.Label == MRUKAnchor.SceneLabels.COUCH && _bedAnchor == null)
            {
                _bedAnchor = anchor;
            }
        }

        if (_bedAnchor != null)
        {
            bedDetected = true;
            bedPosition = _bedAnchor.transform.position;
            bedRotation = _bedAnchor.transform.rotation;

            // VolumeBounds gives us the 3D bounding box
            var bounds = _bedAnchor.VolumeBounds ?? new Bounds(_bedAnchor.transform.position, Vector3.zero);
            bedSize = bounds.size;  // x = width, y = height (mattress), z = length

            // PlaneRect gives the top surface dimensions
            var planeRect = _bedAnchor.PlaneRect ?? new Rect(0f, 0f, bedSize.x, bedSize.z);

            Debug.Log($"[SceneUnderstanding] Bed detected at {bedPosition}, size={bedSize}, rotation={bedRotation.eulerAngles}");

            // Update RoomMeshLoader with real bed data
            if (roomMeshLoader != null)
            {
                // Override the fallback bed with the real one
                roomMeshLoader.bedOffset = bedPosition;
                roomMeshLoader.fallbackBedWidth = bedSize.x > 0 ? bedSize.x : planeRect.size.x;
                roomMeshLoader.fallbackBedLength = bedSize.z > 0 ? bedSize.z : planeRect.size.y;
                roomMeshLoader.fallbackMattressHeight = bedPosition.y;

                // Tell it to re-place the bed
                roomMeshLoader.SendMessage("PlaceBedFromScene", SendMessageOptions.DontRequireReceiver);
            }

            // Update avatar driver with real bed transform
            if (avatarDriver != null && avatarDriver.bedTransform != null)
            {
                avatarDriver.bedTransform.position = bedPosition;
                avatarDriver.bedTransform.rotation = bedRotation;
                avatarDriver.bedTransform.localScale = new Vector3(
                    bedSize.x > 0 ? bedSize.x : 1.93f,
                    0.05f,
                    bedSize.z > 0 ? bedSize.z : 2.03f
                );
            }

            // Create invisible bed surface for JOY placement
            CreateBedSurface();

            if (showDebugVisuals)
            {
                CreateDebugBedVisual();
            }
        }
        else
        {
            Debug.Log("[SceneUnderstanding] No bed detected — using fallback placement");
        }
    }

    private void CreateBedSurface()
    {
        // Invisible plane at the top of the mattress — JOY lies on this
        if (bedSurfaceTransform == null)
        {
            var surfaceObj = new GameObject("BedSurface");
            bedSurfaceTransform = surfaceObj.transform;
        }

        bedSurfaceY = bedPosition.y + bedSize.y * 0.5f;  // top of volume bounds

        bedSurfaceTransform.position = new Vector3(bedPosition.x, bedSurfaceY, bedPosition.z);
        bedSurfaceTransform.rotation = bedRotation;
        bedSurfaceTransform.localScale = new Vector3(
            bedSize.x > 0 ? bedSize.x : 1.93f,
            0.01f,
            bedSize.z > 0 ? bedSize.z : 2.03f
        );

        // Add collider so NavMesh knows the bed surface exists
        // but don't make it an obstacle — JOY needs to get ON it
        var collider = bedSurfaceTransform.gameObject.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = bedSurfaceTransform.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;  // trigger, not solid — JOY can move onto it
        }

        bedSurfaceReady = true;
        Debug.Log($"[SceneUnderstanding] Bed surface created at Y={bedSurfaceY:F2}");
    }

    // MARK: - NavMesh

    private void BakeRuntimeNavMesh()
    {
        // Option 1: Use MRUK's built-in SceneNavigation
        var sceneNav = FindFirstObjectByType<SceneNavigation>();
        if (sceneNav != null)
        {
            sceneNav.BuildSceneNavMesh();
            Debug.Log("[SceneUnderstanding] NavMesh baked via MRUK SceneNavigation");
            return;
        }

        // Option 2: Use Unity NavMeshSurface on the floor
        if (navMeshSurface == null && _floorAnchor != null)
        {
            // Create a NavMeshSurface on the floor
            var floorObj = _floorAnchor.gameObject;
            navMeshSurface = floorObj.GetComponent<NavMeshSurface>();
            if (navMeshSurface == null)
            {
                navMeshSurface = floorObj.AddComponent<NavMeshSurface>();
            }
        }

        if (navMeshSurface != null)
        {
            // Add colliders to furniture so NavMesh carves around them
            AddCollidersToFurniture();

            navMeshSurface.BuildNavMesh();
            Debug.Log("[SceneUnderstanding] NavMesh baked via Unity NavMeshSurface");
        }
    }

    private void AddCollidersToFurniture()
    {
        foreach (var anchor in _currentRoom.Anchors)
        {
            // Skip floor/ceiling/walls — those are surfaces, not obstacles
            if (anchor.Label == MRUKAnchor.SceneLabels.FLOOR ||
                anchor.Label == MRUKAnchor.SceneLabels.CEILING ||
                anchor.Label == MRUKAnchor.SceneLabels.WALL_FACE)
            {
                continue;
            }

            // Add box colliders to furniture so NavMesh carves around them
            var bounds = anchor.VolumeBounds;
            if (!bounds.HasValue)
            {
                continue;
            }

            var volumeBounds = bounds.Value;
            if (volumeBounds.size.sqrMagnitude > 0.01f)
            {
                var collider = anchor.gameObject.GetComponent<BoxCollider>();
                if (collider == null)
                {
                    collider = anchor.gameObject.AddComponent<BoxCollider>();
                    collider.size = volumeBounds.size;
                    collider.center = volumeBounds.center;

                    // Mark as NavMesh obstacle
                    var obstacle = anchor.gameObject.AddComponent<NavMeshObstacle>();
                    obstacle.shape = NavMeshObstacleShape.Box;
                    obstacle.size = volumeBounds.size;
                    obstacle.center = volumeBounds.center;
                    obstacle.carving = true;
                }
            }
        }
    }

    // MARK: - Public Queries

    /// Get a position beside the bed (for JOY staging)
    public Vector3? GetBedSidePosition(bool userSide)
    {
        if (!bedDetected) return null;

        var right = bedRotation * Vector3.right;
        var offset = right * (bedSize.x * 0.5f + 0.5f);  // half bed width + standing clearance

        return userSide
            ? bedPosition + offset
            : bedPosition - offset;
    }

    /// Get a position at the foot of the bed
    public Vector3? GetFootOfBedPosition()
    {
        if (!bedDetected) return null;
        var forward = bedRotation * Vector3.forward;
        return bedPosition - forward * (bedSize.z * 0.5f + 0.8f);
    }

    /// Get the bed surface center (for lying positions)
    public Vector3? GetBedSurfaceCenter()
    {
        if (!bedDetected) return null;
        return bedPosition + Vector3.up * (bedSize.y * 0.5f);
    }

    /// Get a position ON the bed for a given sex position.
    /// Returns world position at mattress surface height.
    /// The offset is relative to bed center, rotated to match bed orientation.
    public Vector3? GetOnBedPosition(string position, bool isPartner)
    {
        if (!bedDetected || bedSurfaceTransform == null) return null;

        // Base position: center of bed at mattress surface
        var center = new Vector3(bedPosition.x, bedSurfaceY, bedPosition.z);
        var right = bedRotation * Vector3.right;
        var forward = bedRotation * Vector3.forward;

        // Offset from center based on position + role
        // Partner (JOY) positions relative to bed center
        Vector3 offset = Vector3.zero;
        float heightOffset = 0f;  // above mattress surface

        var lower = (position ?? "").ToLowerInvariant();

        if (lower.Contains("missionary"))
        {
            // Lying on back, centered, slightly toward head of bed
            offset = forward * 0.2f;
            heightOffset = 0.05f;  // just above mattress
        }
        else if (lower.Contains("cowgirl"))
        {
            if (isPartner)
            {
                // Partner on top — elevated above bed center
                offset = forward * 0.1f;
                heightOffset = 0.4f;  // sitting height above mattress
            }
            else
            {
                offset = forward * 0.1f;
                heightOffset = 0.05f;
            }
        }
        else if (lower.Contains("doggy"))
        {
            // On hands and knees — centered
            offset = -forward * 0.1f;
            heightOffset = 0.3f;  // knees + torso height
        }
        else if (lower.Contains("spooning"))
        {
            // Side-lying — offset to one side
            offset = (isPartner ? -right : right) * 0.2f + forward * 0.2f;
            heightOffset = 0.1f;
        }
        else if (lower.Contains("prone"))
        {
            offset = forward * 0.1f;
            heightOffset = 0.05f;  // lying flat
        }
        else if (lower.Contains("69"))
        {
            offset = (isPartner ? -forward : forward) * 0.3f;
            heightOffset = 0.05f;
        }
        else if (lower.Contains("oral"))
        {
            if (isPartner)
            {
                offset = -forward * 0.3f;  // at foot of bed area
                heightOffset = 0.0f;  // kneeling at bed edge
            }
            else
            {
                offset = forward * 0.1f;
                heightOffset = 0.05f;  // lying/sitting on bed
            }
        }
        else if (lower.Contains("lotus") || lower.Contains("seated"))
        {
            offset = forward * 0.1f;
            heightOffset = 0.25f;  // sitting on bed
        }
        else
        {
            // Default: center of bed, slightly above surface
            heightOffset = 0.05f;
        }

        return center + offset + Vector3.up * heightOffset;
    }

    /// Get the bed edge position (for sitting on the edge, getting on/off)
    public Vector3? GetBedEdgePosition(string side)
    {
        if (!bedDetected) return null;

        var right = bedRotation * Vector3.right;
        var forward = bedRotation * Vector3.forward;
        var center = new Vector3(bedPosition.x, bedSurfaceY, bedPosition.z);

        return side.ToLowerInvariant() switch
        {
            "left" => center - right * (bedSize.x * 0.5f) + Vector3.down * 0.1f,
            "right" => center + right * (bedSize.x * 0.5f) + Vector3.down * 0.1f,
            "foot" => center - forward * (bedSize.z * 0.5f) + Vector3.down * 0.1f,
            "head" => center + forward * (bedSize.z * 0.5f) + Vector3.down * 0.1f,
            _ => center
        };
    }

    // MARK: - Debug

    private void CreateDebugBedVisual()
    {
        var debug = GameObject.CreatePrimitive(PrimitiveType.Cube);
        debug.name = "DebugBedBounds";
        debug.transform.position = bedPosition;
        debug.transform.rotation = bedRotation;
        debug.transform.localScale = bedSize;

        var mat = debug.GetComponent<Renderer>().material;
        mat.color = new Color(0f, 1f, 0f, 0.15f);
        Destroy(debug.GetComponent<Collider>());
    }
}
