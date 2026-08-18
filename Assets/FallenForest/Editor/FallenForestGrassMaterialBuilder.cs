#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Rebuilds the user grass materials from the supplied color/opacity maps and assigns them to
    /// all three measured-area prefabs. This avoids rectangular cards when source color maps have no alpha.
    /// </summary>
    public static class FallenForestGrassMaterialBuilder
    {
        private const string Root = "Assets/FallenForest";
        private const string TextureRoot = Root + "/Art/Vegetation/UserGrass/Textures";
        private const string MaterialRoot = Root + "/Materials/Vegetation/UserGrass";
        private const string PrefabRoot = Root + "/Prefabs/Vegetation";
        private const string RuntimeGrass = Root + "/Generated/SceneBootstrap/RuntimeGrass.prefab";

        [MenuItem("Fallen Forest/Release/Rebuild User Grass Materials")]
        public static void RebuildFromMenu()
        {
            ApplyIfAvailable();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void ApplyIfAvailable()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "/Grass.png") == null) return;
            Directory.CreateDirectory(MaterialRoot);

            Material grass = BuildMaterial("Grass", .39f, .21f);
            Material grass1 = BuildMaterial("Grass1", .38f, .20f);
            Material grass2 = BuildMaterial("Grass2", .40f, .23f);
            Material grass3 = BuildMaterial("Grass3", .39f, .19f);

            ApplyToPrefab(PrefabRoot + "/UserGrass_Large.prefab", grass, grass1, grass2, grass3);
            ApplyToPrefab(PrefabRoot + "/UserGrass_Small.prefab", grass, grass1, grass2, grass3);
            ApplyToPrefab(PrefabRoot + "/UserGrass_Tiny.prefab", grass, grass1, grass2, grass3);
            ApplyToPrefab(RuntimeGrass, grass, grass1, grass2, grass3);
        }

        private static Material BuildMaterial(string prefix, float cutoff, float wind)
        {
            string path = MaterialRoot + "/" + prefix + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("FallenForest/ForestWindURP");
            if (shader == null) throw new InvalidOperationException("FallenForest/ForestWindURP shader is missing.");

            if (material == null)
            {
                material = new Material(shader) { name = prefix };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            Texture2D color = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "/" + prefix + ".png");
            Texture2D opacity = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "/" + prefix + "_Opacity.png");
            if (color == null || opacity == null)
                throw new InvalidOperationException($"User grass material {prefix} is missing color or opacity texture.");

            ConfigureOpacityTexture(TextureRoot + "/" + prefix + "_Opacity.png");
            material.SetTexture("_BaseMap", color);
            material.SetTexture("_OpacityMap", opacity);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Cutoff", cutoff);
            material.SetFloat("_WindAmplitude", wind);
            material.SetFloat("_WindFrequency", .82f);
            material.SetFloat("_BendStrength", .46f);
            material.SetFloat("_HeightMask", 1.9f);
            material.SetFloat("_GrassExclusionEnabled", 1f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureOpacityTexture(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool dirty = false;
            if (importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                dirty = true;
            }
            if (importer.alphaSource != TextureImporterAlphaSource.None)
            {
                importer.alphaSource = TextureImporterAlphaSource.None;
                dirty = true;
            }
            if (dirty) importer.SaveAndReimport();
        }

        private static void ApplyToPrefab(string prefabPath, Material grass, Material grass1, Material grass2, Material grass3)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] source = renderer.sharedMaterials;
                    Material[] replacement = new Material[Mathf.Max(1, source.Length)];
                    for (int i = 0; i < replacement.Length; i++)
                    {
                        string name = source.Length > i && source[i] != null ? source[i].name : renderer.name;
                        replacement[i] = Choose(name, grass, grass1, grass2, grass3);
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

        private static Material Choose(string sourceName, Material grass, Material grass1, Material grass2, Material grass3)
        {
            if (sourceName.IndexOf("Grass3", StringComparison.OrdinalIgnoreCase) >= 0) return grass3;
            if (sourceName.IndexOf("Grass2", StringComparison.OrdinalIgnoreCase) >= 0) return grass2;
            if (sourceName.IndexOf("Grass1", StringComparison.OrdinalIgnoreCase) >= 0) return grass1;
            return grass;
        }
    }
}
#endif
