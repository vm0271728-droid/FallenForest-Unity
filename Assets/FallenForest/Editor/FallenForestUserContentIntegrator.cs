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
    /// Integrates the user's canonical source assets before scene assembly and release validation.
    /// It never generates replacement geometry for missing user models.
    /// </summary>
    public static class FallenForestUserContentIntegrator
    {
        private const string Root = "Assets/FallenForest";
        private const string GrassModel = Root + "/Art/Vegetation/UserGrass/Source/Grass.fbx";
        private const string GrassPrefabDir = Root + "/Prefabs/Vegetation";
        private const string LargeGrass = GrassPrefabDir + "/UserGrass_Large.prefab";
        private const string SmallGrass = GrassPrefabDir + "/UserGrass_Small.prefab";
        private const string TinyGrass = GrassPrefabDir + "/UserGrass_Tiny.prefab";
        private const string BootstrapDir = Root + "/Generated/SceneBootstrap";
        private const string BootstrapGrass = BootstrapDir + "/RuntimeGrass.prefab";
        private const string ForestScene = Root + "/Scenes/Forest.unity";

        [MenuItem("Fallen Forest/Release/Integrate User Content")]
        public static void IntegrateFromMenu()
        {
            IntegrateBeforeSceneAssembly();
            FallenForestSceneAssembler.EnsureRequiredScenesForCI();
            PatchGeneratedForestScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Fallen Forest: canonical user content integration completed.");
        }

        public static void IntegrateBeforeSceneAssembly()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BuildGrassVariantsIfAvailable();
            FinalCreaturePrefabBuilder.BuildAvailable();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void PatchGeneratedForestScene()
        {
            if (!File.Exists(ForestScene)) return;
            GameObject large = AssetDatabase.LoadAssetAtPath<GameObject>(LargeGrass);
            GameObject small = AssetDatabase.LoadAssetAtPath<GameObject>(SmallGrass);
            GameObject tiny = AssetDatabase.LoadAssetAtPath<GameObject>(TinyGrass);
            if (large == null || small == null || tiny == null) return;

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ForestScene, OpenSceneMode.Single);
            ForestScatterer scatterer = UnityEngine.Object.FindFirstObjectByType<ForestScatterer>();
            if (scatterer != null)
            {
                SerializedObject so = new(scatterer);
                SerializedProperty grasses = so.FindProperty("grassPrefabs");
                if (grasses != null)
                {
                    grasses.arraySize = 3;
                    grasses.GetArrayElementAtIndex(0).objectReferenceValue = large;
                    grasses.GetArrayElementAtIndex(1).objectReferenceValue = small;
                    grasses.GetArrayElementAtIndex(2).objectReferenceValue = tiny;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            if (previous.IsValid() && previous.path != scene.path && !string.IsNullOrEmpty(previous.path))
                EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
        }

        private static void BuildGrassVariantsIfAvailable()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(GrassModel);
            if (source == null)
            {
                Debug.LogWarning($"Fallen Forest: user Grass.fbx is not imported yet: {GrassModel}");
                return;
            }

            Directory.CreateDirectory(GrassPrefabDir);
            Directory.CreateDirectory(BootstrapDir);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (instance == null) throw new InvalidOperationException("Could not instantiate user Grass.fbx.");

            try
            {
                List<Candidate> candidates = new();
                foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    Bounds bounds = renderer.bounds;
                    float footprint = Mathf.Max(.000001f, bounds.size.x * bounds.size.z);
                    candidates.Add(new Candidate(renderer.gameObject, footprint));
                }

                candidates.Sort((a, b) => b.Footprint.CompareTo(a.Footprint));
                if (candidates.Count < 3)
                    throw new InvalidOperationException($"User Grass.fbx must contain at least three mesh variants; found {candidates.Count}.");

                GameObject large = SaveVariant(candidates[0].Object, "UserGrass_Large", LargeGrass);
                GameObject small = SaveVariant(candidates[1].Object, "UserGrass_Small", SmallGrass);
                GameObject tiny = SaveVariant(candidates[2].Object, "UserGrass_Tiny", TinyGrass);

                // The scene assembler checks this path first. Saving the real largest grass variant
                // here prevents it from manufacturing its old crossed-quad bootstrap grass.
                SaveVariant(candidates[0].Object, "RuntimeGrass", BootstrapGrass);

                Debug.Log(
                    $"Fallen Forest: user grass classified by footprint. " +
                    $"Large={candidates[0].Object.name}:{candidates[0].Footprint:0.###}, " +
                    $"Small={candidates[1].Object.name}:{candidates[1].Footprint:0.###}, " +
                    $"Tiny={candidates[2].Object.name}:{candidates[2].Footprint:0.###}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static GameObject SaveVariant(GameObject sourceObject, string prefabName, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                AssetDatabase.DeleteAsset(path);

            GameObject root = new(prefabName);
            GameObject visual = UnityEngine.Object.Instantiate(sourceObject);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;

            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private readonly struct Candidate
        {
            public readonly GameObject Object;
            public readonly float Footprint;

            public Candidate(GameObject obj, float footprint)
            {
                Object = obj;
                Footprint = footprint;
            }
        }
    }
}
#endif
