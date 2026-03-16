# Observer Camera Framework

The Quest app now has a dedicated observer-camera layer intended for:

- first-person headset play as the primary view
- optional picture-in-picture preview
- third-person preset cameras for recording, casting, or debugging

## Core idea

Keep the player view and observer view separate.

- `OVRCameraRig` remains the live headset view
- `ObserverCameraController` owns one secondary camera
- that camera renders to a `RenderTexture`
- the `RenderTexture` can be shown in PIP or reused later for recording/casting integrations

## Script

- [ObserverCameraController.cs](/C:/Users/docto/QuestHolodeck/UnityProject/Assets/Scripts/Camera/ObserverCameraController.cs)

## Supported presets

- `TopDown`
- `BedsideLeft`
- `BedsideRight`
- `CornerFrontLeft`
- `CornerFrontRight`
- `PartnerEyes`
- `UserShoulder`

These are resolved relative to the bed, partner model, and headset anchors so they work across:

- `Meta + Unity`
- `Unity + Unity`
- `Primitive`

## Runtime controls

The controller exposes methods for future UI wiring:

- `SetObserverEnabled(bool)`
- `SetPipEnabled(bool)`
- `SetPreset(ObserverCameraPreset)`
- `CyclePreset(int direction = 1)`

There are also debug hotkeys for editor/sim use:

- `O` toggles observer camera
- `P` toggles PIP
- `[` and `]` cycle presets

## Current limits

- This adds camera presets and PIP, not a full recording/export pipeline
- On-device recording still needs either Quest capture, external casting, or a dedicated native/plugin path
- Extra cameras cost performance on Quest, so the observer camera defaults should be treated as optional tools

## Meta SDK note

The installed Meta SDK in this project did not provide a turnkey bedside/corner observer-camera system. Meta Avatar and other Meta XR features can coexist with this layer, but the observer presets are implemented as a Unity-side system so they work regardless of avatar mode.
