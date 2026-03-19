# Joy Staging Caveats

This file records the practical staging rules learned while tuning JOY in Unity.

## Core Mental Model

- JOY is heavily runtime-staged. Edit Mode is only the starting configuration.
- Play Mode is the real behavior. Scripts reposition, rotate, hide, and retune JOY after startup.
- JOY's root behaves more like a feet/base anchor than a torso-center anchor.
- Because of that, position and rotation usually have to be adjusted together for bed poses.

## Mode Rules

- `Conversation` and `Training` are portrait modes.
- `Activity` is a bed-relative mode.
- `Training` currently reuses the same staging path as `Conversation`.

## Conversation / Training

- Do not "fix" eyeline by moving the user camera.
- In VR, the headset camera is the user's real position.
- Use a small head-look offset instead.
- Conversation staging places JOY relative to the headset, not relative to the bed.
- Portrait lighting is mode-specific and should not leak into `Activity`.

## Activity

- Activity mode must explicitly restage JOY when switching from another mode.
- One-time startup staging is not enough, because switching from `Conversation` can leave JOY in the wrong place.
- Activity staging should be based on bed anchors, not portrait placement.
- Bedside side selection and head/feet direction are separate concerns.

## Bed Semantics

- `left` and `right` should mean the user's left/right while lying in bed.
- They should not mean camera-left, room-left, or observer-left.
- The code should use one shared helper for bed-side semantics so cameras and avatar anchors agree.
- In the current fallback bed for this scene:
- pillows / bedside tables mark the headboard end
- the user's current/default bedside is `bed_left`
- `bed_left` maps to `+bedTransform.right`
- `bed_right` maps to `-bedTransform.right`

## Axis / Anchor Behavior

- World axes are normal: left is left, right is right, up is up, down is down.
- The confusing part is the anchor, not the axis system.
- Translating JOY still makes sense.
- The caveat is that the transform acts from a base/feet-like anchor, especially for lying poses.

## Bed Pose Lessons

- A correct bed pose requires all of these to agree:
- side of bed
- headboard vs footboard direction
- face-up vs face-down orientation
- mattress contact height
- Because JOY's root behaves like a feet/base anchor, lying poses should place the root nearer the footboard and rotate the body axis so the head extends toward the headboard.

If any one of those is wrong, the pose will look obviously broken.

## Practical Debug Workflow

- Use actual experience-view captures when judging the result.
- Scene-object captures are useful for pose inspection, but they are not the same as the user-facing camera view.
- If a pose looks wrong:
- verify anchor position
- verify root rotation
- verify head/feet direction along the bed
- verify mattress lift / clipping

## Current Repo Expectations

- `Conversation`: JOY close to the user, portrait presentation.
- `Training`: same default staging as `Conversation`.
- `Activity`: JOY staged relative to the bed, lying on the mattress by default.

## Files To Check

- `UnityProject/Assets/Scripts/Avatar/Partner/JoyBodyController.cs`
- `UnityProject/Assets/Scripts/Avatar/Partner/PartnerBodyController.cs`
- `UnityProject/Assets/Scripts/Avatar/SexKitAvatarDriver.cs`
- `UnityProject/Assets/Scripts/App/ExperienceModeController.cs`
- `UnityProject/Assets/Scripts/Camera/ObserverCameraController.cs`
