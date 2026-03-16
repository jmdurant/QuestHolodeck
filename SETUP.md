# SexKit Quest App — Setup Guide

## Prerequisites

- **Unity 6** (required for Meta XR SDK v74+)
- **Meta XR All-in-One SDK v85+** (current as of Feb 2026)
- **Unity OpenXR Plugin** (required, replaces legacy Oculus XR Plugin)
- **Unity XR Hands package** (for OpenXR hand tracking)
- **TextMeshPro** (for UI text — usually included in Unity by default)
- Meta Avatars SDK (optional, for premium avatar rendering)
- Meta Quest 3 or Quest 3S
- SexKit running on iPhone with LiveStream server enabled

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
  com.meta.xr.sdk.avatars        (Meta Avatars SDK — optional for premium avatars)

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

### 3. Import SexKit Scripts

Copy the entire `Scripts/` folder into your Unity project's `Assets/` folder:

```
Assets/
├── Scripts/
│   ├── Network/
│   │   ├── SexKitWebSocketClient.cs    — WebSocket + auto-reconnect
│   │   ├── LiveFrame.cs                — Data model (matches SexKit JSON)
│   │   ├── QuestTrackingUpstream.cs    — Sends Quest tracking back to iPhone
│   │   └── BonjourDiscovery.cs         — Auto-discovers iPhone on network
│   ├── Avatar/
│   │   ├── SexKitAvatarDriver.cs       — 3-mode body rendering + interpolation
│   │   ├── MetaAvatarBridge.cs         — Meta Avatars SDK + Humanoid + finger mapping
│   │   ├── AIAgentController.cs        — 4 intelligence modes + eye contact
│   │   ├── QuestTrackingMerge.cs       — Merges Quest head/hands with SexKit body
│   │   └── HapticDeviceBridge.cs       — BLE haptic device protocols (stub)
│   ├── Room/
│   │   ├── RoomMeshLoader.cs           — LiDAR mesh import + bed placement
│   │   └── PassthroughManager.cs       — Quest 3 mixed reality setup
│   └── UI/
│       ├── ConnectionUI.cs             — Connection screen + auto-discover
│       ├── SexKitHUD.cs                — Floating head-tracked HUD
│       └── SpatialAudioManager.cs      — 3D positioned voice from Body B
```

### 4. Scene Setup — Step by Step

Create a new scene called "SexKitScene". Build the hierarchy exactly as shown:

#### Step 1: Core Manager

```
Hierarchy: Create Empty → name "SexKitManager"

Add Components:
  ✅ SexKitWebSocketClient
  ✅ UnityMainThreadDispatcher
  ✅ BonjourDiscovery

Inspector:
  SexKitWebSocketClient:
    - serverAddress: (leave blank — Bonjour will fill it)
    - autoReconnect: ✅
  BonjourDiscovery:
    - serviceType: _sexkit-stream._tcp.
    - scanTimeout: 10
```

#### Step 2: Connection UI

```
Hierarchy: Create → UI → Canvas → name "ConnectionCanvas"
  Set Canvas: Render Mode = Screen Space - Overlay

Children of ConnectionCanvas:
  - Panel "ConnectPanel"
    - TMP_InputField "AddressInput" (placeholder: "ws://192.168.1.5:8080")
    - Button "ConnectButton" (text: "Connect")
    - Button "ScanButton" (text: "Scan Network")
    - TextMeshPro "StatusText"
  - Panel "ConnectedPanel" (starts inactive)
    - TextMeshPro "FrameCountText"
    - TextMeshPro "DataPreviewText"
    - Button "DisconnectButton" (text: "Disconnect")

Add Component to ConnectionCanvas:
  ✅ ConnectionUI

Inspector — wire references:
  ConnectionUI:
    - addressInput → AddressInput
    - connectButton → ConnectButton
    - disconnectButton → DisconnectButton
    - scanButton → ScanButton
    - statusText → StatusText
    - frameCountText → FrameCountText
    - dataPreviewText → DataPreviewText
    - connectPanel → ConnectPanel
    - connectedPanel → ConnectedPanel
    - bonjourDiscovery → SexKitManager.BonjourDiscovery
```

#### Step 3: Avatar System

```
Hierarchy: Create Empty → name "AvatarSystem"

Add Components:
  ✅ SexKitAvatarDriver
  ✅ MetaAvatarBridge
  ✅ AIAgentController

Inspector:
  SexKitAvatarDriver:
    - mode: Primitive (start here, upgrade to Humanoid/MetaAvatar later)
    - sceneOffset: (0, 0, -2)  — place scene 2m in front of user

  MetaAvatarBridge:
    - useMetaAvatars: ❌ (until Meta Avatars SDK is imported)
    - humanoidAnimatorA: (assign after importing character model)
    - humanoidAnimatorB: (assign after importing character model)
    - questTracking: → TrackingMerge.QuestTrackingMerge (after step 4)

  AIAgentController:
    - mode: RuleBased (start here)
    - userTracking: → TrackingMerge.QuestTrackingMerge (after step 4)
    - avatarDriver: → this.SexKitAvatarDriver
    - enableEyeContact: ✅
```

#### Step 4: Quest Tracking

```
Hierarchy: Create Empty → name "TrackingMerge"

Add Components:
  ✅ QuestTrackingMerge
  ✅ QuestTrackingUpstream

Inspector:
  QuestTrackingMerge:
    - questHeadTransform: → OVRCameraRig/TrackingSpace/CenterEyeAnchor
    - mergeHeadTracking: ✅
    - mergeHandTracking: ✅
    - enableEyeTracking: ❌ (Quest 3 has no eye tracking — enable for Quest Pro)

  QuestTrackingUpstream:
    - tracking: → this.QuestTrackingMerge
    - wsClient: → SexKitManager.SexKitWebSocketClient
    - sendUpstream: ✅
    - sendRate: 30
```

#### Step 5: Room + Passthrough

```
Hierarchy: Create Empty → name "RoomEnvironment"

Add Components:
  ✅ RoomMeshLoader
  ✅ PassthroughManager

Inspector:
  RoomMeshLoader:
    - roomMeshPrefab: (assign imported LiDAR mesh, or leave null for passthrough-only)
    - usePassthrough: ✅

  PassthroughManager:
    - enablePassthrough: ✅
    - ovrManager: → OVRCameraRig.OVRManager
    - cameraRig: → OVRCameraRig
```

#### Step 6: Spatial HUD

```
Hierarchy: Create → UI → Canvas → name "HUDCanvas"
  Set Canvas: Render Mode = World Space
  Set Scale: (0.001, 0.001, 0.001)  — world space UI scaling

Children of HUDCanvas:
  - TextMeshPro "HeartRateText"
  - TextMeshPro "PositionText"
  - TextMeshPro "TimerText"
  - TextMeshPro "IntensityText"
  - TextMeshPro "ConnectionText"
  - TextMeshPro "DataSourceText"

Add Component to HUDCanvas:
  ✅ SexKitHUD

Inspector:
  SexKitHUD:
    - heartRateText → HeartRateText
    - positionText → PositionText
    - timerText → TimerText
    - intensityText → IntensityText
    - connectionText → ConnectionText
    - dataSourceText → DataSourceText
    - followHead: ✅
    - distanceFromHead: 1.5
    - heightOffset: -0.3
```

#### Step 7: Spatial Audio

```
Hierarchy: Create Empty → name "AgentAudio"

Add Components:
  ✅ AudioSource (standard Unity, NOT OVRAudioSource which is deprecated)
  ✅ SpatialAudioManager

Inspector:
  AudioSource:
    - Spatial Blend: 1.0 (fully 3D)
    - Min Distance: 0.5
    - Max Distance: 5

  SpatialAudioManager:
    - agentVoice: → this.AudioSource
    - agentController: → AvatarSystem.AIAgentController
    - spatializeVoice: ✅
```

#### Step 8: OVRCameraRig (Meta XR)

```
Hierarchy: Add the OVRCameraRig prefab from Meta XR Core SDK
  (Assets/Oculus/VR/Prefabs/OVRCameraRig)

Ensure OVRManager component has:
  - isInsightPassthroughEnabled: ✅
  - Tracking Origin Type: Floor Level
```

#### Step 9: Wire Cross-References

Now go back and connect the cross-references that depend on other objects:

```
AvatarSystem → MetaAvatarBridge:
  - questTracking → TrackingMerge.QuestTrackingMerge

AvatarSystem → AIAgentController:
  - userTracking → TrackingMerge.QuestTrackingMerge

TrackingMerge → QuestTrackingMerge:
  - questHeadTransform → OVRCameraRig/TrackingSpace/CenterEyeAnchor

RoomEnvironment → PassthroughManager:
  - ovrManager → OVRCameraRig.OVRManager
  - cameraRig → OVRCameraRig
```

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

### 6. Room Mesh Import (Optional)

If using a LiDAR room scan from SexKit iPhone (passthrough is the alternative):

1. On iPhone: Settings → Room Backdrop → export room scan
2. Transfer .obj file to PC
3. Import into Unity (Assets → Import New Asset)
4. Create prefab from imported mesh
5. Assign prefab to RoomMeshLoader → roomMeshPrefab

### 7. Character Model Import

For Humanoid avatar mode (recommended: JOY by BGShop):

1. Import FBX into Unity
2. In Import Settings → Rig → Animation Type: **Humanoid**
3. Click **Configure** → verify bone mapping (green = auto-detected)
4. If bones aren't auto-detected, manually map using the SexKit joint names:
   ```
   head → Head, neck → Neck, spine → Spine, hip → Hips
   leftShoulder → LeftUpperArm, leftElbow → LeftLowerArm, leftWrist → LeftHand
   (same for right side)
   leftHip → LeftUpperLeg, leftKnee → LeftLowerLeg, leftAnkle → LeftFoot
   (same for right side)
   ```
5. Create two instances in scene (Body A + Body B)
6. Add Animator component to each
7. Assign to MetaAvatarBridge → humanoidAnimatorA / humanoidAnimatorB
8. Set SexKitAvatarDriver → mode: UnityHumanoid

### 8. Meta Avatars (Premium — Optional)

If using Meta Avatars SDK instead of Humanoid:
1. Import `com.meta.xr.sdk.avatars` via Package Manager
2. In `MetaAvatarBridge.cs`, uncomment the OvrAvatarEntity fields and code
3. Add OvrAvatarEntity components to two GameObjects
4. Assign to MetaAvatarBridge → avatarA / avatarB
5. Set MetaAvatarBridge → useMetaAvatars: ✅
6. Set SexKitAvatarDriver → mode: MetaAvatar

### 9. Testing

```
1. iPhone: Open SexKit → More → Settings → Live Stream → Enable Server
   Note the address shown (e.g., "192.168.1.5:8080")

2. Quest: Build and Run (File → Build and Run)
   - App opens to Connection screen
   - "Scanning for SexKit on your network..." (Bonjour auto-discovery)
   - If found: auto-fills address, tap Connect
   - If not found: enter IP:Port manually, tap Connect

3. Verify:
   - Status shows "Connected"
   - Frame count incrementing
   - Data preview shows HR, position, phase
   - Bodies appear in scene (primitive spheres or character model)
   - HUD floating in front of you with session data
   - Passthrough shows your real room (if enabled)

4. Start a session on iPhone/Watch:
   - Bodies start moving with live data
   - Position changes reflected in real-time
   - Verbal cues play from Body B's spatial position (if audio configured)
```

## Architecture

```
iPhone (SexKit)                      Quest 3
├── Watch sensors                    ├── SexKitWebSocketClient
├── Position detection               │   └── Receives LiveFrame JSON (30fps)
├── Partner inference                 ├── BonjourDiscovery
├── Biometric pacing engine          │   └── Auto-finds iPhone on network
├── LiveStream WebSocket ──Wi-Fi──→  ├── ConnectionUI
│   (30fps, Bonjour advertised)      │   └── Connect/disconnect + status
└── All ML/intelligence              ├── SexKitAvatarDriver
                                     │   └── Drives 2 body avatars
                                     ├── MetaAvatarBridge
                                     │   └── Humanoid/Meta Avatar + 47 bones
                                     ├── AIAgentController
                                     │   └── 4 modes + eye contact + gaze
                                     ├── QuestTrackingMerge
                                     │   └── Head + hands merged with body
                                     ├── QuestTrackingUpstream
                                     │   └── Sends Quest tracking → iPhone
                                     ├── RoomMeshLoader + PassthroughManager
                                     │   └── Real room via passthrough or mesh
                                     ├── SexKitHUD
                                     │   └── Floating head-tracked data
                                     └── SpatialAudioManager
                                         └── Voice from Body B's position
```

## Two-Way WebSocket Protocol

```
iPhone → Quest (30fps):
  LiveFrame JSON with: skeleton, HR, HRV, respiratory rate,
  position, intensity, rhythm, pacing phase, verbal cues,
  bed calibration, UWB spatial, partner inference state

Quest → iPhone (30fps):
  QuestTrackingFrame JSON with: head 6DOF, hand joints (26 per hand),
  gaze direction (Quest Pro only), tracking confidence
```

## Avatar Modes

| Mode | Rendering | Quality | Fingers | Setup |
|------|-----------|---------|---------|-------|
| Primitive | Spheres + lines | Basic | No | No extra setup |
| Unity Humanoid | Character model (FBX) | Good | Yes (47 bones) | Import model, set Humanoid rig |
| Meta Avatar | Meta's avatars | Best | Yes + face | Import Meta Avatars SDK |

## Stub Status

These features have the architecture/protocols defined but need platform-specific implementation:

| Feature | Status | What's needed |
|---------|--------|---------------|
| BLE Haptics | Protocol stubs | Android Bluetooth API via AndroidJavaObject |
| Meta Avatars | Falls back to Humanoid | Uncomment code when SDK imported |
| Eye tracking | Quest Pro only | Enable in QuestTrackingMerge for Quest Pro |
| TTS voice | Debug.Log stub | Android TTS or pre-generated audio clips |

## Files

| File | Lines | Purpose |
|------|-------|---------|
| **Network** | | |
| SexKitWebSocketClient.cs | ~140 | WebSocket connection + auto-reconnect |
| LiveFrame.cs | ~135 | Full data model (45+ fields) |
| QuestTrackingUpstream.cs | ~90 | Two-way: Quest → iPhone tracking |
| BonjourDiscovery.cs | ~160 | Auto-discover iPhone via mDNS |
| **Avatar** | | |
| SexKitAvatarDriver.cs | ~180 | Three-mode body rendering + interpolation |
| MetaAvatarBridge.cs | ~230 | Humanoid + Meta Avatar + 47 bone mapping |
| AIAgentController.cs | ~340 | 4 intelligence modes + eye contact system |
| QuestTrackingMerge.cs | ~120 | Merge Quest head/hands with SexKit body |
| HapticDeviceBridge.cs | ~120 | BLE haptic protocols (stub) |
| **Room** | | |
| RoomMeshLoader.cs | ~100 | LiDAR mesh + bed placement |
| PassthroughManager.cs | ~70 | Quest 3 mixed reality |
| **UI** | | |
| ConnectionUI.cs | ~140 | Connection screen + Bonjour + status |
| SexKitHUD.cs | ~120 | Floating spatial HUD |
| SpatialAudioManager.cs | ~70 | 3D positioned voice |
| **Total** | **~2,200** | **Complete Quest app** |
