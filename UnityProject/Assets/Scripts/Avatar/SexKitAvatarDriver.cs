// SexKitAvatarDriver.cs
// SexKit Quest App
//
// Drives two body avatars from LiveFrame skeleton data
// Supports: Meta Avatars SDK, generic Unity Humanoid, or primitive skeleton
// 60fps interpolation from 2fps WebSocket data

using UnityEngine;
using System.Collections.Generic;

public class SexKitAvatarDriver : MonoBehaviour
{
    [Header("Avatar Mode")]
    public AvatarMode mode = AvatarMode.Primitive;

    [Header("References")]
    public Transform bodyARoot;
    public Transform bodyBRoot;
    public Transform bedTransform;

    [Header("Settings")]
    public Vector3 sceneOffset = new Vector3(0, 0, -2f); // place scene in front of user
    public float interpolationSpeed = 4f;

    // Joint transforms for primitive mode
    private Dictionary<string, Transform> _jointsA = new();
    private Dictionary<string, Transform> _jointsB = new();
    private Dictionary<string, LineRenderer> _bonesA = new();
    private Dictionary<string, LineRenderer> _bonesB = new();

    // Interpolation targets
    private Dictionary<string, Vector3> _targetA = new();
    private Dictionary<string, Vector3> _targetB = new();
    private Dictionary<string, Vector3> _currentA = new();
    private Dictionary<string, Vector3> _currentB = new();

    public enum AvatarMode
    {
        Primitive,      // Spheres + lines (like SexKit's RealityKit rendering)
        UnityHumanoid,  // Standard Unity Animator with Humanoid rig
        MetaAvatar      // Meta Avatars SDK with ApplyStreamData
    }

    // Core bone connections (15 bones — always rendered)
    private static readonly (string, string)[] CoreBones = {
        ("head", "neck"), ("neck", "spine"), ("spine", "hip"),
        ("neck", "leftShoulder"), ("leftShoulder", "leftElbow"), ("leftElbow", "leftWrist"),
        ("neck", "rightShoulder"), ("rightShoulder", "rightElbow"), ("rightElbow", "rightWrist"),
        ("hip", "leftHip"), ("leftHip", "leftKnee"), ("leftKnee", "leftAnkle"),
        ("hip", "rightHip"), ("rightHip", "rightKnee"), ("rightKnee", "rightAnkle"),
    };

    // Extended bone connections (added when ARKit 91-joint data is available)
    private static readonly (string, string)[] ExtendedBones = {
        // Full spine chain
        ("neck", "spine1"), ("spine1", "spine2"), ("spine2", "spine3"),
        ("spine3", "spine4"), ("spine4", "spine5"), ("spine5", "spine6"), ("spine6", "spine7"),
        // Upper/lower arm segments
        ("leftShoulder", "leftUpperArm"), ("leftUpperArm", "leftElbow"),
        ("leftElbow", "leftForearm"), ("leftForearm", "leftWrist"),
        ("rightShoulder", "rightUpperArm"), ("rightUpperArm", "rightElbow"),
        ("rightElbow", "rightForearm"), ("rightForearm", "rightWrist"),
        // Upper/lower leg segments
        ("leftHip", "leftUpperLeg"), ("leftUpperLeg", "leftKnee"),
        ("leftKnee", "leftLowerLeg"), ("leftLowerLeg", "leftAnkle"),
        ("rightHip", "rightUpperLeg"), ("rightUpperLeg", "rightKnee"),
        ("rightKnee", "rightLowerLeg"), ("rightLowerLeg", "rightAnkle"),
        // Feet
        ("leftAnkle", "leftFoot"), ("leftFoot", "leftToes"),
        ("rightAnkle", "rightFoot"), ("rightFoot", "rightToes"),
        // Face
        ("head", "nose"), ("head", "jaw"),
        ("head", "leftEye"), ("head", "rightEye"),
        ("head", "leftEar"), ("head", "rightEar"),
        // Left hand (index + thumb only for visual clarity)
        ("leftWrist", "leftHandThumb1"), ("leftHandThumb1", "leftHandThumb2"), ("leftHandThumb2", "leftHandThumb3"),
        ("leftWrist", "leftHandIndex1"), ("leftHandIndex1", "leftHandIndex2"), ("leftHandIndex2", "leftHandIndex3"),
        ("leftWrist", "leftHandMiddle1"), ("leftHandMiddle1", "leftHandMiddle2"), ("leftHandMiddle2", "leftHandMiddle3"),
        // Right hand
        ("rightWrist", "rightHandThumb1"), ("rightHandThumb1", "rightHandThumb2"), ("rightHandThumb2", "rightHandThumb3"),
        ("rightWrist", "rightHandIndex1"), ("rightHandIndex1", "rightHandIndex2"), ("rightHandIndex2", "rightHandIndex3"),
        ("rightWrist", "rightHandMiddle1"), ("rightHandMiddle1", "rightHandMiddle2"), ("rightHandMiddle2", "rightHandMiddle3"),
    };

    // Combined — used for primitive rendering
    private static (string, string)[] Bones => CoreBones;

    void Start()
    {
        SexKitWebSocketClient.Instance.OnFrameReceived += OnFrame;

        if (mode == AvatarMode.Primitive)
        {
            bodyARoot = BuildPrimitiveBody(ref _jointsA, ref _bonesA, "BodyA", Color.red);
            bodyBRoot = BuildPrimitiveBody(ref _jointsB, ref _bonesB, "BodyB", new Color(1f, 0.4f, 0.6f));
        }
    }

    void Update()
    {
        // Smooth interpolation toward targets
        float t = Time.deltaTime * interpolationSpeed;

        // Iterate all joints that have target data (handles both 16 and 91 joint tiers)
        foreach (var joint in SkeletonData.AllJointNames)
        {
            if (_targetA.ContainsKey(joint))
            {
                if (!_currentA.ContainsKey(joint)) _currentA[joint] = _targetA[joint];
                _currentA[joint] = Vector3.Lerp(_currentA[joint], _targetA[joint], t);

                if (mode == AvatarMode.Primitive && _jointsA.ContainsKey(joint))
                    _jointsA[joint].localPosition = _currentA[joint] + sceneOffset;
            }

            if (_targetB.ContainsKey(joint))
            {
                if (!_currentB.ContainsKey(joint)) _currentB[joint] = _targetB[joint];
                _currentB[joint] = Vector3.Lerp(_currentB[joint], _targetB[joint], t);

                if (mode == AvatarMode.Primitive && _jointsB.ContainsKey(joint))
                    _jointsB[joint].localPosition = _currentB[joint] + sceneOffset;
            }
        }

        // Update bone lines
        if (mode == AvatarMode.Primitive)
        {
            UpdateBoneLines(_bonesA, _currentA);
            UpdateBoneLines(_bonesB, _currentB);
        }

        // Update bed from latest frame
        if (SexKitWebSocketClient.Instance.latestFrame != null && bedTransform != null)
        {
            var frame = SexKitWebSocketClient.Instance.latestFrame;
            if (frame.bedWidth > 0 && frame.bedLength > 0)
            {
                bedTransform.localScale = new Vector3(frame.bedWidth, 0.05f, frame.bedLength);
                bedTransform.localPosition = new Vector3(0, frame.mattressHeight, 0) + sceneOffset;
            }
        }
    }

    private void OnFrame(LiveFrame frame)
    {
        // Update targets from skeleton data
        // Use AllJointNames when ARKit tier (91 joints), CoreJointNames for lower tiers
        if (frame.skeletonA != null)
        {
            var jointNames = (frame.skeletonA.tier <= 1 && frame.skeletonA.jointCount > 16)
                ? SkeletonData.AllJointNames
                : SkeletonData.CoreJointNames;

            foreach (var joint in jointNames)
            {
                Vector3 pos = frame.skeletonA.GetJoint(joint);
                if (pos != Vector3.zero) _targetA[joint] = pos;
            }
        }

        if (frame.skeletonB != null)
        {
            var jointNames = (frame.skeletonB.tier <= 1 && frame.skeletonB.jointCount > 16)
                ? SkeletonData.AllJointNames
                : SkeletonData.CoreJointNames;

            foreach (var joint in jointNames)
            {
                Vector3 pos = frame.skeletonB.GetJoint(joint);
                if (pos != Vector3.zero) _targetB[joint] = pos;
            }
        }
    }

    // MARK: - Primitive Body Builder

    public void SetPrimitiveVisibility(bool bodyAVisible, bool bodyBVisible)
    {
        if (bodyARoot != null)
            bodyARoot.gameObject.SetActive(bodyAVisible);

        if (bodyBRoot != null)
            bodyBRoot.gameObject.SetActive(bodyBVisible);
    }

    private Transform BuildPrimitiveBody(
        ref Dictionary<string, Transform> joints,
        ref Dictionary<string, LineRenderer> bones,
        string name, Color color)
    {
        var root = new GameObject(name).transform;
        root.SetParent(transform);

        // Joint spheres
        foreach (var jointName in SkeletonData.JointNames)
        {
            float radius = jointName == "head" ? 0.08f : 0.03f;
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"{name}_{jointName}";
            sphere.transform.SetParent(root);
            sphere.transform.localScale = Vector3.one * radius * 2;

            var mat = sphere.GetComponent<Renderer>().material;
            mat.color = new Color(color.r, color.g, color.b, 0.9f);

            // Remove collider
            Destroy(sphere.GetComponent<Collider>());

            joints[jointName] = sphere.transform;
        }

        // Bone lines
        foreach (var bone in Bones)
        {
            var lineObj = new GameObject($"{name}_bone_{bone.Item1}_{bone.Item2}");
            lineObj.transform.SetParent(root);
            var line = lineObj.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = 0.025f;
            line.endWidth = 0.025f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(color.r, color.g, color.b, 0.6f);
            line.endColor = new Color(color.r, color.g, color.b, 0.6f);

            bones[$"{bone.Item1}_{bone.Item2}"] = line;
        }

        return root;
    }

    private void UpdateBoneLines(Dictionary<string, LineRenderer> bones, Dictionary<string, Vector3> positions)
    {
        foreach (var bone in Bones)
        {
            string key = $"{bone.Item1}_{bone.Item2}";
            if (bones.ContainsKey(key) && positions.ContainsKey(bone.Item1) && positions.ContainsKey(bone.Item2))
            {
                var line = bones[key];
                line.SetPosition(0, positions[bone.Item1] + sceneOffset);
                line.SetPosition(1, positions[bone.Item2] + sceneOffset);
            }
        }
    }

    void OnDestroy()
    {
        if (SexKitWebSocketClient.Instance != null)
            SexKitWebSocketClient.Instance.OnFrameReceived -= OnFrame;
    }
}
