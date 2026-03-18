using UnityEngine;
using UnityEditor;
using System.IO;

public static class ExtractFBXMaterials
{
    [MenuItem("Tools/Extract Joy FBX Textures and Materials")]
    public static void Extract()
    {
        string fbxPath = "Assets/Models/Joy/joy_v1_5_sportswear.fbx";
        string texturePath = "Assets/Models/Joy/Textures";
        string materialPath = "Assets/Models/Joy/Materials";

        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("[ExtractFBX] Could not find ModelImporter at " + fbxPath);
            return;
        }

        // Extract textures
        importer.ExtractTextures(texturePath);
        AssetDatabase.Refresh();
        Debug.Log("[ExtractFBX] Textures extracted to " + texturePath);

        // Set material import to use external materials (extract)
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.SaveAndReimport();
        Debug.Log("[ExtractFBX] Materials set to external mode, reimporting...");

        // Move extracted materials to the Materials folder
        var generatedMats = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Models/Joy" });
        int moved = 0;
        foreach (var guid in generatedMats)
        {
            var matPath = AssetDatabase.GUIDToAssetPath(guid);
            if (matPath.Contains("/Materials/")) continue; // already in target
            var fileName = Path.GetFileName(matPath);
            var destPath = materialPath + "/" + fileName;
            if (!File.Exists(destPath))
            {
                var result = AssetDatabase.MoveAsset(matPath, destPath);
                if (string.IsNullOrEmpty(result))
                {
                    moved++;
                    Debug.Log("[ExtractFBX] Moved material: " + fileName);
                }
                else
                {
                    Debug.LogWarning("[ExtractFBX] Failed to move " + fileName + ": " + result);
                }
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[ExtractFBX] Done! Moved {moved} materials to {materialPath}");
    }
}
