#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FallenForest.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Converts the exact low-poly Tree_Pack.fbx from the user's Drive archive into individually
    /// scatterable tree/rock prefabs with URP materials. No primitive geometry is used as a visual.
    /// </summary>
    public static class FallenForestLowPolyForestIntegrator
    {
        private const string Root = "Assets/FallenForest";
        private const string SourceRoot = Root + "/Art/Vegetation/UserTrees/LowPolyForest";
        private const string SourceFbx = SourceRoot + "/Source/Extracted/Tree_Pack.fbx";
        private const string PrefabDir = Root + "/Prefabs/Vegetation/LowPolyForest";
        private const string MaterialDir = Root + "/Materials/UserContent/LowPolyForest";
        private const string ForestScene = Root + "/Scenes/Forest.unity";

        [MenuItem("Fallen Forest/Release/Rebuild Low Poly Forest Pack")]
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
            DeleteOldGeneratedPrefabs();

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbx);
            if (source == null)
                throw new InvalidDataException("User low-poly forest FBX was not imported: " + SourceFbx);

            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) instance = UnityEngine.Object.Instantiate(source);
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate user Tree_Pack.fbx.");

            try
            {
                List<Transform> logicalRoots = FindLogicalRoots(instance.transform);
                List<Transform> trees = new();
                List<Transform> rocks = new();
                List<Transform> unknown = new();

                foreach (Transform candidate in logicalRoots)
                {
                    string semantic = BuildSemantic(candidate);
                    bool treeSemantic = IsTreeSemantic(semantic);
                    bool rockSemantic = IsRockSemantic(semantic);

                    if (treeSemantic && !rockSemantic) trees.Add(candidate);
                    else if (rockSemantic && !treeSemantic) rocks.Add(candidate);
                    else unknown.Add(candidate);
                }

                // Generic exports often use names such as Cube/Plane. Classify those by proportions.
                // Mixed tree+rock roots should already have been recursively split by FindLogicalRoots;
                // this geometric fallback prevents one generic wrapper from poisoning the whole pack.
                foreach (Transform candidate in unknown)
                {
                    Bounds b = CalculateBounds(candidate.GetComponentsInChildren<Renderer>(true));
                    float horizontal = Mathf.Max(b.size.x, b.size.z);
                    if (b.size.y > Mathf.Max(2.2f, horizontal * 1.25f)) trees.Add(candidate);
                    else if (b.size.magnitude > .25f) rocks.Add(candidate);
                }

                trees = trees.Where(t => t != null).Distinct().ToList();
                rocks = rocks.Where(t => t != null).Distinct().ToList();

                if (trees.Count == 0)
                    throw new InvalidDataException("Tree_Pack.fbx imported, but no usable tree model roots were discovered.");

                int treeCount = 0;
                foreach (Transform candidate in trees.Take(16))
                    BuildTreePrefab(candidate, ++treeCount);

                int rockCount = 0;
                foreach (Transform candidate in rocks.Take(12))
                    BuildRockPrefab(candidate, ++rockCount);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"Fallen Forest: user low-poly pack built into {treeCount} tree and {rockCount} rock prefabs from {logicalRoots.Count} logical FBX roots.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        public static void PatchForestScene()
        {
            if (!File.Exists(ForestScene)) return;

            List<GameObject> treePrefabs = LoadGeneratedPrefabs("LowPolyTree_");
            List<GameObject> rockPrefabs = LoadGeneratedPrefabs("LowPolyRock_");
            if (treePrefabs.Count == 0)
                throw new InvalidDataException("No LowPolyTree prefabs exist after building the user Tree_Pack.fbx.");

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ForestScene, OpenSceneMode.Single);
            try
            {
                ForestScatterer forest = UnityEngine.Object.FindFirstObjectByType<ForestScatterer>(FindObjectsInactive.Include);
                WorldGenerationCoordinator coordinator = UnityEngine.Object.FindFirstObjectByType<WorldGenerationCoordinator>(FindObjectsInactive.Include);
                Terrain terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>(FindObjectsInactive.Include);
                if (forest == null || coordinator == null)
                    throw new InvalidDataException("Forest scene is missing ForestScatterer/WorldGenerationCoordinator.");

                AppendTrees(forest, treePrefabs);

                ForestPropScatterer props = coordinator.GetComponent<ForestPropScatterer>();
                if (props == null) props = coordinator.gameObject.AddComponent<ForestPropScatterer>();
                SerializedObject propSo = new(props);
                SetObject(propSo, "terrain", terrain);
                SerializedProperty propArray = propSo.FindProperty("propPrefabs");
                propArray.arraySize = rockPrefabs.Count;
                for (int i = 0; i < rockPrefabs.Count; i++)
                    propArray.GetArrayElementAtIndex(i).objectReferenceValue = rockPrefabs[i];
                propSo.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject coordinatorSo = new(coordinator);
                SetObject(coordinatorSo, "propScatterer", props);
                coordinatorSo.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"Fallen Forest: scene patched with {treePrefabs.Count} low-poly tree variants and {rockPrefabs.Count} rock variants.");
            }
            finally
            {
                if (previous.IsValid() && !string.IsNullOrEmpty(previous.path) && previous.path != scene.path)
                    EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
            }
        }

        private static void BuildTreePrefab(Transform candidate, int index)
        {
            GameObject root = new($"LowPolyTree_{index:00}");
            try
            {
                GameObject visual = CloneVisual(candidate, root.transform);
                ApplyMaterials(visual);
                Bounds bounds = CalculateBounds(visual.GetComponentsInChildren<Renderer>(true));
                NormalizeExtremeScale(root, ref bounds, 11.5f, 3.5f, 32f);
                bounds = CalculateBounds(visual.GetComponentsInChildren<Renderer>(true));
                AddTrunkCollider(root, bounds);
                AddVisibilityOccluder(root, bounds);
                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/LowPolyTree_{index:00}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildRockPrefab(Transform candidate, int index)
        {
            GameObject root = new($"LowPolyRock_{index:00}");
            try
            {
                GameObject visual = CloneVisual(candidate, root.transform);
                ApplyMaterials(visual);
                Bounds bounds = CalculateBounds(visual.GetComponentsInChildren<Renderer>(true));
                NormalizeExtremeScale(root, ref bounds, 1.25f, .18f, 7f);
                bounds = CalculateBounds(visual.GetComponentsInChildren<Renderer>(true));

                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = root.transform.InverseTransformPoint(bounds.center);
                collider.size = new Vector3(
                    Mathf.Max(.12f, bounds.size.x * .82f),
                    Mathf.Max(.10f, bounds.size.y * .72f),
                    Mathf.Max(.12f, bounds.size.z * .82f));
                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/LowPolyRock_{index:00}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CloneVisual(Transform source, Transform parent)
        {
            GameObject visual = UnityEngine.Object.Instantiate(source.gameObject);
            visual.name = "Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            RemoveNestedColliders(visual);
            return visual;
        }

        private static void RemoveNestedColliders(GameObject visual)
        {
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
        }

        private static List<Transform> FindLogicalRoots(Transform root)
        {
            var result = new List<Transform>();
            foreach (Transform child in root)
                CollectLogicalRoots(child, result, 0);

            if (result.Count == 0)
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    if (renderer != null)
                        result.Add(renderer.transform);
            }

            return result.Where(t => t != null).Distinct().ToList();
        }

        private static void CollectLogicalRoots(Transform node, List<Transform> result, int depth)
        {
            if (node == null || depth > 12) return;
            Renderer[] renderers = node.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            string semantic = BuildSemantic(node);
            bool treeSemantic = IsTreeSemantic(semantic);
            bool rockSemantic = IsRockSemantic(semantic);

            // A semantically coherent group is one authored object even if it has several child
            // renderers (for example trunk + two branch cards). Never split that object apart.
            if (treeSemantic != rockSemantic)
            {
                result.Add(node);
                return;
            }

            var rendererChildren = new List<Transform>();
            foreach (Transform child in node)
                if (child.GetComponentsInChildren<Renderer>(true).Length > 0)
                    rendererChildren.Add(child);

            // Mixed semantic wrapper (whole forest pack) or generic multi-object wrapper: recurse.
            if (rendererChildren.Count > 1)
            {
                int before = result.Count;
                foreach (Transform child in rendererChildren)
                    CollectLogicalRoots(child, result, depth + 1);
                if (result.Count > before) return;
            }

            // A single renderer or a generic indivisible hierarchy is still a legitimate prop;
            // dimensions will classify it later rather than discarding user geometry.
            result.Add(node);
        }

        private static string BuildSemantic(Transform root)
        {
            var pieces = new List<string> { root.name };
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                pieces.Add(renderer.name);
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null) pieces.Add(material.name);
            }
            return string.Join(" ", pieces).ToLowerInvariant();
        }

        private static bool IsRockSemantic(string s) =>
            s.Contains("rock") || s.Contains("stone") || s.Contains("boulder");

        private static bool IsTreeSemantic(string s) =>
            s.Contains("tree") || s.Contains("trunk") || s.Contains("branch") || s.Contains("pine") ||
            s.Contains("spruce") || s.Contains("fir") || s.Contains("background_tree");

        private static void ApplyMaterials(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string semantic = (renderer.name + " " + (materials[i] != null ? materials[i].name : string.Empty)).ToLowerInvariant();
                    materials[i] = BuildMaterial(semantic, i);
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static Material BuildMaterial(string semantic, int slot)
        {
            string family = ResolveFamily(semantic);
            bool foliage = family.StartsWith("Tree_Branches", StringComparison.OrdinalIgnoreCase) ||
                           family.StartsWith("Background_Tree_Atlas", StringComparison.OrdinalIgnoreCase);
            bool rock = family.StartsWith("ROCKS", StringComparison.OrdinalIgnoreCase);
            Shader shader = foliage ? Shader.Find("FallenForest/TreeFoliageURP") : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            string safeFamily = string.IsNullOrEmpty(family) ? "LowPoly_Generic_" + Mathf.Abs(semantic.GetHashCode()) : family;
            string path = $"{MaterialDir}/{safeFamily}_{slot:00}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            Texture2D diffuse = FindTexture(family, "diffuse", "albedo", "color");
            Texture2D normal = FindTexture(family, "normal");
            Texture2D opacity = FindTexture(family, "opacity", "alpha", "transparency");
            SetTexture(material, "_BaseMap", "_MainTex", diffuse);
            if (normal != null)
            {
                if (material.HasProperty("_NormalMap")) material.SetTexture("_NormalMap", normal);
                if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (foliage && opacity != null && material.HasProperty("_OpacityMap"))
                material.SetTexture("_OpacityMap", opacity);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", foliage ? .34f : .5f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", rock ? .16f : foliage ? .12f : .22f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static string ResolveFamily(string semantic)
        {
            if (semantic.Contains("background") || semantic.Contains("atlas")) return "Background_Tree_Atlas";
            if (semantic.Contains("branches_1") || semantic.Contains("branch_1") || semantic.Contains("branches 1")) return "Tree_Branches_1";
            if (semantic.Contains("branches_2") || semantic.Contains("branch_2") || semantic.Contains("branches 2")) return "Tree_Branches_2";
            if (semantic.Contains("trunk_01") || semantic.Contains("trunk 01") || semantic.Contains("trunk1")) return "Tree_Trunk_01";
            if (semantic.Contains("trunk_02") || semantic.Contains("trunk 02") || semantic.Contains("trunk2")) return "Tree_Trunk_02";
            if (semantic.Contains("rock") || semantic.Contains("stone") || semantic.Contains("boulder")) return "ROCKS";
            if (semantic.Contains("branch")) return "Tree_Branches_1";
            if (semantic.Contains("trunk")) return "Tree_Trunk_01";
            return string.Empty;
        }

        private static Texture2D FindTexture(string family, params string[] kinds)
        {
            int bestScore = int.MinValue;
            Texture2D best = null;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SourceRoot + "/Textures", SourceRoot + "/OuterTextures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                int score = 0;
                if (!string.IsNullOrEmpty(family) && name.Contains(family.ToLowerInvariant())) score += 12;
                foreach (string kind in kinds)
                    if (name.Contains(kind)) score += 5;
                if (score <= bestScore) continue;
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) continue;
                best = texture;
                bestScore = score;
            }
            return bestScore >= 5 ? best : null;
        }

        private static void AppendTrees(ForestScatterer forest, List<GameObject> additions)
        {
            SerializedObject so = new(forest);
            SerializedProperty trees = so.FindProperty("treePrefabs");
            var combined = new List<GameObject>();
            for (int i = 0; i < trees.arraySize; i++)
            {
                GameObject existing = trees.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (existing != null) combined.Add(existing);
            }
            // The HD spruce remains dominant. Each low-poly variant is weighted twice for visible
            // variety without taking over the art direction.
            foreach (GameObject addition in additions)
            {
                combined.Add(addition);
                combined.Add(addition);
            }
            trees.arraySize = combined.Count;
            for (int i = 0; i < combined.Count; i++)
                trees.GetArrayElementAtIndex(i).objectReferenceValue = combined[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<GameObject> LoadGeneratedPrefabs(string prefix)
        {
            var result = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { PrefabDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                if (!Path.GetFileNameWithoutExtension(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) result.Add(prefab);
            }
            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        private static void DeleteOldGeneratedPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { PrefabDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        private static void NormalizeExtremeScale(GameObject root, ref Bounds bounds, float targetHeight, float minimumHeight, float maximumHeight)
        {
            if (bounds.size.y <= .001f) return;
            if (bounds.size.y >= minimumHeight && bounds.size.y <= maximumHeight) return;
            float factor = targetHeight / bounds.size.y;
            root.transform.localScale *= factor;
        }

        private static void AddTrunkCollider(GameObject root, Bounds bounds)
        {
            float treeHeight = Mathf.Max(bounds.size.y, 2f);
            float radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * .16f, .14f, .55f);
            float height = Mathf.Max(radius * 2f, treeHeight * .70f);
            CapsuleCollider trunk = root.AddComponent<CapsuleCollider>();
            trunk.direction = 1;
            trunk.radius = radius;
            trunk.height = height;
            Vector3 centre = new(bounds.center.x, bounds.min.y + height * .5f, bounds.center.z);
            trunk.center = root.transform.InverseTransformPoint(centre);
        }

        private static void AddVisibilityOccluder(GameObject root, Bounds bounds)
        {
            GameObject occluder = new("FoliageVisibilityOccluder");
            occluder.transform.SetParent(root.transform, false);
            BoxCollider box = occluder.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = root.transform.InverseTransformPoint(bounds.center + Vector3.up * bounds.size.y * .08f);
            box.size = new Vector3(
                Mathf.Max(.45f, bounds.size.x * .72f),
                Mathf.Max(.8f, bounds.size.y * .68f),
                Mathf.Max(.45f, bounds.size.z * .72f));
            occluder.AddComponent<VisibilityOccluder>();
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void SetTexture(Material material, string preferred, string fallback, Texture texture)
        {
            if (texture == null || material == null) return;
            if (material.HasProperty(preferred)) material.SetTexture(preferred, texture);
            else if (material.HasProperty(fallback)) material.SetTexture(fallback, texture);
        }

        private static void SetObject(SerializedObject so, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }
    }
}
#endif
