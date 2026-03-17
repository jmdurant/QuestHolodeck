# Avatar Mode Plan

This document captures the current avatar direction after the Joy runtime investigation and the new Ida import.

## Decision

We are no longer treating one model as responsible for both:

- close-up conversational presence
- full-body physical activity animation

Instead, we split the problem by mode.

## Mode Split

### Joy

Use Joy as the `conversation model`.

Why:

- we already invested in Joy's speaking path
- Joy's face and upper-body presence are the strongest part of the current work
- Joy does not appear to be a practical full-body runtime animation asset for this app

Target presentation:

- face-to-face conversation
- upper torso + face framing
- black or void background by default
- emphasis on:
  - facial expressions
  - eye contact
  - visemes / speech
  - subtle breathing
  - slight head and shoulder motion

### Ida

Use Ida as the `activity model`.

Why:

- Ida is already imported as a Humanoid in Unity
- Ida appears to preserve blendshapes on import
- Ida is much closer to the pipeline needed for:
  - Mixamo
  - DeepMotion
  - humanoid retargeting
  - possible ARKit / NVIDIA face integration

Current evidence:

- [SK_BusinessLady_Nude.fbx.meta](/C:/Users/docto/QuestHolodeck/UnityProject/Assets/IdaFaber/Meshes/Girl/SK_BusinessLady_Nude.fbx.meta) is imported as `Humanoid`
- the same import has `importBlendShapes: 1`
- the asset pack includes:
  - [SK_BusinessLady_Nude.fbx](/C:/Users/docto/QuestHolodeck/UnityProject/Assets/IdaFaber/Meshes/Girl/SK_BusinessLady_Nude.fbx)
  - [SK_BusinessLady_Nude Variant.prefab](/C:/Users/docto/QuestHolodeck/UnityProject/Assets/IdaFaber/Prefabs/Girl/SK_BusinessLady_Nude%20Variant.prefab)
  - [A_Idle_F.fbx](/C:/Users/docto/QuestHolodeck/UnityProject/Assets/IdaFaber/Demo/A_Idle_F.fbx)

## Product Shape

The app can expose two distinct presentation modes.

### Conversation Mode

Primary avatar:

- Joy

Camera / framing:

- tight framing
- face and upper torso
- direct eye-level presentation
- default black / void background

Animation expectations:

- speech
- visemes
- blink
- gaze
- face presets
- subtle idle motion only

### Activity Mode

Primary avatar:

- Ida

Camera / framing:

- full body or configurable observer views

Animation expectations:

- humanoid motion
- position changes
- activity clips
- retargeted or generated animation

Animation sources:

- Mixamo for stock clips / rig normalization
- DeepMotion for prompted body animation
- possible ARKit-compatible face path if Ida's blendshape names validate cleanly

## Why This Split Is Better

### It reduces risk

Joy no longer has to solve:

- full-body runtime rig reliability
- Humanoid compatibility
- Mixamo compatibility
- DeepMotion compatibility

Ida no longer has to carry the entire conversation identity on day one.

### It fits the actual strengths of each asset

Joy:

- expressive speaking presence
- already wired into current face/speech controller work

Ida:

- imported as Humanoid
- better fit for body animation tooling

### It keeps the architecture clean

The app can keep one control system and swap embodiment by mode.

That means:

- `ControlFrame` remains the semantic command layer
- conversation mode maps to Joy controllers
- activity mode maps to Ida controllers

## Proposed Architecture

### Shared Systems

These remain mode-agnostic:

- websocket / `LiveFrame`
- websocket / `ControlFrame`
- `PartnerCommandBus`
- `PartnerDirector`
- environment / room systems
- observer cameras

### Joy-Specific Systems

- `JoyFaceController`
- `JoyBodyController`
- close-up camera framing
- conversation-mode state rules

### Ida-Specific Systems

Planned:

- `IdaBodyController`
- `IdaFaceController` or blendshape driver
- humanoid animation controller
- clip playback / retarget layer

## Practical Plan

### Phase 1: Lock the split

Goal:

- stop forcing Joy to be the full-body activity avatar

Work:

- preserve Joy as conversation embodiment
- preserve Ida as activity embodiment
- make naming explicit in code and docs

### Phase 2: Build Conversation Mode around Joy

Goal:

- make the current Joy work pay off

Work:

- black / void default background
- upper torso framing
- stable close-up camera
- refine:
  - gaze
  - blink
  - visemes
  - expression presets
  - subtle breathing

### Phase 3: Validate Ida for activity

Goal:

- confirm Ida is truly the better runtime body model

Validation checklist:

- confirm Humanoid import is working cleanly in Unity
- inspect actual blendshape names on the imported nude mesh
- confirm the nude prefab is the correct activity base
- confirm body animation clips apply cleanly

### Phase 4: Activity pipeline

Goal:

- make Ida the body-animation runtime asset

Work:

- hook Ida into the app as a separate activity-mode avatar
- test stock idle / locomotion / pose clips
- test Mixamo compatibility if needed
- test DeepMotion pipeline for prompted animations

### Phase 5: Face options for Ida

Goal:

- decide whether Ida also gets modern facial animation

Possible paths:

- use Ida only for body, keep face simpler
- use Ida blendshapes directly
- use NVIDIA Audio2Face-3D if the blendshapes map well enough to ARKit-style output

## Open Questions

### 1. Do we want one character identity or two?

Current working names:

- Joy = conversation model
- Ida = activity model

This is a development naming split only.

### 2. Does Ida's imported blendshape list actually match ARKit naming closely enough?

This is the main technical question for NVIDIA face integration.

### 3. Should Activity Mode reuse the current ControlFrame schema directly?

Likely yes.

But the mapping should be mode-aware:

- conversation mode interprets commands into face-first behavior
- activity mode interprets commands into full-body behavior

## Recommendation

Use this split.

It is the most pragmatic direction we have found:

- Joy becomes useful immediately instead of being a failed full-body experiment
- Ida gives the project a realistic full-body animation path
- the control architecture already built in this repo still applies

This is a much stronger product direction than trying to make Joy do both jobs.
