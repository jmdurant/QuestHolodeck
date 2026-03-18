import json
import os
import sys

import bpy


def parse_args():
    if "--" not in sys.argv:
        raise SystemExit("Expected arguments after --")

    args = sys.argv[sys.argv.index("--") + 1 :]
    if len(args) < 2:
        raise SystemExit("Usage: blender -b <blend> --python export_blend_to_fbx.py -- <blend> <fbx> [report_json]")

    blend_path = os.path.abspath(args[0])
    fbx_path = os.path.abspath(args[1])
    report_path = os.path.abspath(args[2]) if len(args) > 2 and args[2] else None
    export_deform_only = True
    if len(args) > 3:
        export_deform_only = args[3].lower() not in {"false", "0", "all_bones"}
    return blend_path, fbx_path, report_path, export_deform_only


def pick_armature():
    armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    if not armatures:
        return None

    return max(armatures, key=lambda obj: len(obj.data.bones))


def meshes_for_armature(armature):
    meshes = []
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue

        if obj.parent == armature:
            meshes.append(obj)
            continue

        for modifier in obj.modifiers:
            if modifier.type == "ARMATURE" and modifier.object == armature:
                meshes.append(obj)
                break

    return meshes


def object_report(armature, meshes):
    return {
        "armature": armature.name if armature else None,
        "bone_count": len(armature.data.bones) if armature else 0,
        "meshes": [obj.name for obj in meshes],
    }


def ensure_object_mode():
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")


def main():
    blend_path, fbx_path, report_path, export_deform_only = parse_args()

    bpy.ops.wm.open_mainfile(filepath=blend_path)
    ensure_object_mode()

    armature = pick_armature()
    meshes = meshes_for_armature(armature) if armature else []

    bpy.ops.object.select_all(action="DESELECT")

    # Apply rotations on all export targets so the FBX has clean transforms
    for obj in meshes + ([armature] if armature else []):
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        obj.select_set(False)

    for obj in meshes:
        obj.select_set(True)
    if armature:
        armature.select_set(True)
        bpy.context.view_layer.objects.active = armature
    elif meshes:
        bpy.context.view_layer.objects.active = meshes[0]

    os.makedirs(os.path.dirname(fbx_path), exist_ok=True)

    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        use_space_transform=True,
        bake_space_transform=False,
        add_leaf_bones=False,
        use_armature_deform_only=export_deform_only,
        mesh_smooth_type="FACE",
        bake_anim=False,
        path_mode="COPY",
        embed_textures=False,
    )

    report = object_report(armature, meshes)
    report["export_deform_only"] = export_deform_only
    print(json.dumps(report, indent=2))

    if report_path:
        os.makedirs(os.path.dirname(report_path), exist_ok=True)
        with open(report_path, "w", encoding="utf-8") as handle:
            json.dump(report, handle, indent=2)


if __name__ == "__main__":
    main()
