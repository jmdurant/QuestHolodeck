# QuestHolodeck

## What This Is

Meta Quest 3 VR intimate wellness app (Unity 6 + URP). Pairs with the **SexKit** iOS companion app via WebSocket. An AI agent inhabits **Body B** (virtual partner) using a two-layer architecture: WebSocket for perception, MCP for motor control.

## Architecture

- **Two-body system**: Body A = user (Quest headset tracking), Body B = virtual partner (driven by iOS data + AI agent)
- **Two partner models**: Joy (conversation mode, upper-body/face focus) and Ida (activity mode, full-body humanoid)
- **Data flow**: iPhone sensors (30fps LiveFrame) -> AI Agent (perception) -> MCP tool calls (action) -> ControlFrame -> Quest (rendering at 90fps)
- **Singleton access**: `SexKitWebSocketClient.Instance` for network state

## Project Structure

```
UnityProject/Assets/Scripts/
  Avatar/            # Avatar drivers, tracking merge, Meta Avatar bridge
    Partner/         # Joy/Ida body+face controllers, command bus, director
  Network/           # WebSocket client, LiveFrame, ControlFrame, Bonjour
  Camera/            # Observer camera presets, staging controller
  UI/                # HUD, connection screen, speech capture, confirmation
  Room/              # Room mesh, passthrough, environment, scene understanding
  App/               # ExperienceModeController (mode switching)
```

## Key Conventions

- **Unity 6** with Universal Render Pipeline (URP 17.3.0)
- **Meta XR SDK v85** (All-in-One) — use OpenXR APIs, NOT deprecated OVR APIs
- **Deprecated APIs to avoid**: OVRHand/OVRSkeleton (use XR Hands), OVRAudioSource (use Meta XR Audio Source), OVRSceneManager (use MRUK), Oculus XR Plugin (use OpenXR)
- **MonoBehaviour-based** — scripts are Unity components
- **Semantic control**: Agent sends intents ("speak", "gaze_at_user"), NOT raw bone data
- **ControlFrame**: 2-5Hz command cadence from agent; routes through PartnerCommandBus -> PartnerDirector -> avatar controllers
- **LiveFrame**: 30fps sensory stream from iPhone (HR, skeleton, biometrics, spatial data)
- Target: Android ARM64, min API 29, ASTC texture compression
- Bundle ID: `com.docto.questholodeck`

## MCP Integration

Two MCP servers configured in `.mcp.json`:
- **UnityMCP**: Direct Unity editor control (hierarchy, objects, scripts, animation, UI). Requires Unity editor to be running.
- **blender-mcp**: 3D asset generation/editing, PolyHaven/Sketchfab import, AI model generation (Hyper3D, Hunyuan)

## Build & Run

- Open `UnityProject/` in Unity Hub (Unity 6+)
- Single scene: `Assets/Scenes/SexKitScene.unity`
- Build target: Android (Quest 3 / Quest 3S)
- Requires Meta Quest Developer Hub for sideloading

## Key Documentation

- `SETUP.md` — Step-by-step Unity project setup and scene construction
- `CONTROL_PROTOCOL.md` — Full agent control protocol (WebSocket + MCP layers)
- `AVATAR_MODE_PLAN.md` — Joy vs Ida model split strategy
- `CONTROLFRAME_FRAMEWORK.md` — ControlFrame schema and routing
- `OBSERVER_CAMERA_FRAMEWORK.md` — Multi-camera preset system

## Tools

- `tools/export_blend_to_fbx.py` — Blender CLI script for FBX export with armature selection
