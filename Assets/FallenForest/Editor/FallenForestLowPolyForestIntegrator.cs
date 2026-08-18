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
    /// Converts the exact low-poly Tree_Pack sources from the user's Drive archive into individually
    /// scatterable tree/rock prefabs with URP materials. FBX and OBJ are both required primary model
    /// sources; neither is treated as an optional fallback. Runtime prefab generation uses the FBX
    /// representation to avoid duplicating the same authored geometry while OBJ import is validated
    /// as an equally required canonical source. No primitive geometry is used as a visual.
    /// </summary>
    public static class FallenForestLowPolyForestIntegrator
    {
        private const string Root = "Assets/FallenForest";
        private const string SourceRoot = Root + "/Art/Vegetation/UserTrees/LowPolyForest";
        private const string SourceFbx = SourceRoot + "/Source/Extracted/Tree_Pack.fbx";
        private const string SourceObj = SourceRoot + "/Source/Extracted/Tree_Pack.obj";
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

            GameObject sourceFbx = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbx);
            if (sourceFbx == null)
                throw new InvalidDataException("Primary user low-poly forest FBX was not imported: " + SourceFbx);

            GameObject sourceObj = AssetDatabase.LoadAssetAtPath<GameObject>(SourceObj);
            if (sourceObj == null)
                throw new InvalidDataException("Primary user low-poly forest OBJ was not imported: " + SourceObj);

            Renderer[] fbxRenderers = sourceFbx.GetComponentsInChildren<Renderer>(true);
            Renderer[] objRenderers = sourceObj.GetComponentsInChildren<Renderer>(true);
            if (fbxRenderers.Length == 0)
                throw new InvalidDataException("Primary Tree_Pack.fbx imported without usable renderers.");
            if (objRenderers.Length == 0)
                throw new InvalidDataException("Primary Tree_Pack.obj imported without usable renderers.");

            Debug.Log($"Fallen Forest: dual-primary low-poly sources validated — FBX renderers={fbxRenderers.Length}, OBJ renderers={objRenderers.Length}.");

            // FBX and OBJ are two primary representations of the same authored pack. Building the
            // runtime prefab set from both would duplicate the same trees/rocks, so the FBX instance
            // is used for prefab extraction after both primary sources have passed import validation.
            GameObject instance = PrefabUtility.InstantiatePrefab(sourceFbx) as GameObject;
            if (instance == null) instance = UnityEngine.Object.Instantiate(sourceFbx);
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate primary user Tree_Pack.fbx.");

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
                Debug.Log($"Fallen Forest: dual-primary low-poly pack built into {treeCount} tree and {rockCount} rock prefabs from {logicalRoots.Count} logical FBX roots after FBX+OBJ validation.");
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
                throw new InvalidDataException("No LowPolyTree prefabs exist after building the user Tree_Pack sources.");

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

            if (treeSemantic != rockSemantic)
            {
                result.Add(node);
                return;
            }

            var rendererChildren = new List<Transform>();
            foreach (Transform child in node)
                if (child.GetComponentsInChildren<Renderer>(true).Length > 0)
                    rendererChildren.Add(child);

            if (rendererChildren.Count > 1)
            {
                int before = result.Count;
                foreach (Transform child in rendererChildren)
                    CollectLogicalRoots(child, result, depth + 1);
                if (result.Count > before) return;
            }

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

        private static void SetTexture(Material material, string urpProperty, string legacyProperty, Texture2D texture)
        {
            if (texture == null) return;
            if (material.HasProperty(urpProperty)) material.SetTexture(urpProperty, texture);
            if (material.HasProperty(legacyProperty)) material.SetTexture(legacyProperty, texture);
        }

        private static void AddTrunkCollider(GameObject root, Bounds bounds)
        {
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.center = root.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * .45f, bounds.center.z));
            collider.height = Mathf.Max(.8f, bounds.size.y * .76f);
            collider.radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * .22f, .12f, 1.2f);
        }

        private static void AddVisibilityOccluder(GameObject root, Bounds bounds)
        {
            VisibilityOccluder occluder = root.AddComponent<VisibilityOccluder>();
            SerializedObject so = new(occluder);
            SerializedProperty radius = so.FindProperty("radius");
            if (radius != null) radius.floatValue = Mathf.Max(.5f, Mathf.Max(bounds.extents.x, bounds.extents.z));
            SerializedProperty height = so.FindProperty("height");
            if (height != null) height.floatValue = Mathf.Max(1f, bounds.size.y);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Bounds CalculateBounds(IEnumerable<Renderer> renderers)
        {
            bool has = false;
            Bounds bounds = new(Vector3.zero, Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                if (!has) { bounds = renderer.bounds; has = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return has ? bounds : new Bounds(Vector3.zero, Vector3.one);
        }

        private static void NormalizeExtremeScale(GameObject root, ref Bounds bounds, float targetHeight, float minHeight, float maxHeight)
        {
            float h = Mathf.Max(.001f, bounds.size.y);
            if (h >= minHeight && h <= maxHeight) return;
            float scale = targetHeight / h;
            root.transform.localScale *= scale;
        }

        private static void DeleteOldGeneratedPrefabs()
        {
            if (!Directory.Exists(PrefabDir)) return;
            foreach (string path in Directory.GetFiles(PrefabDir, "*.prefab", SearchOption.TopDirectoryOnly))
                AssetDatabase.DeleteAsset(path.Replace('\\', '/'));
        }

        private static List<GameObject> LoadGeneratedPrefabs(string prefix)
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(p => p != null)
                .ToList();
        }

        private static void AppendTrees(ForestScatterer forest, List<GameObject> additions)
        {
            SerializedObject so = new(forest);
            SerializedProperty array = so.FindProperty("treePrefabs");
            var all = new List<GameObject>();
            for (int i = 0; i < array.arraySize; i++)
            {
                GameObject current = array.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (current != null && !current.name.StartsWith("LowPolyTree_", StringComparison.OrdinalIgnoreCase)) all.Add(current);
            }
            all.AddRange(additions);
            array.arraySize = all.Count;
            for (int i = 0; i < all.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(SerializedObject so, string property, UnityEngine.Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.objectReferenceValue = value;
        }
    }
}
#endif