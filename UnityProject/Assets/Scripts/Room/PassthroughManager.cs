// PassthroughManager.cs
// SexKit Quest App
//
// Manages Quest 3 passthrough mixed reality
// Real room visible with virtual bodies overlaid

using UnityEngine;

public class PassthroughManager : MonoBehaviour
{
    [Header("Settings")]
    public bool enablePassthrough = true;
    public float passthroughOpacity = 1.0f;
    public bool enableDepthOcclusion = true;  // virtual objects behind real objects get hidden

    [Header("References")]
    public OVRManager ovrManager;
    public OVRCameraRig cameraRig;

    private OVRPassthroughLayer _passthroughLayer;

    void Start()
    {
        if (!enablePassthrough) return;
        SetupPassthrough();
    }

    void SetupPassthrough()
    {
        // Enable passthrough on OVRManager
        if (ovrManager != null)
        {
            ovrManager.isInsightPassthroughEnabled = true;
        }

        // Add passthrough layer.
        // Flexible layering fields (overlayType/compositionDepth) are deprecated in current OVR SDK,
        // so rely on default background passthrough behavior.
        _passthroughLayer = gameObject.AddComponent<OVRPassthroughLayer>();

        // Set camera to transparent so passthrough shows through
        if (cameraRig != null)
        {
            var centerCam = cameraRig.centerEyeAnchor.GetComponent<Camera>();
            if (centerCam != null)
            {
                centerCam.clearFlags = CameraClearFlags.SolidColor;
                centerCam.backgroundColor = Color.clear;
            }
        }

        Debug.Log("[SexKit] Passthrough enabled — real room visible");
    }

    /// Toggle between passthrough (mixed reality) and full VR
    public void SetPassthrough(bool enabled)
    {
        enablePassthrough = enabled;
        if (_passthroughLayer != null)
        {
            _passthroughLayer.enabled = enabled;
        }
        if (ovrManager != null)
        {
            ovrManager.isInsightPassthroughEnabled = enabled;
        }
    }

    /// Adjust passthrough opacity (0 = full VR, 1 = full passthrough)
    public void SetOpacity(float opacity)
    {
        passthroughOpacity = Mathf.Clamp01(opacity);
        if (_passthroughLayer != null)
        {
            _passthroughLayer.textureOpacity = passthroughOpacity;
        }
    }
}
