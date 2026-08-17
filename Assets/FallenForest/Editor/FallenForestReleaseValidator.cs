#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using FallenForest.Cinematics;
using FallenForest.Documents;
using FallenForest.Monsters;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Video;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Validates actual Fallen Forest release dependencies. Historical temporary filenames are not
    /// release gates; exact user assets, generated gameplay prefabs/scenes and forbidden media are.
    /// </summary>
    public static class FallenForestReleaseValidator
    {
        private const string Root = "Assets/FallenForest";

        [MenuItem("Fallen Forest/Validate Release Content")]
        public static void ValidateFromMenu()
        {
            ValidateReleaseOrThrow();
            Debug.Log("Fallen Forest: release content validation PASSED.");
        }

        public static void ValidateReleaseOrThrow()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            RequireExternalModel("Locust", errors);
            RequireExternalModel("Boiled", errors);
            RequireImportedModel(Root + "/Art/Vegetation/UserGrass", "Grass", "user three-variant grass FBX", errors);
            RequireImportedModel(Root + "/Art/Viewmodel/Arms", "fpsarms", "user first-person arms", errors);
            RequireImportedModel(Root + "/Art/Viewmodel/Flashlight", "flashlight", "user flashlight", errors);
            RequireImportedModel(Root + "/Art/Vehicles/Pickup", "pickup", "user pickup truck", errors);
            RequireImportedModel(Root + "/Art/Documents/UserDocument", "document", "user document folder model", errors);

            RequireAsset<AudioClip>(Root + "/Audio/Menu/creepy_forest_menu.ogg", "CC0 horror menu track", errors);
            RequireAsset<AudioClip>(Root + "/Audio/Ambience/forest_ambience_cc0.mp3", "CC0 base forest ambience", errors);
            RequireAsset<AudioClip>(Root + "/Audio/Ambience/ambient_horror_cc0.ogg", "CC0 horror ambience layer", errors);
            RequireAsset<AudioClip>(Root + "/Audio/Screamers/jakes-screamer.mp3", "Locust jumpscare A", errors);
            RequireAsset<AudioClip>(Root + "/Audio/Screamers/the-screamer-shared-between-mallie-and-jenny.mp3", "Locust jumpscare B", errors);
            RequireAsset<VideoClip>(Root + "/Video/boiled_one_jumpscare.mp4", "Boiled One screamer video", errors);

            string forbidden = Root + "/Audio/Screamers/amazing-grace-analog-horror.mp3";
            if (File.Exists(forbidden) || AssetDatabase.LoadAssetAtPath<AudioClip>(forbidden) != null)
                errors.Add("Forbidden file is still present: amazing-grace-analog-horror.mp3");

            RequirePrefabWith<LocustAI>(Root + "/Prefabs/Locust_Final.prefab", "final Locust gameplay prefab", errors);
            RequirePrefabWith<BoiledOneEncounter>(Root + "/Prefabs/BoiledOne_Final.prefab", "final Boiled One gameplay prefab", errors);
            RequireAsset<GameObject>(Root + "/Prefabs/Vegetation/UserGrass_Large.prefab", "large user grass prefab", errors);
            RequireAsset<GameObject>(Root + "/Prefabs/Vegetation/UserGrass_Small.prefab", "small user grass prefab", errors);
            RequireAsset<GameObject>(Root + "/Prefabs/Vegetation/UserGrass_Tiny.prefab", "tiny user grass prefab", errors);
            RequireAsset<GameObject>(FinalUserAssetPrefabBuilder.ArmsPrefab, "final first-person arms prefab", errors);
            RequireAsset<GameObject>(FinalUserAssetPrefabBuilder.FlashlightPrefab, "final flashlight prefab", errors);
            RequirePrefabWith<DocumentPickup>(FinalUserAssetPrefabBuilder.DocumentPrefab, "final document pickup prefab", errors);
            RequirePrefabWith<CinematicPickupVehicle>(FinalUserAssetPrefabBuilder.PickupPrefab, "physics-ready final pickup truck prefab", errors);

            RequireAsset<SceneAsset>(Root + "/Scenes/MainMenu.unity", "MainMenu scene", errors);
            RequireAsset<SceneAsset>(Root + "/Scenes/Forest.unity", "Forest scene", errors);

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Icon/app_icon_1024.png");
            if (icon == null)
                warnings.Add("Android store icon is not authored yet; development APK may use the Unity/default icon.");
            else if (icon.width < 1024 || icon.height < 1024)
                warnings.Add($"Android icon should be >=1024x1024 before store release; imported {icon.width}x{icon.height}.");

            WarnMissing<AudioClip>(Root + "/Audio/Monster/locust_near_sting.wav", "Locust near sting", warnings);
            WarnMissing<AudioClip>(Root + "/Audio/Monster/boiled_trigger_sting.wav", "Boiled trigger sting", warnings);
            WarnMissing<AudioClip>(Root + "/Audio/Ending/car_pass_engine.wav", "ending pickup engine", warnings);

            foreach (string warning in warnings)
                Debug.LogWarning("Fallen Forest release warning: " + warning);

            if (errors.Count > 0)
            {
                string message = "Fallen Forest release validation failed:\n - " + string.Join("\n - ", errors);
                Debug.LogError(message);
                throw new BuildFailedException(message);
            }
        }

        private static void RequireExternalModel(string token, List<string> errors)
        {
            string folder = Root + "/Art/Models/DoctorNowhere/" + token;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                errors.Add($"Missing exact {token} model folder: {folder}");
                return;
            }

            if (FindRendererBearingImportedModel(folder, token) != null) return;
            errors.Add($"Exact downloadable {token} model has not been imported into {folder}. Placeholder geometry is forbidden in release APKs.");
        }

        private static void RequireImportedModel(string folder, string token, string label, List<string> errors)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                errors.Add($"Missing {label} folder: {folder}");
                return;
            }
            if (FindRendererBearingImportedModel(folder, token) == null)
                errors.Add($"Missing imported {label} under {folder}.");
        }

        private static GameObject FindRendererBearingImportedModel(string folder, string preferredToken)
        {
            GameObject fallback = null;
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".fbx" && ext != ".obj" && ext != ".gltf" && ext != ".glb") continue;
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null || model.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                if (fallback == null) fallback = model;
                if (Path.GetFileNameWithoutExtension(path).IndexOf(preferredToken, StringComparison.OrdinalIgnoreCase) >= 0)
                    return model;
            }
            return fallback;
        }

        private static void RequirePrefabWith<T>(string path, string label, List<string> errors) where T : Component
        {
            GameObject prefab = RequireAsset<GameObject>(path, label, errors);
            if (prefab != null && prefab.GetComponentInChildren<T>(true) == null)
                errors.Add($"{label} is present but has no {typeof(T).Name}: {path}");
        }

        private static T RequireAsset<T>(string path, string label, List<string> errors) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                errors.Add($"Missing {label}: {path}");
            return asset;
        }

        private static void WarnMissing<T>(string path, string label, List<string> warnings) where T : UnityEngine.Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) == null)
                warnings.Add($"Optional {label} is not present: {path}");
        }
    }
}
#endif
