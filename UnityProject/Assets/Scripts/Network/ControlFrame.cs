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
    public string type;
    public float speed;
    public float amplitude;
    public SerializableVector3 target;
}

[Serializable]
public class ControlPosture
{
    public string position;
    public float blendTime;
}

[Serializable]
public class ControlGesture
{
    public string name;
    public float intensity;
    public float blendTime;
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
    public string state;
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
