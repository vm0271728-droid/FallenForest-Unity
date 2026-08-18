#if UNITY_EDITOR
using System;
using System.IO;
using FallenForest.Monsters;
using UnityEditor;
using UnityEngine;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Builds gameplay prefabs only from the exact user-supplied creature FBX files.
    /// No primitive or placeholder geometry is ever substituted.
    /// </summary>
    public static class FinalCreaturePrefabBuilder
    {
        private const string Root = "Assets/FallenForest";
        private const string LocustFolder = Root + "/Art/Models/DoctorNowhere/Locust";
        private const string BoiledFolder = Root + "/Art/Models/DoctorNowhere/Boiled";
        private const string PrefabDir = Root + "/Prefabs";
        private const string MaterialDir = Root + "/Materials/Creatures";

        [MenuItem("Fallen Forest/Release/Rebuild Exact Creature Prefabs")]
        public static void BuildFromMenu()
        {
            BuildAvailable();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BuildAvailable()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(MaterialDir);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            GameObject locust = FindModelAsset(LocustFolder, "locust");
            if (locust != null) BuildLocust(locust);
            else Debug.LogWarning($"Fallen Forest: exact Locust FBX is not imported under {LocustFolder}.");

            GameObject boiled = FindModelAsset(BoiledFolder, "boiled");
            if (boiled != null) BuildBoiled(boiled);
            else Debug.LogWarning($"Fallen Forest: exact Boiled One FBX is not imported under {BoiledFolder}.");
        }

        private static GameObject FindModelAsset(string folder, string preferredToken)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { folder });
            GameObject fallback = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".fbx" && ext != ".obj") continue;
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null || model.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                if (fallback == null) fallback = model;
                if (Path.GetFileNameWithoutExtension(path).IndexOf(preferredToken, StringComparison.OrdinalIgnoreCase) >= 0)
                    return model;
            }
            return fallback;
        }

        private static void BuildLocust(GameObject source)
        {
            Texture2D baseColor = LoadTexture(LocustFolder + "/Textures/locust_basecolor_tex.png");
            Texture2D normal = LoadTexture(LocustFolder + "/Textures/locust_normal_tex.png", true);
            Texture2D metallic = LoadTexture(LocustFolder + "/Textures/locust_metallic_tex.png");
            Texture2D fibers = LoadTexture(LocustFolder + "/Textures/locust_fibers_tex.png");

            Material main = CreateOrLoadMaterial(MaterialDir + "/Locust_Main.mat");
            SetBaseTexture(main, baseColor);
            SetNormal(main, normal);
            if (metallic != null && main.HasProperty("_MetallicGlossMap"))
            {
                main.SetTexture("_MetallicGlossMap", metallic);
                main.SetFloat("_Metallic", .72f);
            }
            if (main.HasProperty("_Smoothness")) main.SetFloat("_Smoothness", .20f);

            Material black = CreateOrLoadMaterial(MaterialDir + "/Locust_Black.mat");
            SetBaseColor(black, new Color(.012f, .014f, .015f, 1f));
            if (black.HasProperty("_Smoothness")) black.SetFloat("_Smoothness", .08f);

            Material fiber = CreateOrLoadMaterial(MaterialDir + "/Locust_Fibers.mat");
            SetBaseTexture(fiber, fibers != null ? fibers : baseColor);
            EnableAlphaClip(fiber, .28f);
            if (fiber.HasProperty("_Smoothness")) fiber.SetFloat("_Smoothness", .10f);

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (root == null) throw new InvalidOperationException("Could not instantiate exact Locust FBX.");
            root.name = "Locust_Final";
            NormalizeHeight(root, 4.05f);

            ApplyMaterials(root, name =>
            {
                string lower = name.ToLowerInvariant();
                if (lower.Contains("fiber")) return fiber;
                if (lower.Contains("black")) return black;
                return main;
            });

            Animator animator = root.GetComponentInChildren<Animator>(true);
            Transform head = FindTransform(root.transform, "head", "face");
            LocustAI ai = root.GetComponent<LocustAI>() ?? root.AddComponent<LocustAI>();
            SerializedObject so = new(ai);
            SetObject(so, "animator", animator);
            SetObject(so, "headBone", head);
            SetObject(so, "nearSting", AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Monster/locust_near_sting.wav"));
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureCreatureCollider(root, 1.05f, 4.05f, new Vector3(0f, 2.02f, 0f));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Locust_Final.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("Fallen Forest: exact Locust gameplay prefab rebuilt.");
        }

        private static void BuildBoiled(GameObject source)
        {
            Material body = MaterialForTexture("Boiled_Body", "BoiledOne_Body_AlbedoTransparency.png");
            Material headDetail = MaterialForTexture("Boiled_HeadDetail", "PNG.png");
            Material details = MaterialForTexture("Boiled_Details", "BoiledOne_Details_AlbedoTransparency.png");
            Material teeth = MaterialForTexture("Boiled_Teeth", "BoiledOne_TeethMaterial_AlbedoTransparency.png");
            Material eyes = MaterialForTexture("Boiled_Eyes", "BoiledOne_Eyes_AlbedoTransparency.png");
            Material gums = MaterialForTexture("Boiled_Gums", "BoiledOne_GumsMaterial_AlbedoTransparency.png");

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (root == null) throw new InvalidOperationException("Could not instantiate exact Boiled One FBX.");
            root.name = "BoiledOne_Final";
            NormalizeHeight(root, 2.65f);

            ApplyMaterials(root, name =>
            {
                string lower = name.ToLowerInvariant();
                if (lower.Contains("headdetail")) return headDetail;
                if (lower.Contains("detail")) return details;
                if (lower.Contains("teeth")) return teeth;
                if (lower.Contains("eye")) return eyes;
                if (lower.Contains("gum")) return gums;
                return body;
            });

            Animator animator = root.GetComponentInChildren<Animator>(true);
            Transform head = FindTransform(root.transform, "head", "body5", "body4");
            BoiledOneEncounter encounter = root.GetComponent<BoiledOneEncounter>() ?? root.AddComponent<BoiledOneEncounter>();
            SerializedObject so = new(encounter);
            SetObject(so, "animator", animator);
            SetObject(so, "visualRoot", root.transform);
            SetObject(so, "headBone", head);
            SetObject(so, "preVideoSting", AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Monster/boiled_trigger_sting.wav"));
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureCreatureCollider(root, .72f, 2.65f, new Vector3(0f, 1.325f, 0f));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/BoiledOne_Final.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("Fallen Forest: exact Boiled One gameplay prefab rebuilt.");
        }

        private static Material MaterialForTexture(string materialName, string textureName)
        {
            Material material = CreateOrLoadMaterial(MaterialDir + "/" + materialName + ".mat");
            Texture2D texture = LoadTexture(BoiledFolder + "/Textures/" + textureName);
            SetBaseTexture(material, texture);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
            return material;
        }

        private static Texture2D LoadTexture(string path, bool normalMap = false)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (normalMap && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    dirty = true;
                }
                importer.maxTextureSize = Mathf.Min(importer.maxTextureSize <= 0 ? 2048 : importer.maxTextureSize, 2048);
                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                android.name = "Android";
                android.overridden = true;
                android.format = TextureImporterFormat.ASTC_6x6;
                android.maxTextureSize = 2048;
                importer.SetPlatformTextureSettings(android);
                if (dirty) importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material CreateOrLoadMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void NormalizeHeight(GameObject root, float targetHeight)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y <= .001f) return;
            float factor = targetHeight / bounds.size.y;
            root.transform.localScale *= factor;
        }

        private static void ApplyMaterials(GameObject root, Func<string, Material> choose)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] source = renderer.sharedMaterials;
                Material[] replacement = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    string originalName = source[i] != null ? source[i].name : string.Empty;
                    replacement[i] = choose(originalName);
                }
                renderer.sharedMaterials = replacement;
            }
        }

        private static void SetBaseTexture(Material material, Texture2D texture)
        {
            if (material == null || texture == null) return;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            else if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        }

        private static void SetBaseColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static void SetNormal(Material material, Texture2D normal)
        {
            if (material == null || normal == null || !material.HasProperty("_BumpMap")) return;
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }

        private static void EnableAlphaClip(Material material, float cutoff)
        {
            if (material == null) return;
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", cutoff);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }

        private static Transform FindTransform(Transform root, params string[] tokens)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            foreach (string token in tokens)
                foreach (Transform candidate in all)
                    if (candidate.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                        return candidate;
            return root;
        }

        private static void EnsureCreatureCollider(GameObject root, float radius, float height, Vector3 center)
        {
            if (root.GetComponentInChildren<Collider>(true) != null) return;
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = height;
            capsule.center = center;
        }

        private static void SetObject(SerializedObject so, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }
    }
}
#endif
