# ControlFrame Framework

This repo now has a separate partner-control pipeline for AI-driven actions.

## Intent

`LiveFrame` and `ControlFrame` are different concerns:

- `LiveFrame`: observed state coming from capture and session telemetry
- `ControlFrame`: intended partner behavior coming from the iOS/LLM side

The Quest app is responsible for embodiment. It should not receive raw bone commands from the LLM. It should receive semantic commands and translate them into avatar behavior locally.

## Runtime Flow

1. iOS/backend receives live session data over the existing pipeline.
2. The LLM evaluates context on a slow cadence, for example every 5 seconds.
3. The LLM emits a `ControlFrame`.
4. The Quest websocket client receives that `ControlFrame`.
5. `PartnerCommandBus` forwards it to `PartnerDirector`.
6. `PartnerDirector` translates the command into body, face, and voice actions.
7. The avatar-specific controllers drive the actual rig.

## Components

### Network

- `UnityProject/Assets/Scripts/Network/ControlFrame.cs`
- `UnityProject/Assets/Scripts/Network/SexKitWebSocketClient.cs`

`SexKitWebSocketClient` now routes both `LiveFrame` and `ControlFrame` messages.

Supported message shapes:

- direct `LiveFrame`
- direct `ControlFrame`
- envelope with `liveFrame`
- envelope with `controlFrame`
- envelope with `messageType` or `type` set to `live` or `control`

### Partner Runtime

- `UnityProject/Assets/Scripts/Avatar/Partner/PartnerContracts.cs`
- `UnityProject/Assets/Scripts/Avatar/Partner/PartnerCommandBus.cs`
- `UnityProject/Assets/Scripts/Avatar/Partner/PartnerDirector.cs`
- `UnityProject/Assets/Scripts/Avatar/Partner/PartnerBodyController.cs`
- `UnityProject/Assets/Scripts/Avatar/Partner/PartnerFaceController.cs`
- `UnityProject/Assets/Scripts/Avatar/Partner/PartnerVoiceController.cs`

Responsibilities:

- `PartnerCommandBus`: receives semantic commands from websocket
- `PartnerDirector`: owns prioritization, expiry, blending, and action dispatch
- `PartnerBodyController`: pose intent, gaze target, hand targets, gesture routing
- `PartnerFaceController`: expression presets, emotion intensity, jaw/blendshape control
- `PartnerVoiceController`: speech requests and simple mouth-open timing

## ControlFrame Shape

The real iOS payload is sparse and nested. Quest now expects the same shape.

Top-level fields currently supported:

- `type`
- `timestamp`
- `mode`
- `movement`
- `posture`
- `gesture`
- `gaze`
- `verbal`
- `physical`
- `breathing`
- `reaction`
- `expression`
- `environment`

Example:

```json
{
  "type": "control",
  "timestamp": 1710523250.0,
  "mode": "physical",
  "gaze": {
    "target": "user_eyes",
    "intensity": 1.0,
    "behavior": "intense_contact"
  },
  "verbal": {
    "text": "don't stop...",
    "emotion": "breathless",
    "urgency": 0.8
  },
  "physical": {
    "position": "Missionary",
    "rhythmHz": 1.8,
    "intensity": 0.7,
    "amplitude": 0.5
  },
  "breathing": {
    "rate": 28.0,
    "depth": 0.7,
    "pattern": "panting",
    "audible": true
  },
  "expression": {
    "expression": "pleasure",
    "intensity": 0.6,
    "blendTime": 0.5
  }
}
```

## What Lives Where

### iOS / LLM Side

Owns:

- context evaluation
- dialogue and decision-making
- deciding which semantic action should happen next

Does not own:

- bones
- blendshapes
- IK
- per-frame motion

### Quest App

Owns:

- translating semantic actions into embodiment
- blending and interrupting actions safely
- attaching those actions to the current avatar rig
- keeping the animation stable between LLM updates

## How To Connect To Your Model

The default controllers are intentionally generic and minimal. They are the framework, not the final Joy-specific rig driver.

To connect your actual model:

1. Keep `PartnerDirector` as the decision layer.
2. Replace or extend `PartnerBodyController` with a Joy-specific body controller.
3. Replace or extend `PartnerFaceController` with a Joy-specific face controller.
4. Map semantic commands to real bones and blendshapes for that rig.

Expected next step for Joy:

- assign the partner model root
- assign head, chest, and hand bones
- assign facial blendshapes or face bones
- map presets like `SoftSmile`, `Warm`, and `Teasing` to the rig

## Design Rule

The LLM should issue sparse semantic commands in the nested control schema.

Good:

- `gaze.target = user_eyes`
- `expression.expression = pleasure`
- `verbal.text = "right there..."`
- `physical.position = Missionary`
- `breathing.pattern = panting`

Bad:

- raw per-bone transforms
- direct jaw bone rotations
- frame-by-frame animation commands from the backend

## Current Status

Implemented:

- `ControlFrame` schema
- websocket routing
- command bus
- director
- Joy-specific body/face embodiment
- scene generation hookup in `QuestHolodeckProjectSetup`

Not implemented yet:

- production lip-sync / TTS
- rich gesture library
