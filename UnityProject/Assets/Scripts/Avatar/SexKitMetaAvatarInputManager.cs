using Oculus.Avatar2;
using UnityEngine;
using Node = UnityEngine.XR.XRNode;

public class SexKitMetaAvatarInputManager : OvrAvatarInputManager
{
    [SerializeField] private OVRCameraRig cameraRig;

    public void SetCameraRig(OVRCameraRig rig)
    {
        cameraRig = rig;
    }

    protected override void OnTrackingInitialized()
    {
        var trackingDelegate = new TrackingDelegate(cameraRig);
        _inputTrackingProvider = new OvrAvatarInputTrackingDelegatedProvider(trackingDelegate);
        _inputControlProvider = new OvrAvatarInputControlDelegatedProvider(new ControlDelegate());
    }

    private sealed class TrackingDelegate : OvrAvatarInputTrackingDelegate
    {
        private readonly OVRCameraRig _cameraRig;

        public TrackingDelegate(OVRCameraRig cameraRig)
        {
            _cameraRig = cameraRig;
        }

        public override bool GetRawInputTrackingState(out OvrAvatarInputTrackingState inputTrackingState)
        {
            inputTrackingState = default;

            bool leftControllerActive = false;
            bool rightControllerActive = false;
            if (OVRInput.GetActiveController() != OVRInput.Controller.Hands)
            {
                leftControllerActive = OVRInput.GetControllerOrientationTracked(OVRInput.Controller.LTouch);
                rightControllerActive = OVRInput.GetControllerOrientationTracked(OVRInput.Controller.RTouch);
            }

            if (_cameraRig != null)
            {
                inputTrackingState.headsetActive = true;
                inputTrackingState.leftControllerActive = leftControllerActive;
                inputTrackingState.rightControllerActive = rightControllerActive;
                inputTrackingState.leftControllerVisible = false;
                inputTrackingState.rightControllerVisible = false;
                inputTrackingState.headset = (CAPI.ovrAvatar2Transform)_cameraRig.centerEyeAnchor;
                inputTrackingState.leftController = (CAPI.ovrAvatar2Transform)_cameraRig.leftHandAnchor;
                inputTrackingState.rightController = (CAPI.ovrAvatar2Transform)_cameraRig.rightHandAnchor;
                return true;
            }

            if (!OVRNodeStateProperties.IsHmdPresent())
                return false;

            inputTrackingState.headsetActive = true;
            inputTrackingState.leftControllerActive = leftControllerActive;
            inputTrackingState.rightControllerActive = rightControllerActive;
            inputTrackingState.leftControllerVisible = true;
            inputTrackingState.rightControllerVisible = true;

            if (OVRNodeStateProperties.GetNodeStatePropertyVector3(
                    Node.CenterEye,
                    NodeStatePropertyType.Position,
                    OVRPlugin.Node.EyeCenter,
                    OVRPlugin.Step.Render,
                    out var headPos))
            {
                inputTrackingState.headset.position = headPos;
            }

            if (OVRNodeStateProperties.GetNodeStatePropertyQuaternion(
                    Node.CenterEye,
                    NodeStatePropertyType.Orientation,
                    OVRPlugin.Node.EyeCenter,
                    OVRPlugin.Step.Render,
                    out var headRot))
            {
                inputTrackingState.headset.orientation = headRot;
            }

            inputTrackingState.headset.scale = Vector3.one;
            inputTrackingState.leftController.position = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
            inputTrackingState.rightController.position = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
            inputTrackingState.leftController.orientation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch);
            inputTrackingState.rightController.orientation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);
            inputTrackingState.leftController.scale = Vector3.one;
            inputTrackingState.rightController.scale = Vector3.one;
            return true;
        }
    }

    private sealed class ControlDelegate : OvrAvatarInputControlDelegate
    {
        public override bool GetInputControlState(out OvrAvatarInputControlState inputControlState)
        {
            inputControlState = new OvrAvatarInputControlState
            {
                type = GetControllerType()
            };

            UpdateControllerInput(ref inputControlState.leftControllerState, OVRInput.Controller.LTouch);
            UpdateControllerInput(ref inputControlState.rightControllerState, OVRInput.Controller.RTouch);
            return true;
        }

        private static void UpdateControllerInput(ref OvrAvatarControllerState controllerState, OVRInput.Controller controller)
        {
            controllerState.buttonMask = 0;
            controllerState.touchMask = 0;

            if (OVRInput.Get(OVRInput.Button.One, controller))
                controllerState.buttonMask |= CAPI.ovrAvatar2Button.One;
            if (OVRInput.Get(OVRInput.Button.Two, controller))
                controllerState.buttonMask |= CAPI.ovrAvatar2Button.Two;
            if (OVRInput.Get(OVRInput.Button.Three, controller))
                controllerState.buttonMask |= CAPI.ovrAvatar2Button.Three;
            if (OVRInput.Get(OVRInput.Button.PrimaryThumbstick, controller))
                controllerState.buttonMask |= CAPI.ovrAvatar2Button.Joystick;

            if (OVRInput.Get(OVRInput.Touch.One, controller))
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.One;
            if (OVRInput.Get(OVRInput.Touch.Two, controller))
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.Two;
            if (OVRInput.Get(OVRInput.Touch.PrimaryThumbstick, controller))
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.Joystick;
            if (OVRInput.Get(OVRInput.Touch.PrimaryThumbRest, controller))
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.ThumbRest;

            controllerState.indexTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller);
            if (OVRInput.Get(OVRInput.Touch.PrimaryIndexTrigger, controller))
            {
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.Index;
            }
            else if (controllerState.indexTrigger <= 0f)
            {
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.Pointing;
            }

            controllerState.handTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, controller);

            if ((controllerState.touchMask & (CAPI.ovrAvatar2Touch.One |
                                              CAPI.ovrAvatar2Touch.Two |
                                              CAPI.ovrAvatar2Touch.Joystick |
                                              CAPI.ovrAvatar2Touch.ThumbRest)) == 0)
            {
                controllerState.touchMask |= CAPI.ovrAvatar2Touch.ThumbUp;
            }
        }
    }
}
