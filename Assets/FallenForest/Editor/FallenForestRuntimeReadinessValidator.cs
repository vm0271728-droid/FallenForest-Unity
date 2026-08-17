#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using FallenForest.Monsters;
using FallenForest.Player;
using FallenForest.World;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Runtime-oriented gate for failures a successful compile cannot prove: real textured assets,
    /// final user trees, terrain physics, viewmodel motion, adaptive lighting and hallucination wiring.
    /// </summary>
    public static class FallenForestRuntimeReadinessValidator
    {
        private const string ForestScene = "Assets/FallenForest/Scenes/Forest.unity";
        private const string BlackSpruce = "Assets/FallenForest/Prefabs/Vegetation/Trees/BlackSpruce_LOD.prefab";

        public static void ValidateOrThrow()
        {
            var errors = new List<string>();

            RequireTexturedPrefab(FinalUserAssetPrefabBuilder.ArmsPrefab, "first-person arms", errors);
            RequireTexturedPrefab(FinalUserAssetPrefabBuilder.FlashlightPrefab, "flashlight", errors);
            RequireTexturedPrefab(FinalUserAssetPrefabBuilder.DocumentPrefab, "document", errors);
            RequireTexturedPrefab(FinalUserAssetPrefabBuilder.PickupPrefab, "pickup", errors);
            RequireTexturedPrefab("Assets/FallenForest/Prefabs/Locust_Final.prefab", "Locust", errors);
            RequireTexturedPrefab("Assets/FallenForest/Prefabs/BoiledOne_Final.prefab", "Boiled One", errors);
            RequireTexturedPrefab(BlackSpruce, "canonical Black Spruce", errors);

            GameObject locustPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FallenForest/Prefabs/Locust_Final.prefab");
            if (locustPrefab != null && locustPrefab.GetComponentInChildren<LocustProceduralAnimator>(true) == null)
                errors.Add("Final Locust prefab has no authored AnimatorController and no LocustProceduralAnimator fallback.");

            GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlackSpruce);
            if (treePrefab != null)
            {
                LODGroup lod = treePrefab.GetComponent<LODGroup>();
                if (lod == null || lod.GetLODs().Length < 5)
                    errors.Add("Canonical Black Spruce prefab must contain the supplied LOD0-LOD4 LODGroup.");
                if (treePrefab.GetComponent<CapsuleCollider>() == null)
                    errors.Add("Canonical Black Spruce prefab has no physical trunk CapsuleCollider.");
                if (treePrefab.GetComponentInChildren<VisibilityOccluder>(true) == null)
                    errors.Add("Canonical Black Spruce prefab has no lightweight foliage visibility occluder.");
            }

            if (!File.Exists(ForestScene))
            {
                errors.Add("Forest scene is missing before runtime readiness validation.");
            }
            else
            {
                Scene previous = SceneManager.GetActiveScene();
                try
                {
                    EditorSceneManager.OpenScene(ForestScene, OpenSceneMode.Single);

                    Terrain terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>(FindObjectsInactive.Include);
                    if (terrain == null || terrain.terrainData == null)
                    {
                        errors.Add("Generated Forest scene has no valid Terrain/TerrainData.");
                    }
                    else
                    {
                        TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
                        if (collider == null || !collider.enabled)
                            errors.Add("Forest Terrain has no enabled TerrainCollider.");
                        else if (collider.terrainData != terrain.terrainData)
                            errors.Add("Forest TerrainCollider is not bound to the active TerrainData.");
                        if (terrain.terrainData.terrainLayers == null || terrain.terrainData.terrainLayers.Length == 0)
                            errors.Add("Forest Terrain has no TerrainLayer and would render as an untextured/default floor.");
                    }

                    if (UnityEngine.Object.FindFirstObjectByType<WorldGenerationCoordinator>(FindObjectsInactive.Include) == null)
                        errors.Add("Forest scene has no WorldGenerationCoordinator for terrain-first runtime generation.");
                    if (UnityEngine.Object.FindFirstObjectByType<ViewmodelMotionController>(FindObjectsInactive.Include) == null)
                        errors.Add("Forest scene has no ViewmodelMotionController; first-person arms/flashlight would remain static.");
                    if (UnityEngine.Object.FindFirstObjectByType<AtmosphereController>(FindObjectsInactive.Include) == null)
                        errors.Add("Forest scene has no adaptive AtmosphereController.");
                    if (UnityEngine.Object.FindFirstObjectByType<WhiteEyesHallucination>(FindObjectsInactive.Include) == null)
                        errors.Add("Forest scene has no canonical White Eyes hallucination system.");

                    FlashlightController flashlight = UnityEngine.Object.FindFirstObjectByType<FlashlightController>(FindObjectsInactive.Include);
                    if (flashlight == null)
                        errors.Add("Forest scene has no FlashlightController.");

                    ForestScatterer scatterer = UnityEngine.Object.FindFirstObjectByType<ForestScatterer>(FindObjectsInactive.Include);
                    if (scatterer == null)
                    {
                        errors.Add("Forest scene has no ForestScatterer.");
                    }
                    else
                    {
                        SerializedObject so = new(scatterer);
                        SerializedProperty trees = so.FindProperty("treePrefabs");
                        if (trees == null || trees.arraySize == 0)
                        {
                            errors.Add("ForestScatterer has no final user tree prefabs.");
                        }
                        else
                        {
                            bool hasCanonical = false;
                            for (int i = 0; i < trees.arraySize; i++)
                            {
                                GameObject tree = trees.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                                if (tree == null) continue;
                                string path = AssetDatabase.GetAssetPath(tree);
                                if (path == BlackSpruce) hasCanonical = true;
                                if (path.IndexOf("Generated/SceneBootstrap", StringComparison.OrdinalIgnoreCase) >= 0)
                                    errors.Add("ForestScatterer still references bootstrap tree geometry: " + path);
                            }
                            if (!hasCanonical)
                                errors.Add("ForestScatterer is not wired to the canonical Black Spruce user prefab.");
                        }
                    }
                }
                finally
                {
                    if (previous.IsValid() && !string.IsNullOrEmpty(previous.path))
                        EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
                }
            }

            if (errors.Count == 0)
            {
                Debug.Log("Fallen Forest: runtime readiness PASSED (materials, user trees, terrain, motion, adaptive night, White Eyes).");
                return;
            }

            string message = "Fallen Forest runtime readiness failed:\n - " + string.Join("\n - ", errors);
            Debug.LogError(message);
            throw new BuildFailedException(message);
        }

        private static void RequireTexturedPrefab(string path, string label, List<string> errors)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                errors.Add($"Missing {label} prefab: {path}");
                return;
            }

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                errors.Add($"{label} prefab has no Renderer: {path}");
                return;
            }

            bool hasMaterial = false;
            bool hasTexture = false;
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    hasMaterial = true;
                    if (HasTexture(material, "_BaseMap") || HasTexture(material, "_MainTex"))
                    {
                        hasTexture = true;
                        break;
                    }
                }
                if (hasTexture) break;
            }

            if (!hasMaterial)
                errors.Add($"{label} prefab has no material assigned: {path}");
            else if (!hasTexture)
                errors.Add($"{label} prefab has materials but no base-color texture assigned: {path}");
        }

        private static bool HasTexture(Material material, string property)
        {
            return material.HasProperty(property) && material.GetTexture(property) != null;
        }
    }
}
#endif
