#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using FallenForest.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Builds release tree prefabs exclusively from the user's Google Drive tree archive.
    /// Black Spruce receives a real LOD0-LOD4 LODGroup, physical trunk collision and lightweight
    /// foliage visibility occlusion. Dead fir OBJ objects are preserved as rarer forest variants.
    /// </summary>
    public static class FallenForestTreePackIntegrator
    {
        private const string Root = "Assets/FallenForest";
        private const string TreeRoot = Root + "/Art/Vegetation/UserTrees";
        private const string BlackSource = TreeRoot + "/BlackSpruce/Source";
        private const string DeadSource = TreeRoot + "/DeadFirs/Source";
        private const string PrefabDir = Root + "/Prefabs/Vegetation/Trees";
        private const string MaterialDir = Root + "/Materials/UserContent/Trees";
        private const string ForestScene = Root + "/Scenes/Forest.unity";
        public const string BlackSprucePrefab = PrefabDir + "/BlackSpruce_LOD.prefab";

        [MenuItem("Fallen Forest/Release/Rebuild Canonical Trees")]
        public static void BuildFromMenu()
        {
            BuildAvailable();
            PatchForestScene();
        }

        public static void BuildAvailable()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(MaterialDir);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            BuildBlackSpruce();
            BuildDeadFirVariants();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void PatchForestScene()
        {
            if (!File.Exists(ForestScene)) return;

            GameObject black = AssetDatabase.LoadAssetAtPath<GameObject>(BlackSprucePrefab);
            if (black == null)
                throw new InvalidDataException("Canonical Black Spruce LOD prefab was not built from the user tree archive.");

            List<GameObject> weighted = new();
            // Black Spruce remains the dominant living forest species.
            for (int i = 0; i < 28; i++) weighted.Add(black);
            for (int i = 0; i < 4; i++)
            {
                GameObject dead = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/DeadFir_{i + 1}.prefab");
                if (dead != null) weighted.Add(dead);
            }

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ForestScene, OpenSceneMode.Single);
            try
            {
                ForestScatterer scatterer = UnityEngine.Object.FindFirstObjectByType<ForestScatterer>(FindObjectsInactive.Include);
                if (scatterer == null)
                    throw new InvalidDataException("Forest scene has no ForestScatterer to receive canonical tree prefabs.");

                SerializedObject so = new(scatterer);
                SerializedProperty trees = so.FindProperty("treePrefabs");
                trees.arraySize = weighted.Count;
                for (int i = 0; i < weighted.Count; i++)
                    trees.GetArrayElementAtIndex(i).objectReferenceValue = weighted[i];
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"Fallen Forest: forest patched with {weighted.Count} weighted canonical user tree entries.");
            }
            finally
            {
                if (previous.IsValid() && !string.IsNullOrEmpty(previous.path) && previous.path != scene.path)
                    EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
            }
        }

        private static void BuildBlackSpruce()
        {
            GameObject[] models = new GameObject[5];
            string[] paths = new string[5];
            for (int lod = 0; lod < 5; lod++)
            {
                paths[lod] = FindModelPath(BlackSource, $"LOD{lod}", ".fbx");
                if (string.IsNullOrEmpty(paths[lod]))
                    throw new InvalidDataException($"Canonical Black Spruce is missing LOD{lod} FBX under {BlackSource}.");
                models[lod] = AssetDatabase.LoadAssetAtPath<GameObject>(paths[lod]);
                if (models[lod] == null)
                    throw new InvalidDataException($"Unity could not import Black Spruce LOD{lod}: {paths[lod]}");
            }

            GameObject root = new("BlackSpruce_LOD");
            try
            {
                var lods = new LOD[5];
                Bounds lod0Bounds = default;
                bool haveBounds = false;
                float[] thresholds = { .58f, .34f, .19f, .095f, .035f };

                for (int lod = 0; lod < 5; lod++)
                {
                    GameObject visual = InstantiateModel(models[lod], root.transform, $"LOD{lod}");
                    ApplyTreeMaterials(visual, Path.GetDirectoryName(paths[lod])?.Replace('\\', '/') ?? BlackSource);
                    Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
                    lods[lod] = new LOD(thresholds[lod], renderers);
                    if (lod == 0)
                    {
                        lod0Bounds = CalculateBounds(renderers);
                        haveBounds = renderers.Length > 0;
                    }
                }

                LODGroup group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = true;
                group.SetLODs(lods);
                group.RecalculateBounds();

                if (haveBounds)
                {
                    AddTrunkCollider(root, lod0Bounds);
                    AddVisibilityOccluder(root, lod0Bounds);
                }

                PrefabUtility.SaveAsPrefabAsset(root, BlackSprucePrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildDeadFirVariants()
        {
            string path = FindModelPath(DeadSource, "firs", ".obj");
            if (string.IsNullOrEmpty(path)) return;
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) return;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (instance == null) instance = UnityEngine.Object.Instantiate(source);
            try
            {
                ApplyTreeMaterials(instance, Path.GetDirectoryName(path)?.Replace('\\', '/') ?? DeadSource);
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                int created = 0;
                for (int r = 0; r < renderers.Length && created < 4; r++)
                {
                    MeshFilter filter = renderers[r].GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;

                    created++;
                    GameObject root = new($"DeadFir_{created}");
                    try
                    {
                        GameObject visual = UnityEngine.Object.Instantiate(renderers[r].gameObject);
                        visual.name = "Visual";
                        visual.transform.SetParent(root.transform, false);
                        visual.transform.localPosition = Vector3.zero;
                        visual.transform.localRotation = Quaternion.identity;
                        visual.transform.localScale = Vector3.one;
                        ApplyTreeMaterials(visual, Path.GetDirectoryName(path)?.Replace('\\', '/') ?? DeadSource);

                        Bounds bounds = CalculateBounds(visual.GetComponentsInChildren<Renderer>(true));
                        AddTrunkCollider(root, bounds);
                        AddVisibilityOccluder(root, bounds);
                        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/DeadFir_{created}.prefab");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void AddTrunkCollider(GameObject root, Bounds bounds)
        {
            float treeHeight = Mathf.Max(bounds.size.y, 2f);
            float radius = Mathf.Clamp(treeHeight * .021f, .17f, .58f);
            float height = Mathf.Max(radius * 2f, treeHeight * .78f);
            CapsuleCollider trunk = root.AddComponent<CapsuleCollider>();
            trunk.direction = 1;
            trunk.radius = radius;
            trunk.height = height;
            Vector3 worldCentre = new(bounds.center.x, bounds.min.y + height * .5f, bounds.center.z);
            trunk.center = root.transform.InverseTransformPoint(worldCentre);
        }

        private static void AddVisibilityOccluder(GameObject root, Bounds bounds)
        {
            GameObject occluder = new("FoliageVisibilityOccluder");
            occluder.transform.SetParent(root.transform, false);
            BoxCollider box = occluder.AddComponent<BoxCollider>();
            box.isTrigger = true;
            Vector3 centre = bounds.center + Vector3.up * bounds.size.y * .06f;
            box.center = root.transform.InverseTransformPoint(centre);
            box.size = new Vector3(
                Mathf.Max(.5f, bounds.size.x * .68f),
                Mathf.Max(1f, bounds.size.y * .72f),
                Mathf.Max(.5f, bounds.size.z * .68f));
            occluder.AddComponent<VisibilityOccluder>();
        }

        private static GameObject InstantiateModel(GameObject source, Transform parent, string name)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) instance = UnityEngine.Object.Instantiate(source);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static void ApplyTreeMaterials(GameObject root, string searchFolder)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = BuildUrpMaterial(materials[i], searchFolder, renderer.name, i);
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static Material BuildUrpMaterial(Material source, string searchFolder, string rendererName, int slot)
        {
            string sourceName = source != null ? source.name : rendererName + "_" + slot;
            string lower = sourceName.ToLowerInvariant();
            bool foliage = lower.Contains("fol") || lower.Contains("branch") || lower.Contains("brch") || lower.Contains("leaf");
            Shader shader = foliage
                ? Shader.Find("FallenForest/TreeFoliageURP")
                : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            string safe = Sanitize(sourceName + (foliage ? "_Foliage" : "_Bark"));
            string path = $"{MaterialDir}/{safe}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = safe };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            Texture2D baseMap = source != null ? source.mainTexture as Texture2D : null;
            if (baseMap == null) baseMap = FindBestTexture(searchFolder, sourceName, "color", "diffuse", "gray");
            Texture2D normal = FindBestTexture(searchFolder, sourceName, "normal");
            Texture2D opacity = FindBestTexture(searchFolder, sourceName, "transparency", "opacity", "alpha");

            SetTexture(material, "_BaseMap", "_MainTex", baseMap);
            if (normal != null)
            {
                if (material.HasProperty("_NormalMap")) material.SetTexture("_NormalMap", normal);
                if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (foliage && opacity != null && material.HasProperty("_OpacityMap"))
                material.SetTexture("_OpacityMap", opacity);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", foliage ? .36f : .5f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", foliage ? .18f : .24f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D FindBestTexture(string folder, string materialName, params string[] requiredKinds)
        {
            string family = FamilyToken(materialName);
            int bestScore = int.MinValue;
            Texture2D best = null;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder, TreeRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string lower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                int score = 0;
                if (!string.IsNullOrEmpty(family) && lower.Contains(family)) score += 8;
                for (int i = 0; i < requiredKinds.Length; i++)
                    if (lower.Contains(requiredKinds[i])) score += 4;
                if (score <= bestScore) continue;
                Texture2D candidate = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (candidate == null) continue;
                bestScore = score;
                best = candidate;
            }
            return bestScore >= 4 ? best : null;
        }

        private static string FamilyToken(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("fol_2_long")) return "fol_2_long";
            if (lower.Contains("fol_2_short")) return "fol_2_short";
            if (lower.Contains("fol_1_dead")) return "fol_1_dead";
            if (lower.Contains("fol_1")) return "fol_1";
            if (lower.Contains("brch_dead")) return "brch_dead";
            if (lower.Contains("brch_cut")) return "brch_cut";
            if (lower.Contains("branch")) return "branch_01";
            if (lower.Contains("bark")) return "bark_1";
            return string.Empty;
        }

        private static string FindModelPath(string folder, string token, string extension)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) continue;
                if (Path.GetFileNameWithoutExtension(path).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return path;
            }
            return null;
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static void SetTexture(Material material, string preferred, string fallback, Texture texture)
        {
            if (texture == null || material == null) return;
            if (material.HasProperty(preferred)) material.SetTexture(preferred, texture);
            else if (material.HasProperty(fallback)) material.SetTexture(fallback, texture);
        }

        private static string Sanitize(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        }
    }
}
#endif
