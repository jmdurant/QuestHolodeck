// SkyboxMaterialGenerator.cs
// Editor script — auto-creates Skybox/Panoramic materials from
// equirectangular JPGs in Resources/Environments/
//
// Run via: Unity menu → SexKit → Generate Skybox Materials
// Or it runs automatically when JPGs without matching materials are detected.

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class SkyboxMaterialGenerator
{
    private const string EnvironmentsPath = "Assets/Resources/Environments";
    private const string SkyboxShader = "Skybox/Panoramic";

    [MenuItem("SexKit/Generate Skybox Materials")]
    public static void GenerateMaterials()
    {
        if (!Directory.Exists(EnvironmentsPath))
        {
            Debug.LogWarning($"[SkyboxGen] Folder not found: {EnvironmentsPath}");
            return;
        }

        var shader = Shader.Find(SkyboxShader);
        if (shader == null)
        {
            Debug.LogError($"[SkyboxGen] Shader not found: {SkyboxShader}");
            return;
        }

        int created = 0;

        // Find all JPG/PNG textures that start with "Skybox_"
        var files = Directory.GetFiles(EnvironmentsPath, "Skybox_*.*");
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file).ToLower();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".hdr") continue;
            if (file.Contains("_depth")) continue;  // skip depth maps

            var baseName = Path.GetFileNameWithoutExtension(file);
            var matPath = $"{EnvironmentsPath}/{baseName}.mat";

            // Skip if material already exists
            if (File.Exists(matPath))
            {
                Debug.Log($"[SkyboxGen] Material already exists: {baseName}");
                continue;
            }

            // Ensure texture import settings are correct
            var texturePath = file.Replace("\\", "/");
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            // Load texture
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                Debug.LogWarning($"[SkyboxGen] Could not load texture: {texturePath}");
                continue;
            }

            // Create panoramic skybox material
            var material = new Material(shader);
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_Rotation", 0);
            material.SetFloat("_Exposure", 1.0f);

            // Detect HDR vs LDR
            if (ext == ".hdr")
            {
                material.SetFloat("_Mapping", 1);  // Latitude/Longitude Layout
            }

            AssetDatabase.CreateAsset(material, matPath);
            created++;
            Debug.Log($"[SkyboxGen] Created material: {matPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (created > 0)
        {
            Debug.Log($"[SkyboxGen] Generated {created} skybox material(s)");
        }
        else
        {
            Debug.Log("[SkyboxGen] No new materials needed — all up to date");
        }
    }

    // Auto-run when Unity imports new assets in the Environments folder
    class SkyboxImportPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (path.StartsWith(EnvironmentsPath) && path.Contains("Skybox_"))
                {
                    // Delay to ensure texture import is complete
                    EditorApplication.delayCall += GenerateMaterials;
                    return;
                }
            }
        }
    }
}
#endif
