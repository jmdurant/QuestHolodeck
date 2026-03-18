using UnityEngine;
using UnityEditor;

public static class FixHairTextureEdges
{
    [MenuItem("Tools/Fix Hair Texture Edges")]
    public static void Fix()
    {
        FixTexture("Assets/Models/Joy/Textures/hair_d.png");
    }

    static void FixTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogError("[FixHair] Not found: " + path); return; }

        bool wasReadable = importer.isReadable;
        if (!wasReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) { Debug.LogError("[FixHair] Failed to load: " + path); return; }

        int w = tex.width, h = tex.height;
        var pixels = tex.GetPixels();
        var result = (Color[])pixels.Clone();

        // Step 1: Compute average color of fully opaque hair pixels.
        // This is the "true" hair color we want edges to match.
        float sumR = 0, sumG = 0, sumB = 0;
        int opaqueCount = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a > 0.8f)
            {
                sumR += pixels[i].r;
                sumG += pixels[i].g;
                sumB += pixels[i].b;
                opaqueCount++;
            }
        }

        Color hairColor = opaqueCount > 0
            ? new Color(sumR / opaqueCount, sumG / opaqueCount, sumB / opaqueCount)
            : new Color(0.15f, 0.1f, 0.08f); // fallback dark brown

        Debug.Log($"[FixHair] Average hair color: ({hairColor.r:F3}, {hairColor.g:F3}, {hairColor.b:F3}) from {opaqueCount} opaque pixels");

        // Step 2: For every pixel with alpha > 0 but < 0.8, blend its RGB toward
        // the average hair color. The lower the alpha, the more we pull toward hair color.
        // This ensures wispy edge pixels show dark brown instead of white/grey.
        int corrected = 0;
        for (int i = 0; i < result.Length; i++)
        {
            float a = result[i].a;
            if (a <= 0f || a >= 0.8f) continue;

            // Blend strength: low alpha = more correction toward hair color
            float blend = 1f - (a / 0.8f); // 1.0 at alpha=0, 0.0 at alpha=0.8
            blend = blend * blend; // ease-in for gentler transition on higher alpha pixels

            result[i] = new Color(
                Mathf.Lerp(result[i].r, hairColor.r, blend),
                Mathf.Lerp(result[i].g, hairColor.g, blend),
                Mathf.Lerp(result[i].b, hairColor.b, blend),
                a // preserve original alpha
            );
            corrected++;
        }
        Debug.Log($"[FixHair] Color-corrected {corrected} edge pixels");

        // Step 3: Dilate hair color into fully transparent pixels (16 passes)
        for (int pass = 0; pass < 16; pass++)
        {
            var src = (Color[])result.Clone();
            int filled = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (src[i].a > 0f) continue;

                    float r = 0, g = 0, b = 0;
                    int count = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                            var neighbor = src[ny * w + nx];
                            if (neighbor.a > 0f || (neighbor.r + neighbor.g + neighbor.b) > 0.01f)
                            {
                                r += neighbor.r; g += neighbor.g; b += neighbor.b;
                                count++;
                            }
                        }
                    }
                    if (count > 0)
                    {
                        result[i] = new Color(r / count, g / count, b / count, 0f);
                        filled++;
                    }
                }
            }
            if (filled == 0) break;
        }

        // Write back
        var newTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        newTex.SetPixels(result);
        newTex.Apply();
        System.IO.File.WriteAllBytes(
            System.IO.Path.Combine(Application.dataPath, "../", path),
            newTex.EncodeToPNG());
        Object.DestroyImmediate(newTex);

        if (!wasReadable)
            importer.isReadable = false;
        importer.SaveAndReimport();

        Debug.Log("[FixHair] Done");
    }
}
