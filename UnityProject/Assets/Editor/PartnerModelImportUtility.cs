using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PartnerModelImportUtility
{
    private const string DefaultModelPath = "Assets/Models/joy_v1_5_sportswear.fbx";
    private const string FullBonesModelPath = "Assets/Models/joy_v1_5_sportswear_fullbones.fbx";

    [MenuItem("Tools/Quest Holodeck/Configure Partner Model")]
    public static void ConfigureDefaultModel()
    {
        ConfigureModel(DefaultModelPath);
    }

    [MenuItem("Tools/Quest Holodeck/Configure Partner Model Full Bones")]
    public static void ConfigureFullBonesModel()
    {
        ConfigureModel(FullBonesModelPath);
    }

    [MenuItem("Tools/Quest Holodeck/Dump Partner Model Bones")]
    public static void DumpDefaultModelBones()
    {
        DumpModelBones(DefaultModelPath);
    }

    public static void ConfigureModel(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[QuestHolodeckSetup] Model importer not found at {assetPath}");
            return;
        }

        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.optimizeGameObjects = false;

        if (assetPath.Contains("joy_v1_5_sportswear"))
        {
            importer.humanDescription = BuildJoyHumanDescription();
        }

        importer.SaveAndReimport();

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        var animator = model != null ? model.GetComponent<Animator>() : null;
        var avatar = animator != null ? animator.avatar : null;

        Debug.Log(
            $"[QuestHolodeckSetup] Partner model configured: path={assetPath}, " +
            $"avatarValid={(avatar != null && avatar.isValid)}, avatarHuman={(avatar != null && avatar.isHuman)}");
    }

    private static HumanDescription BuildJoyHumanDescription()
    {
        return new HumanDescription
        {
            human = new[]
            {
                Human("Hips", "rig_joy"),
                Human("Spine", "spine_fk"),
                Human("Chest", "spine_fk.002"),
                Human("UpperChest", "spine_fk.003"),
                Human("Neck", "DEF-spine.005"),
                Human("Head", "DEF-spine.006"),
                Human("LeftShoulder", "DEF-shoulder.L"),
                Human("RightShoulder", "DEF-shoulder.R"),
                Human("LeftUpperArm", "DEF-upper_arm.L"),
                Human("RightUpperArm", "DEF-upper_arm.R"),
                Human("LeftLowerArm", "DEF-forearm.L"),
                Human("RightLowerArm", "DEF-forearm.R"),
                Human("LeftHand", "DEF-hand.L"),
                Human("RightHand", "DEF-hand.R"),
                Human("LeftUpperLeg", "DEF-thigh.L"),
                Human("RightUpperLeg", "DEF-thigh.R"),
                Human("LeftLowerLeg", "DEF-shin.L"),
                Human("RightLowerLeg", "DEF-shin.R"),
                Human("LeftFoot", "DEF-foot.L"),
                Human("RightFoot", "DEF-foot.R"),
                Human("LeftToes", "DEF-toe.L"),
                Human("RightToes", "DEF-toe.R"),
            },
            skeleton = BuildJoySkeleton(),
            upperArmTwist = 0.5f,
            lowerArmTwist = 0.5f,
            upperLegTwist = 0.5f,
            lowerLegTwist = 0.5f,
            armStretch = 0.05f,
            legStretch = 0.05f,
            feetSpacing = 0f,
            hasTranslationDoF = false,
        };
    }

    private static SkeletonBone[] BuildJoySkeleton()
    {
        var names = new[]
        {
            "rig_joy",
            "spine_fk",
            "spine_fk.002",
            "spine_fk.003",
            "DEF-spine.005",
            "DEF-spine.006",
            "DEF-shoulder.L",
            "DEF-shoulder.R",
            "DEF-upper_arm.L",
            "DEF-upper_arm.R",
            "DEF-forearm.L",
            "DEF-forearm.R",
            "DEF-hand.L",
            "DEF-hand.R",
            "DEF-thigh.L",
            "DEF-thigh.R",
            "DEF-shin.L",
            "DEF-shin.R",
            "DEF-foot.L",
            "DEF-foot.R",
            "DEF-toe.L",
            "DEF-toe.R",
        };

        var bones = new List<SkeletonBone>(names.Length);
        foreach (var name in names)
        {
            bones.Add(new SkeletonBone
            {
                name = name,
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,
            });
        }

        return bones.ToArray();
    }

    private static HumanBone Human(string humanName, string boneName)
    {
        return new HumanBone
        {
            humanName = humanName,
            boneName = boneName,
            limit = new HumanLimit
            {
                useDefaultValues = true,
            },
        };
    }

    public static void DumpModelBones(string assetPath)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (model == null)
        {
            Debug.LogWarning($"[QuestHolodeckSetup] Model asset not found at {assetPath}");
            return;
        }

        var builder = new StringBuilder();
        AppendTransform(model.transform, 0, builder);
        Debug.Log($"[QuestHolodeckSetup] Bone dump for {assetPath}\n{builder}");
    }

    private static void AppendTransform(Transform transform, int depth, StringBuilder builder)
    {
        builder.Append(' ', depth * 2);
        builder.AppendLine(transform.name);

        for (var i = 0; i < transform.childCount; i++)
        {
            AppendTransform(transform.GetChild(i), depth + 1, builder);
        }
    }
}
