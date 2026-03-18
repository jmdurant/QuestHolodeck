# Joy Model Pipeline: Blender to Unity

## Blender Source

Joy is a Rigify-rigged character modeled in Blender's standard coordinate system:
- **Forward**: -Y (character faces -Y)
- **Up**: Z
- **Armature**: `rig_joy` (Rigify-generated, 600+ bones including IK/MCH/ORG helpers)
- **Meshes**: body, eye, hair_01, clothing variants — parented to `rig_joy` via Armature modifier

### Blend File

`uploads_files_4104286_joy_v1.5_sportswear_packed.blend` with textures packed inside. Separate texture PNGs in the `textures/` folder alongside it.

### Outfit Collections

| # | Collection | Meshes |
|---|-----------|--------|
| 01 | `01_bikini` | bikini |
| 02 | `02_racerback_crop_high_waist` | racerback_crop_sport, high_waist_leggings, shoes |
| 03 | `03_longline_padded_bike_short` | longline_wirefree_padded, bike_short, shoes |
| 04 | `04_volleyball_jersey` | volleyball_jersey_top/bottom, shoes |
| 05 | `05_workout_tanktop` | padded_workout_tanktop, workout_short, shoes |
| 06 | `06_beach_volleyball` | beach_volleyball_top/bottom, shoes |

Core meshes (always exported): `body`, `eye`, `hair_01` or `hair_02`.

## FBX Export

### Pre-Export Steps

1. **Apply rotations** on all meshes and the armature (`Ctrl+A > Apply Rotation`). Some meshes (body, eye, leggings) have a 90° X rotation from modeling. If not applied, the FBX axis conversion produces inconsistent orientations.

2. **Fix bone-parented meshes**. `hair_01` is parented to bone `DEF-spine.006` (`parent_type=BONE`). The FBX exporter drops bone-parented meshes. Fix by changing to `parent_type=OBJECT` while keeping the Armature modifier:
   ```python
   hair.parent_type = 'OBJECT'
   hair.parent_bone = ''
   ```

3. **Set visibility** for the desired outfit. Hide meshes from other outfits via `hide_viewport`.

### Export Settings

```python
bpy.ops.export_scene.fbx(
    filepath=output_path,
    use_selection=True,
    embed_textures=True,          # Textures inside FBX for Unity extraction
    path_mode='COPY',
    object_types={'MESH', 'ARMATURE'},
    mesh_smooth_type='FACE',
    add_leaf_bones=False,         # Rigify already has leaf bones
    bake_anim=False,              # No animations in this export
    apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z',            # Standard Blender-to-Unity axis mapping
    axis_up='Y',
    bake_space_transform=False,   # Do NOT bake — breaks skinned mesh bones
)
```

### The rig_joy (270, 0, 0) Rotation

The FBX exporter adds a -90° X rotation (`270` in Unity euler) to the armature root to convert from Blender's Z-up to Unity's Y-up coordinate system. This is **unavoidable** and **expected** for Blender Rigify armatures.

**What it means:**
- `rig_joy` child of `JoyPartner` always has `localRotation = (270, 0, 0)`
- The bone hierarchy operates in this rotated coordinate space
- The parent `JoyPartner` transform is at identity — this is the control point for positioning/rotating the model

**What it does NOT mean:**
- The model is broken or incorrectly exported
- You need to compensate with runtime rotation offsets
- `bake_space_transform=True` will fix it (it breaks skinning)

### Model Orientation in Unity

At identity rotation on the `JoyPartner` root:
- **Model faces +Z** (confirmed via surround screenshot test)
- `Quaternion.LookRotation(directionToCamera)` correctly faces the model toward the camera
- No yaw offset needed

## Unity Import Pipeline

### Step 1: FBX Import + Texture Extraction

Run `Tools > Extract Joy FBX Textures and Materials` (editor script `ExtractFBXMaterials.cs`):
- Calls `ModelImporter.ExtractTextures()` to pull embedded PNGs into `Assets/Models/Joy/Textures/`
- Sets `materialLocation = External` so Unity creates `.mat` files in `Assets/Models/Joy/Materials/`
- Some body textures (body_d, head_d, arm_d, etc.) don't extract from the FBX — copy them manually from the downloaded textures folder

### Step 2: Material Wiring

Run `Tools > Wire Joy Materials` (editor script `WireJoyMaterials.cs`):
- Sets all materials to `Universal Render Pipeline/Lit` shader
- Maps diffuse (`_d`), normal (`_n`), roughness (`_r`), metallic (`_m`) textures to URP properties
- Configures hair/eyelash as alpha cutout (cutoff 0.15)
- Configures cornea as fully transparent (glossy eye overlay)
- Creates missing materials (genital, hairtie, gum_tongue) that aren't in every outfit

### Step 3: Scene Setup

1. Instantiate the FBX as `JoyPartner` under `AvatarSystem`
2. Add an `Animator` component (FBX import doesn't add one without an Avatar configured)
3. `JoyBodyController.AutoBind()` finds the model by name and resolves bones at runtime

## Conversation Stage

```
Top-down view:

    Camera/User ──── 0.8m ────→ Joy (facing camera)
         👤          flatFwd          ☺
    (wherever the                 feet on floor (y=0)
     headset is)                  LookRotation(toCamera)
```

`JoyBodyController.TryApplyConversationStage()`:
1. Gets `Camera.main` position and forward direction
2. Projects forward onto the floor plane (removes Y component)
3. Places Joy `conversationDistance` meters ahead along that direction
4. Sets `y = 0` (feet on floor)
5. Faces Joy toward the camera via `LookRotation(camPos - joyPos)`
6. Captures the pose as `baseRootLocalPosition`/`baseRootRotation` so `base.Tick` maintains it

### Head Tracking

`headYawReferenceDegrees = 0` — the head bone's local forward aligns with the model's face direction at rest. The base class `ApplyHeadLookTarget` computes yaw relative to this reference, clamps it to ±70°, and smoothly rotates the head toward the user's tracked position.

The old value of `180` was from a previous model export where the bone-local forward was inverted. After applying rotations in Blender before export, the bone space aligns correctly and the reference is `0`.

## Common Pitfalls

| Problem | Cause | Fix |
|---------|-------|-----|
| Model is magenta | URP pipeline asset not assigned in Graphics Settings | Run `Tools > Setup URP Pipeline` |
| Hair missing from FBX | `hair_01` is bone-parented (`parent_type=BONE`) | Change to `parent_type=OBJECT` before export |
| Bones not binding at runtime | Unity "fake null" — serialized destroyed references | `ClearDestroyedBoneReferences()` in base class |
| Head turns away from user | `headYawReferenceDegrees` wrong | Set to `0` for models with applied rotations |
| Materials still on Standard shader | Material not in WireJoy mapping dictionary | Add to mapping so it gets URP/Lit assigned |
| Textures missing after FBX extract | Some textures don't extract from embedded FBX | Copy from source textures folder manually |
| Hair bangs thin/sparse | URP defaults to backface culling; hair cards are single-plane | Set `_Cull = 0` (Off) for hair materials — matches Blender `backface_culling=False` |
