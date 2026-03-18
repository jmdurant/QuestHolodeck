using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public static class WireJoyMaterials
{
    [MenuItem("Tools/Wire Joy Materials")]
    public static void Wire()
    {
        string texDir = "Assets/Models/Joy/Textures";
        string matDir = "Assets/Models/Joy/Materials";

        // Map: material name -> (diffuse, normal, roughness/metallic)
        var mapping = new Dictionary<string, (string diffuse, string normal, string roughness, string metallic)>
        {
            ["head"]    = ("head_d",    "head_n",    "head_r",    null),
            ["body"]    = ("body_d",    "body_n",    "body_r",    null),
            ["arm"]     = ("arm_d",     "arm_n",     "arm_r",     null),
            ["leg"]     = ("leg_d",     "leg_n",     "leg_r",     null),
            ["lips"]    = ("head_d",    "head_n",    "head_r",    null),
            ["nail"]    = ("body_d",    "body_n",    "body_r",    null),
            ["pupil"]   = ("eye_d",     "eye_n",     "eye_r",     null),
            ["teeth"]   = ("mouth_d",   "mouth_n",   null,        "mouth_m"),
            ["hair_front"] = ("hair_d", "hair_n",    null,        null),
            ["hair_back"]  = ("hair_d", "hair_n",    null,        null),
            ["eyelash"] = ("eyelash",   null,        null,        null),
            ["racerback_crop_sport_diffuse"] = ("racerback_crop_sport_diffuse", "racerback_crop_sport_normal", "racerback_crop_sport_roughness", null),
            ["high_waist_leggings_diffuse"]  = ("high_waist_leggings_diffuse",  "high_waist_leggings_normal",  "high_waist_leggings_roughness",  null),
            ["adidas_pink_shoes_green_diffuse"] = ("adidas_pink_shoes_green_diffuse", "adidas_pink_shoes_normal", "adidas_pink_shoes_roughness", "adidas_pink_shoes_metallic"),
        };

        // Also handle genital material if it exists
        mapping["genital"] = ("gen_d", "gen_n", "gen_r", null);

        // Cornea and lacrimal need to be in the dict to get URP shader assigned
        mapping["cornea"] = (null, null, null, null);
        mapping["lacrimal"] = (null, null, null, null);
        mapping["hairtie"] = (null, null, null, null);
        mapping["gum_tongue"] = ("mouth_d", "mouth_n", null, "mouth_m");

        int configured = 0;
        foreach (var kvp in mapping)
        {
            string matPath = matDir + "/" + kvp.Key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                // Create the missing material so the script works for any outfit
                var urpShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpShader == null)
                {
                    Debug.LogWarning($"[WireJoy] Cannot create material — URP/Lit shader not found: {matPath}");
                    continue;
                }
                mat = new Material(urpShader);
                mat.name = kvp.Key;
                AssetDatabase.CreateAsset(mat, matPath);
                Debug.Log($"[WireJoy] Created missing material: {matPath}");
            }

            // Ensure URP Lit shader
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null && mat.shader != shader)
                mat.shader = shader;

            var (diffuse, normal, roughness, metallic) = kvp.Value;

            if (diffuse != null)
            {
                var tex = FindTexture(texDir, diffuse);
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetColor("_BaseColor", Color.white);
                }
            }

            if (normal != null)
            {
                var tex = FindTexture(texDir, normal);
                if (tex != null)
                {
                    SetNormalMap(tex);
                    mat.SetTexture("_BumpMap", tex);
                    mat.SetFloat("_BumpScale", 1.0f);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }

            if (roughness != null)
            {
                var tex = FindTexture(texDir, roughness);
                if (tex != null)
                {
                    mat.SetTexture("_MetallicGlossMap", tex);
                    mat.SetFloat("_Smoothness", 0.5f);
                    mat.SetFloat("_Metallic", 0.0f);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
            }

            if (metallic != null)
            {
                var tex = FindTexture(texDir, metallic);
                if (tex != null)
                {
                    mat.SetTexture("_MetallicGlossMap", tex);
                    mat.SetFloat("_Metallic", 1.0f);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
            }

            // Hair and eyelash: alpha clip, double-sided, opaque-looking.
            // Blender uses HASHED blending which dithers but looks solid at viewing distance.
            // The hair_d texture has 93% fully transparent pixels and 5% above alpha 0.5.
            // Using a low cutoff (0.05) keeps all visible hair strands while discarding only
            // fully empty areas. ZWrite ON so hair properly occludes — prevents background
            // bleeding through which washes out the dark brown color to grey.
            if (kvp.Key == "hair_front" || kvp.Key == "hair_back" || kvp.Key == "eyelash")
            {
                mat.SetFloat("_Surface", 0); // Opaque
                mat.SetFloat("_AlphaClip", 1);
                mat.SetFloat("_Cutoff", 0.2f);
                mat.SetFloat("_Cull", 0);    // Off — render both sides (hair cards are single-plane)
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = 2450;
            }

            // Handle cornea (transparent glossy overlay)
            if (kvp.Key == "cornea")
            {
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0); // Alpha blend
                mat.SetFloat("_AlphaClip", 0);
                mat.SetFloat("_Cull", 2); // Back
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetColor("_BaseColor", new Color(1, 1, 1, 0.0f));
                mat.SetFloat("_Smoothness", 0.95f);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_SpecularHighlights", 1f);
                mat.SetFloat("_EnvironmentReflections", 1f);
                // Clear any textures — cornea is just a glossy shell
                mat.SetTexture("_BaseMap", null);
                mat.SetTexture("_BumpMap", null);
                mat.SetTexture("_MetallicGlossMap", null);
                mat.SetTexture("_MainTex", null);
            }

            // Handle lacrimal (transparent wet inner eye)
            if (kvp.Key == "lacrimal")
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetFloat("_AlphaClip", 0);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetColor("_BaseColor", new Color(0.9f, 0.85f, 0.85f, 0.15f));
                mat.SetFloat("_Smoothness", 0.9f);
            }

            EditorUtility.SetDirty(mat);
            configured++;
            Debug.Log($"[WireJoy] Configured: {kvp.Key}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[WireJoy] Done! Configured {configured} materials.");
    }

    static Texture2D FindTexture(string dir, string name)
    {
        // Try exact name with .png
        string path = dir + "/" + name + ".png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) return tex;

        // Try without extension (already has .png in name)
        path = dir + "/" + name;
        tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        return tex;
    }

    static void SetNormalMap(Texture2D tex)
    {
        string path = AssetDatabase.GetAssetPath(tex);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }
}
