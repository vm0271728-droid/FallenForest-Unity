#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Final release material pass for exact user-supplied creature and low-poly rock textures.
    /// It aligns generated assets with the release validator and ensures the built prefabs consume
    /// the actual PBR channels shipped in the canonical archives.
    /// </summary>
    public static class FallenForestReleaseMaterialIntegrator
    {
        private const string Root = "Assets/FallenForest";
        private const string CreatureMaterialDir = Root + "/Materials/UserContent/Creatures";
        private const string LocustTextureDir = Root + "/Art/Models/DoctorNowhere/Locust/Textures";
        private const string BoiledTextureDir = Root + "/Art/Models/DoctorNowhere/Boiled/Textures";
        private const string LowPolyRoot = Root + "/Art/Vegetation/UserTrees/LowPolyForest";
        private const string LowPolyMaterialDir = Root + "/Materials/UserContent/LowPolyForest";
        private const string LocustPrefab = Root + "/Prefabs/Locust_Final.prefab";
        private const string BoiledPrefab = Root + "/Prefabs/BoiledOne_Final.prefab";

        [MenuItem("Fallen Forest/Release/Finalize Creature And Rock PBR Materials")]
        public static void Apply()
        {
            Directory.CreateDirectory(CreatureMaterialDir);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            FinalizeLocust();
            FinalizeBoiled();
            FinalizeLowPolyRocks();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Fallen Forest: release creature/rock PBR material integration completed.");
        }

        private static void FinalizeLocust()
        {
            string basePath = LocustTextureDir + "/locust_basecolor_tex.png";
            string normalPath = LocustTextureDir + "/locust_normal_tex.png";
            string metallicPath = LocustTextureDir + "/locust_metallic_tex.png";
            string roughnessPath = LocustTextureDir + "/locust_roughness_tex.png";

            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            Texture2D normal = LoadNormal(normalPath);
            if (baseMap == null) return;

            Material pbr = CreateOrLoadLit(CreatureMaterialDir + "/Locust_UserPBR.mat");
            SetBaseMap(pbr, baseMap);
            SetNormal(pbr, normal);

            Texture2D packed = FallenForestPbrMaskPacker.BuildMetallicSmoothness(
                CreatureMaterialDir + "/Locust_UserPBR_MetallicSmoothness.png",
                metallicPath,
                roughnessPath,
                true);
            SetMetallicSmoothness(pbr, packed);
            EditorUtility.SetDirty(pbr);

            PatchPrefab(LocustPrefab, delegate(Material current)
            {
                if (current == null) return null;
                string n = current.name;
                return n.IndexOf("Locust_Main", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       n.IndexOf("Locust_UserPBR", StringComparison.OrdinalIgnoreCase) >= 0
                    ? pbr
                    : null;
            });
        }

        private static void FinalizeBoiled()
        {
            Material body = BuildBaseOnly("Boiled_Body", "BoiledOne_Body_AlbedoTransparency.png");
            Material detail = BuildBaseOnly("Boiled_Detail", "BoiledOne_Details_AlbedoTransparency.png");
            Material eyes = BuildBaseOnly("Boiled_Eyes", "BoiledOne_Eyes_AlbedoTransparency.png");
            Material teeth = BuildBaseOnly("Boiled_Teeth", "BoiledOne_TeethMaterial_AlbedoTransparency.png");
            Material gums = BuildBaseOnly("Boiled_Gums", "BoiledOne_GumsMaterial_AlbedoTransparency.png");
            Material headDetail = BuildBaseOnly("Boiled_HeadDetail", "PNG.png");

            PatchPrefab(BoiledPrefab, delegate(Material current)
            {
                if (current == null) return null;
                string n = current.name;
                if (n.IndexOf("HeadDetail", StringComparison.OrdinalIgnoreCase) >= 0) return headDetail;
                if (n.IndexOf("Detail", StringComparison.OrdinalIgnoreCase) >= 0) return detail;
                if (n.IndexOf("Teeth", StringComparison.OrdinalIgnoreCase) >= 0) return teeth;
                if (n.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0) return eyes;
                if (n.IndexOf("Gum", StringComparison.OrdinalIgnoreCase) >= 0) return gums;
                if (n.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0) return body;
                return null;
            });
        }

        private static Material BuildBaseOnly(string name, string textureName)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BoiledTextureDir + "/" + textureName);
            if (texture == null) return null;
            Material material = CreateOrLoadLit(CreatureMaterialDir + "/" + name + ".mat");
            SetBaseMap(material, texture);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void FinalizeLowPolyRocks()
        {
            if (!AssetDatabase.IsValidFolder(LowPolyMaterialDir)) return;

            string aoPath = FirstExisting(
                LowPolyRoot + "/Textures/ROCKS/ROCKS_AO.png",
                LowPolyRoot + "/OuterTextures/ROCKS_AO.png");
            string normalPath = FirstExisting(
                LowPolyRoot + "/Textures/ROCKS/ROCKS_NORMALtest.png",
                LowPolyRoot + "/OuterTextures/ROCKS_NORMALtest.png");
            string roughnessPath = FirstExisting(
                LowPolyRoot + "/Textures/ROCKS/ROCKS_ROUGHNESS.png",
                LowPolyRoot + "/OuterTextures/ROCKS_ROUGHNESS.png");

            Texture2D ao = LoadLinear(aoPath);
            Texture2D normal = LoadNormal(normalPath);
            Texture2D packed = FallenForestPbrMaskPacker.BuildMetallicSmoothness(
                LowPolyMaterialDir + "/Packed_ROCKS_MetallicSmoothness.png",
                null,
                roughnessPath,
                true);

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { LowPolyMaterialDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.IndexOf("ROCKS", StringComparison.OrdinalIgnoreCase) < 0) continue;

                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                SetNormal(material, normal);
                if (ao != null && material.HasProperty("_OcclusionMap"))
                {
                    material.SetTexture("_OcclusionMap", ao);
                    if (material.HasProperty("_OcclusionStrength")) material.SetFloat("_OcclusionStrength", 1f);
                }
                SetMetallicSmoothness(material, packed);
                EditorUtility.SetDirty(material);
            }
        }

        private static Material CreateOrLoadLit(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader);
                material.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            return material;
        }

        private static void PatchPrefab(string prefabPath, Func<Material, Material> replacement)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                bool changed = false;
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material next = replacement(materials[i]);
                        if (next == null || next == materials[i]) continue;
                        materials[i] = next;
                        changed = true;
                    }
                    if (changed) renderer.sharedMaterials = materials;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetBaseMap(Material material, Texture2D texture)
        {
            if (material == null || texture == null) return;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            else if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        }

        private static void SetNormal(Material material, Texture2D normal)
        {
            if (material == null || normal == null) return;
            if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal);
            if (material.HasProperty("_NormalMap")) material.SetTexture("_NormalMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }

        private static void SetMetallicSmoothness(Material material, Texture2D packed)
        {
            if (material == null || packed == null || !material.HasProperty("_MetallicGlossMap")) return;
            material.SetTexture("_MetallicGlossMap", packed);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 1f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1f);
            if (material.HasProperty("_SmoothnessTextureChannel")) material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        private static Texture2D LoadNormal(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D LoadLinear(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static string FirstExisting(params string[] paths)
        {
            foreach (string path in paths)
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return path;
            return null;
        }
    }
}
#endif
