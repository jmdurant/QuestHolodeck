using System;
using UnityEngine;

[Serializable]
public class ControlFrame
{
    public string type;
    public double timestamp;
    public string mode;

    public ControlMovement movement;
    public ControlPosture posture;
    public ControlGesture gesture;
    public ControlGaze gaze;
    public ControlVerbal verbal;
    public ControlPhysical physical;
    public ControlBreathing breathing;
    public ControlReaction reaction;
    public ControlExpression expression;
    public ControlEnvironment environment;

    public bool HasMeaningfulPayload()
    {
        return !string.IsNullOrWhiteSpace(mode)
               || gaze != null
               || verbal != null
               || physical != null
               || breathing != null
               || reaction != null
               || expression != null
               || gesture != null
               || movement != null
               || posture != null
               || environment != null;
    }

    public bool IsControlType()
    {
        return string.Equals(type, "control", StringComparison.OrdinalIgnoreCase);
    }
}

[Serializable]
public class ControlMovement
{
    public float[] targetPosition;     // [x, y, z] room coordinates
    public string targetLocation;      // "bed", "beside_user", "foot_of_bed", "doorway"
    public float targetRotation;       // Y-axis degrees
    public string speed;               // "walk", "approach", "quick", "teleport"
    public string arrivedAction;       // "stand", "sit", "lie_down", "kneel"
}

[Serializable]
public class ControlPosture
{
    public string state;       // standing, sitting, lying_back, lying_face_down, lying_side, kneeling, crouching
    public string position;    // legacy alias for state
    public string facing;      // user_head, user_body, away, forward
    public float lean;         // 0-1
    public float openness;     // 0-1
    public float blendTime;

    public string ResolvedState => !string.IsNullOrWhiteSpace(state) ? state : position;
}

[Serializable]
public class ControlGesture
{
    public string type;        // reach, wave, beckon, touch_face, etc.
    public string name;        // legacy alias for type
    public string target;      // user_hand, user_face, user_shoulder
    public float intensity;    // 0-1
    public float blendTime;

    public string ResolvedType => !string.IsNullOrWhiteSpace(type) ? type : name;
}

[Serializable]
public class ControlGaze
{
    public string target;
    public float intensity;
    public string behavior;
}

[Serializable]
public class ControlVerbal
{
    public string text;
    public string emotion;
    public float urgency;
}

[Serializable]
public class ControlPhysical
{
    public string position;
    public float rhythmHz;
    public float intensity;
    public float amplitude;
    public string pacingMode;
    public int maxEdges;
    public bool breathingSync;
    public bool overridePacing;
}

[Serializable]
public class ControlBreathing
{
    public float rate;
    public float depth;
    public string pattern;
    public bool audible;
}

[Serializable]
public class ControlReaction
{
    public string type;
    public float intensity;
    public string trigger;
}

[Serializable]
public class ControlExpression
{
    public string expression;
    public float intensity;
    public float blendTime;
}

[Serializable]
public class ControlEnvironment
{
    public string lightScene;       // HomeKit scene name
    public float lightBrightness;   // 0-1
    public string lightColor;       // warm_candle, red, purple, blue, pink
    public string musicAction;      // play, pause, skip, set_playlist
    public string musicPlaylist;
    public float musicVolume;       // 0-1
    public string skybox;           // "passthrough", "mountain", "beach", "hotel", "cabin", "cave", "void"
}

[Serializable]
public struct SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }

    public bool IsMeaningful()
    {
        return !Mathf.Approximately(x, 0f)
               || !Mathf.Approximately(y, 0f)
               || !Mathf.Approximately(z, 0f);
    }
}

[Serializable]
public class SocketMessageEnvelope
{
    public string messageType;
    public string type;
    public LiveFrame liveFrame;
    public LiveFrame live;
    public ControlFrame controlFrame;
    public ControlFrame control;
}

public enum PartnerAttentionTarget
{
    None,
    UserFace,
    UserChest,
    Bed,
    LeftHandTarget,
    RightHandTarget,
    CustomPoint
}

public enum PartnerPoseIntent
{
    Idle,
    LeanIn,
    LeanBack,
    Kneeling,
    Reclined,
    ReachLeft,
    ReachRight,
    Brace,
    Comforting
}

public enum PartnerFacePreset
{
    Neutral,
    SoftSmile,
    Warm,
    Teasing,
    Concerned,
    Focused,
    Pleasure,
    IntensePleasure,
    Breathless,
    Gentle
}
