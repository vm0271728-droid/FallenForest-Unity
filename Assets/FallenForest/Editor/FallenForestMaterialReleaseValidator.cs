#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// PBR completeness gate. Critical materials must consume the texture channels that actually
    /// exist in the user's source packages rather than silently falling back to flat constants.
    /// Runs after FallenForestPbrBuildPreprocessor and before Unity serializes build scenes.
    /// </summary>
    public sealed class FallenForestMaterialReleaseValidator : IPreprocessBuildWithReport
    {
        private const string Root = "Assets/FallenForest";
        private const string UserMaterialRoot = Root + "/Materials/UserContent";
        public int callbackOrder => -900;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateOrThrow();
        }

        [MenuItem("Fallen Forest/Release/Validate PBR Material Completeness")]
        public static void ValidateOrThrow()
        {
            var errors = new List<string>();

            Require("FPSArms", UserMaterialRoot + "/FPSArms.mat", errors,
                baseMap: true, normal: true, occlusion: true);
            Require("Flashlight", UserMaterialRoot + "/Flashlight.mat", errors,
                baseMap: true, normal: true, occlusion: true, metallicSmoothness: true, emission: true);
            Require("Document paper", UserMaterialRoot + "/DocumentPaper.mat", errors,
                baseMap: true, normal: true, metallicSmoothness: true);
            Require("Document folder", UserMaterialRoot + "/DocumentFolder.mat", errors,
                baseMap: true, normal: true, metallicSmoothness: true);
            Require("Pickup", UserMaterialRoot + "/Pickup.mat", errors, baseMap: true);
            Require("Locust", UserMaterialRoot + "/Creatures/Locust_UserPBR.mat", errors,
                baseMap: true, normal: true, metallicSmoothness: true);

            ValidateBoiledMaterials(errors);
            ValidateLowPolyMaterials(errors);
            ValidateCanonicalTreeMaterials(errors);

            if (errors.Count == 0)
            {
                Debug.Log("Fallen Forest: PBR material completeness validation PASSED.");
                return;
            }

            string message = "Fallen Forest PBR material validation failed:\n - " + string.Join("\n - ", errors.Distinct());
            Debug.LogError(message);
            throw new BuildFailedException(message);
        }

        private static void ValidateBoiledMaterials(List<string> errors)
        {
            string dir = UserMaterialRoot + "/Creatures";
            foreach (string name in new[] { "Boiled_Detail", "Boiled_Body", "Boiled_Eyes", "Boiled_Teeth", "Boiled_Gums" })
                Require(name, dir + "/" + name + ".mat", errors, baseMap: true);
        }

        private static void ValidateLowPolyMaterials(List<string> errors)
        {
            string dir = UserMaterialRoot + "/LowPolyForest";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                errors.Add("Low-poly user material folder was not generated: " + dir);
                return;
            }

            int checkedCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                if (file.StartsWith("Packed_", StringComparison.OrdinalIgnoreCase)) continue;
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                checkedCount++;

                RequireTexture(file, material, "_BaseMap", "_MainTex", "base color", errors);
                if (file.IndexOf("ROCKS", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    RequireTexture(file, material, "_BumpMap", null, "normal", errors);
                    RequireTexture(file, material, "_OcclusionMap", null, "AO", errors);
                    RequireTexture(file, material, "_MetallicGlossMap", null, "packed roughness/smoothness", errors);
                }
                else if (file.IndexOf("Tree_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         file.IndexOf("Background_", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    RequireTexture(file, material, "_BumpMap", "_NormalMap", "normal", errors);
                    // Custom foliage shader intentionally lights its own alpha-cutout leaves and may
                    // not expose URP/Lit's metallic slot. Lit trunk/background materials must.
                    if (material.HasProperty("_MetallicGlossMap"))
                        RequireTexture(file, material, "_MetallicGlossMap", null, "packed roughness/smoothness", errors);
                }
            }

            if (checkedCount == 0)
                errors.Add("No generated low-poly user materials were found under " + dir);
        }

        private static void ValidateCanonicalTreeMaterials(List<string> errors)
        {
            string dir = UserMaterialRoot + "/Trees";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                errors.Add("Canonical tree material folder was not generated: " + dir);
                return;
            }

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                count++;
                string label = Path.GetFileNameWithoutExtension(path);
                RequireTexture(label, material, "_BaseMap", "_MainTex", "base color", errors);
                if (material.shader != null && material.shader.name == "FallenForest/TreeFoliageURP")
                {
                    RequireTexture(label, material, "_NormalMap", "_BumpMap", "foliage normal", errors);
                    RequireTexture(label, material, "_OpacityMap", null, "foliage opacity", errors);
                }
            }
            if (count == 0)
                errors.Add("No canonical Black Spruce/dead-fir user materials were generated.");
        }

        private static void Require(
            string label,
            string materialPath,
            List<string> errors,
            bool baseMap = false,
            bool normal = false,
            bool occlusion = false,
            bool metallicSmoothness = false,
            bool emission = false)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                errors.Add($"{label}: missing material {materialPath}");
                return;
            }

            if (baseMap) RequireTexture(label, material, "_BaseMap", "_MainTex", "base color", errors);
            if (normal) RequireTexture(label, material, "_BumpMap", "_NormalMap", "normal", errors);
            if (occlusion) RequireTexture(label, material, "_OcclusionMap", null, "AO", errors);
            if (metallicSmoothness) RequireTexture(label, material, "_MetallicGlossMap", null, "metallic/smoothness", errors);
            if (emission) RequireTexture(label, material, "_EmissionMap", null, "emission", errors);
        }

        private static void RequireTexture(
            string label,
            Material material,
            string primary,
            string fallback,
            string channel,
            List<string> errors)
        {
            Texture texture = null;
            if (material.HasProperty(primary)) texture = material.GetTexture(primary);
            if (texture == null && !string.IsNullOrEmpty(fallback) && material.HasProperty(fallback))
                texture = material.GetTexture(fallback);
            if (texture == null)
                errors.Add($"{label}: {channel} texture is not assigned on material {material.name}.");
        }
    }
}
#endif
