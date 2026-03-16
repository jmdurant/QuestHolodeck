# SexKit Quest App — Setup Guide

## Prerequisites

- **Unity 6** (required for Meta XR SDK v74+)
- **Meta XR All-in-One SDK v85+** (current as of Feb 2026)
- **Unity OpenXR Plugin** (required, replaces legacy Oculus XR Plugin)
- **Unity XR Hands package** (for OpenXR hand tracking)
- Meta Avatars SDK (optional, for premium avatar rendering)
- Meta Quest 3 or Quest 3S
- SexKit running on iPhone with LiveStream enabled

## Project Setup

### 1. Create Unity Project

```
Unity Hub → New Project → Universal 3D (URP) → "SexKit Quest"
Requires: Unity 6+
```

### 2. Import Meta SDKs

```
Window → Package Manager → + → Add package by name:
  com.meta.xr.sdk.all           (Meta XR All-in-One v85 — includes all below)
  com.unity.xr.hands             (Unity XR Hands — OpenXR hand tracking)
  com.meta.xr.sdk.avatars        (Meta Avatars SDK — optional)

The All-in-One SDK includes:
  - Core SDK (OVRManager, OVRCameraRig, passthrough)
  - Interaction SDK (hand/controller interaction, OpenXR hand skeleton)
  - Movement SDK (body/face tracking, retargeting)
  - Audio SDK (spatial audio, HRTF — replaces deprecated OVRAudioSource)
  - Haptics SDK
  - Platform SDK
```

### Important: Deprecated APIs to Avoid

| Deprecated | Replacement | Since |
|-----------|-------------|-------|
| OVRHand / OVRSkeleton (hands) | OpenXR hand skeleton via XR Hands | v78 |
| OVRAudioSource | Meta XR Audio Source | v47 |
| OVRSceneManager | MR Utility Kit | v65 |
| Oculus XR Plugin | Unity OpenXR Plugin | v74 |
| GPU Skinning (Avatars) | Compute Skinning (OVR_COMPUTE) | v29 |
| OVRManager.cpuLevel | OVRManager.suggestedCpuPerfLevel | v85 |

### Quest 3 vs Quest Pro Hardware

| Feature | Quest 3 / 3S | Quest Pro |
|---------|-------------|-----------|
| Hand tracking | Yes (OpenXR) | Yes |
| Eye tracking | **NO** | Yes (OVREyeGaze) |
| Passthrough | Yes (color) | Yes (color) |
| Body tracking | Yes (Movement SDK) | Yes |
| LiDAR / depth | No | No |

### 3. Import SexKit Scripts

Copy the `Scripts/` folder from this directory into your Unity project's `Assets/` folder:

```
Assets/
├── Scripts/
│   ├── Network/
│   │   ├── SexKitWebSocketClient.cs    — WebSocket connection to iPhone
│   │   └── LiveFrame.cs                — Data model (matches SexKit JSON)
│   ├── Avatar/
│   │   ├── SexKitAvatarDriver.cs       — Drives body avatars from skeleton data
│   │   ├── MetaAvatarBridge.cs         — Meta Avatars SDK integration
│   │   └── HapticDeviceBridge.cs       — Bluetooth haptic device sync
│   ├── Room/
│   │   └── RoomMeshLoader.cs           — Loads LiDAR room mesh + bed placement
│   └── UI/
│       └── SexKitHUD.cs                — Floating HUD (HR, position, timer)
```

### 4. Scene Setup

Create a new scene called "SexKitScene":

1. **Empty GameObject "SexKitManager"**
   - Add `SexKitWebSocketClient` component
   - Add `UnityMainThreadDispatcher` component
   - Set server address to your iPhone's IP

2. **Empty GameObject "AvatarDriver"**
   - Add `SexKitAvatarDriver` component
   - Set mode to `Primitive` (or `MetaAvatar` if SDK imported)

3. **Empty GameObject "RoomLoader"**
   - Add `RoomMeshLoader` component
   - Assign room mesh prefab (imported OBJ/FBX from SexKit LiDAR scan)

4. **Canvas "HUD"**
   - World Space Canvas
   - Add `SexKitHUD` component
   - Create TextMeshPro elements for HR, position, timer
   - Set `followHead = true`

5. **Optional: "HapticBridge"**
   - Add `HapticDeviceBridge` component
   - Set device type and BLE address

### 5. Build Settings

```
File → Build Settings:
  Platform: Android
  Texture Compression: ASTC
  Minimum API Level: 29 (Android 10)
  Target Architecture: ARM64

Player Settings:
  XR Plug-in Management → OpenXR → Meta Quest Feature Group
  (Do NOT use the legacy Oculus XR Plugin — deprecated for v74+)
  Rendering → Color Space: Linear

Project Settings → XR Plug-in Management:
  Enable "Unity OpenXR" (NOT "Oculus")
  Under OpenXR → Meta Quest Feature Group → enable all
```

### 6. Room Mesh Import

From SexKit iPhone → Settings → Room Backdrop:
1. Export room scan as .obj (or .usdz)
2. Convert to FBX: `usdzconvert room_scan.usdz room_scan.fbx` (or use Blender)
3. Import FBX into Unity
4. Assign as prefab in RoomMeshLoader

### 7. Meta Avatars (Premium)

If using Meta Avatars SDK:
1. Import com.meta.xr.sdk.avatars
2. Uncomment the Meta Avatar code in `MetaAvatarBridge.cs`
3. Add `OvrAvatarEntity` components to two GameObjects
4. Assign to MetaAvatarBridge's avatarA and avatarB fields
5. Joint overrides will drive the avatars from SexKit data

## Architecture

```
iPhone (SexKit)                      Quest 3
├── Watch sensors                    ├── SexKitWebSocketClient
├── Position detection               │   └── Receives LiveFrame JSON
├── Partner inference                 ├── SexKitAvatarDriver
├── LiveStream WebSocket ──Wi-Fi──→  │   └── Drives 2 body avatars
└── All ML/intelligence              ├── MetaAvatarBridge (optional)
                                     │   └── Meta Avatar joint overrides
                                     ├── RoomMeshLoader
                                     │   └── LiDAR mesh or Hyperscape
                                     ├── SexKitHUD
                                     │   └── Floating data display
                                     └── HapticDeviceBridge (optional)
                                         └── BLE device sync
```

## Avatar Modes

| Mode | Rendering | Quality | Setup |
|------|-----------|---------|-------|
| Primitive | Spheres + lines | Basic | No extra setup |
| Unity Humanoid | Standard character model | Good | Import any humanoid FBX |
| Meta Avatar | Meta's photorealistic avatars | Best | Meta Avatars SDK required |

## Passthrough Mixed Reality

Quest 3/3S supports passthrough — see your real room with virtual bodies overlaid:

```csharp
// In your scene's OVRManager:
OVRManager.instance.isInsightPassthroughEnabled = true;

// Enable passthrough layer:
var passthroughLayer = gameObject.AddComponent<OVRPassthroughLayer>();
passthroughLayer.overlayType = OVROverlay.OverlayType.Underlay;
```

With passthrough, the room mesh is optional — your real room IS the environment.

## Testing

1. Start SexKit on iPhone → Settings → enable Live Stream
2. Note the iPhone's IP address and port
3. Build and deploy Quest app
4. Enter IP:Port in the Quest app's connection field
5. Bodies should appear and start moving with live data

## Files

| File | Lines | Purpose |
|------|-------|---------|
| SexKitWebSocketClient.cs | ~140 | WebSocket connection + auto-reconnect |
| LiveFrame.cs | ~110 | Data model matching SexKit JSON schema |
| SexKitAvatarDriver.cs | ~180 | Three-mode avatar rendering + interpolation |
| MetaAvatarBridge.cs | ~130 | Meta Avatars SDK joint override bridge |
| RoomMeshLoader.cs | ~100 | Room mesh + bed placement from calibration |
| SexKitHUD.cs | ~120 | Floating spatial HUD |
| HapticDeviceBridge.cs | ~120 | Bluetooth haptic device protocols |
| **Total** | **~900** | **Complete Quest app** |
