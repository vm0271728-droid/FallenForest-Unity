#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Packs user-supplied standalone PBR masks into the channel layout URP/Lit expects:
    /// metallic in R and smoothness in A. Source images are read directly from disk so their Unity
    /// Read/Write setting does not need to be enabled and mobile runtime memory remains unaffected.
    /// </summary>
    public static class FallenForestPbrMaskPacker
    {
        public static Texture2D BuildMetallicSmoothness(
            string outputAssetPath,
            string metallicAssetPath,
            string smoothnessOrRoughnessAssetPath,
            bool invertRoughness)
        {
            bool hasMetallic = !string.IsNullOrEmpty(metallicAssetPath) && File.Exists(metallicAssetPath);
            bool hasGloss = !string.IsNullOrEmpty(smoothnessOrRoughnessAssetPath) && File.Exists(smoothnessOrRoughnessAssetPath);
            if (!hasMetallic && !hasGloss) return null;

            using Image metallic = hasMetallic ? Image.Load(metallicAssetPath) : null;
            using Image gloss = hasGloss ? Image.Load(smoothnessOrRoughnessAssetPath) : null;

            int width = metallic != null ? metallic.Width : gloss.Width;
            int height = metallic != null ? metallic.Height : gloss.Height;
            Color32[] metalPixels = metallic != null
                ? ResampleNearest(metallic.Pixels, metallic.Width, metallic.Height, width, height)
                : null;
            Color32[] glossPixels = gloss != null
                ? ResampleNearest(gloss.Pixels, gloss.Width, gloss.Height, width, height)
                : null;
            var packed = new Color32[width * height];

            for (int i = 0; i < packed.Length; i++)
            {
                byte metal = metalPixels != null ? Luminance(metalPixels[i]) : (byte)0;
                byte smooth = glossPixels != null ? Luminance(glossPixels[i]) : (byte)128;
                if (invertRoughness) smooth = (byte)(255 - smooth);
                packed[i] = new Color32(metal, 0, 0, smooth);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputAssetPath) ?? string.Empty);
            var output = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            output.SetPixels32(packed);
            output.Apply(false, false);
            File.WriteAllBytes(outputAssetPath, output.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(output);

            AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ConfigurePackedMask(outputAssetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outputAssetPath);
        }

        private static void ConfigurePackedMask(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.sRGBTexture = false;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.maxTextureSize = 2048;

            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = 2048;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 100;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        private static byte Luminance(Color32 c)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * .2126f + c.g * .7152f + c.b * .0722f), 0, 255);
        }

        private static Color32[] ResampleNearest(Color32[] source, int sourceWidth, int sourceHeight, int width, int height)
        {
            if (sourceWidth == width && sourceHeight == height) return source;
            var result = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int sy = Mathf.Clamp(Mathf.FloorToInt((y + .5f) * sourceHeight / height), 0, sourceHeight - 1);
                for (int x = 0; x < width; x++)
                {
                    int sx = Mathf.Clamp(Mathf.FloorToInt((x + .5f) * sourceWidth / width), 0, sourceWidth - 1);
                    result[y * width + x] = source[sy * sourceWidth + sx];
                }
            }
            return result;
        }

        private sealed class Image : IDisposable
        {
            private readonly Texture2D texture;
            public int Width => texture.width;
            public int Height => texture.height;
            public Color32[] Pixels => texture.GetPixels32();

            private Image(Texture2D texture) => this.texture = texture;

            public static Image Load(string assetPath)
            {
                byte[] bytes = File.ReadAllBytes(assetPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                if (!texture.LoadImage(bytes, false))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    throw new InvalidDataException("Could not decode PBR mask image: " + assetPath);
                }
                return new Image(texture);
            }

            public void Dispose()
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
#endif
