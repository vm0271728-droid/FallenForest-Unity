#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Connects the exact texture packages supplied with the user's models to URP materials.
    /// Standalone metallic/smoothness/roughness masks are packed into URP's metallic-alpha layout.
    /// </summary>
    public static class FallenForestUserMaterialBuilder
    {
        private const string Root = "Assets/FallenForest";
        private const string MaterialRoot = Root + "/Materials/UserContent";

        [MenuItem("Fallen Forest/Release/Rebuild User PBR Materials")]
        public static void RebuildFromMenu()
        {
            ApplyIfAvailable();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void ApplyIfAvailable()
        {
            Directory.CreateDirectory(MaterialRoot);

            Material arms = BuildLit(
                "FPSArms",
                Root + "/Art/Viewmodel/Arms/Textures/armColor.png",
                Root + "/Art/Viewmodel/Arms/Textures/armnormal.png",
                Root + "/Art/Viewmodel/Arms/Textures/armAO.png",
                null,
                null,
                false,
                null,
                .30f,
                0f);
            ApplySingle(FinalUserAssetPrefabBuilder.ArmsPrefab, arms);

            Material flashlight = BuildLit(
                "Flashlight",
                Root + "/Art/Viewmodel/Flashlight/Textures/flashlightColor.png",
                Root + "/Art/Viewmodel/Flashlight/Textures/flashlightNormal.png",
                Root + "/Art/Viewmodel/Flashlight/Textures/flashlightAO.png",
                Root + "/Art/Viewmodel/Flashlight/Textures/flashlightMetallic.png",
                Root + "/Art/Viewmodel/Flashlight/Textures/flashlightSmoothness.png",
                false,
                Root + "/Art/Viewmodel/Flashlight/Textures/flashlighemissive.png",
                .58f,
                .92f);
            ApplySingle(FinalUserAssetPrefabBuilder.FlashlightPrefab, flashlight);

            Material pickup = BuildLit(
                "Pickup",
                Root + "/Art/Vehicles/Pickup/Textures/Pickup_Afghanistan.png",
                null,
                null,
                null,
                null,
                false,
                null,
                .24f,
                0f);
            ApplySingle(FinalUserAssetPrefabBuilder.PickupPrefab, pickup);

            Material paper = BuildLit(
                "DocumentPaper",
                Root + "/Art/Documents/UserDocument/Textures/gltf_embedded_0.jpeg",
                Root + "/Art/Documents/UserDocument/Textures/gltf_embedded_2.png",
                null,
                null,
                Root + "/Art/Documents/UserDocument/Textures/gltf_embedded_1@channels=G.png",
                true,
                null,
                .18f,
                0f);
            Material folder = BuildLit(
                "DocumentFolder",
                Root + "/Art/Documents/UserDocument/Textures/gltf_embedded_3.jpeg",
                Root + "/Art/Documents/UserDocument/Textures/gltf_embedded_5.png",
                null,
                Root + "/Art/Documents/UserDocument/Textures/gltf_embedded_4@channels=B.png",
                Root + "/Art/Documents/UserDocument/Textures/gltf_embedded_4@channels=G.png",
                true,
                null,
                .22f,
                0f);
            ApplyDocument(FinalUserAssetPrefabBuilder.DocumentPrefab, paper, folder);
        }

        private static Material BuildLit(
            string name,
            string basePath,
            string normalPath,
            string occlusionPath,
            string metallicPath,
            string smoothnessOrRoughnessPath,
            bool invertRoughness,
            string emissionPath,
            float smoothness,
            float emissionStrength)
        {
            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            if (baseMap == null) return null;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            string path = MaterialRoot + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            SetTexture(material, "_BaseMap", "_MainTex", baseMap);
            if (normalPath != null)
            {
                ConfigureNormal(normalPath);
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (normal != null && material.HasProperty("_BumpMap"))
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }
            }
            if (occlusionPath != null)
            {
                ConfigureLinear(occlusionPath);
                Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>(occlusionPath);
                if (ao != null && material.HasProperty("_OcclusionMap"))
                {
                    material.SetTexture("_OcclusionMap", ao);
                    if (material.HasProperty("_OcclusionStrength")) material.SetFloat("_OcclusionStrength", 1f);
                }
            }

            Texture2D packedMetallicSmoothness = FallenForestPbrMaskPacker.BuildMetallicSmoothness(
                MaterialRoot + "/" + name + "_MetallicSmoothness.png",
                metallicPath,
                smoothnessOrRoughnessPath,
                invertRoughness);
            if (packedMetallicSmoothness != null && material.HasProperty("_MetallicGlossMap"))
            {
                material.SetTexture("_MetallicGlossMap", packedMetallicSmoothness);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 1f);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1f);
                if (material.HasProperty("_SmoothnessTextureChannel")) material.SetFloat("_SmoothnessTextureChannel", 0f);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (emissionPath != null)
            {
                Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(emissionPath);
                if (emission != null && material.HasProperty("_EmissionMap"))
                {
                    material.SetTexture("_EmissionMap", emission);
                    if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.white * emissionStrength);
                    material.EnableKeyword("_EMISSION");
                }
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplySingle(string prefabPath, Material material)
        {
            if (material == null || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    int count = Mathf.Max(1, renderer.sharedMaterials.Length);
                    Material[] materials = new Material[count];
                    for (int i = 0; i < count; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyDocument(string prefabPath, Material paper, Material folder)
        {
            if (paper == null || folder == null || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] current = renderer.sharedMaterials;
                    Material[] replacement = new Material[Mathf.Max(1, current.Length)];
                    for (int i = 0; i < replacement.Length; i++)
                    {
                        string materialName = current.Length > i && current[i] != null ? current[i].name : renderer.name;
                        replacement[i] = materialName.IndexOf("material_4", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         materialName.IndexOf("folder", StringComparison.OrdinalIgnoreCase) >= 0
                            ? folder
                            : paper;
                    }
                    renderer.sharedMaterials = replacement;
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetTexture(Material material, string preferred, string fallback, Texture2D texture)
        {
            if (material.HasProperty(preferred)) material.SetTexture(preferred, texture);
            else if (material.HasProperty(fallback)) material.SetTexture(fallback, texture);
        }

        private static void ConfigureNormal(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.NormalMap) return;
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        private static void ConfigureLinear(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || !importer.sRGBTexture) return;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }
    }
}
#endif
