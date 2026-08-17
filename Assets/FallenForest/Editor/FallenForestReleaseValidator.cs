#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using FallenForest.Audio;
using FallenForest.Cinematics;
using FallenForest.Documents;
using FallenForest.Monsters;
using FallenForest.Player;
using FallenForest.UI;
using FallenForest.World;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace FallenForest.EditorTools
{
    /// <summary>Validates real release assets plus the generated scene wiring required for playable APKs.</summary>
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
            GameObject pickup = RequirePrefabWith<CinematicPickupVehicle>(FinalUserAssetPrefabBuilder.PickupPrefab, "physics-ready final pickup truck prefab", errors);
            if (pickup != null && pickup.GetComponentsInChildren<WheelCollider>(true).Length != 4)
                errors.Add("Final pickup prefab must contain exactly four WheelCollider components.");

            SceneAsset mainMenu = RequireAsset<SceneAsset>(Root + "/Scenes/MainMenu.unity", "MainMenu scene", errors);
            SceneAsset forest = RequireAsset<SceneAsset>(Root + "/Scenes/Forest.unity", "Forest scene", errors);
            if (mainMenu != null && forest != null)
                ValidateGeneratedSceneWiring(errors);

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

        private static void ValidateGeneratedSceneWiring(List<string> errors)
        {
            Scene previous = SceneManager.GetActiveScene();
            try
            {
                EditorSceneManager.OpenScene(Root + "/Scenes/Forest.unity", OpenSceneMode.Single);
                RequireSceneComponent<PlayerMotor>("PlayerMotor", errors);
                RequireSceneComponent<CameraMotion>("CameraMotion", errors);
                RequireSceneComponent<MonsterDirector>("MonsterDirector", errors);
                RequireSceneComponent<FlashlightMonsterDetector>("Flashlight monster detector", errors);
                RequireSceneComponent<WakeUpSequence>("opening/wake-up sequence", errors);
                RequireSceneComponent<BoiledOneSequence>("Boiled One video sequence", errors);
                RequireSceneComponent<JumpscareController>("Locust jumpscare controller", errors);
                RequireSceneComponent<DeathMenuController>("death/continue menu", errors);
                RequireSceneComponent<AudioDirector>("forest audio director", errors);
                RequireSceneComponent<WindInteractor>("grass/player wind interaction", errors);
                RequireSceneComponent<RuntimeQualityController>("mobile runtime quality controller", errors);
                RequireSceneComponent<EndSequence>("final road ending sequence", errors);

                CinematicPickupVehicle vehicle = Object.FindFirstObjectByType<CinematicPickupVehicle>(FindObjectsInactive.Include);
                if (vehicle == null)
                    errors.Add("Forest scene has no physical cinematic pickup vehicle.");
                else if (vehicle.GetComponentsInChildren<WheelCollider>(true).Length != 4)
                    errors.Add("Forest scene cinematic pickup does not contain exactly four WheelColliders.");

                EditorSceneManager.OpenScene(Root + "/Scenes/MainMenu.unity", OpenSceneMode.Single);
                RequireSceneComponent<MenuLocalizationController>("menu localization controller", errors);
                RequireSceneComponent<CreditsPanelController>("full Credits panel controller", errors);

                bool menuTrackWired = false;
                foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (source.clip != null && source.clip.name.IndexOf("creepy_forest_menu", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        menuTrackWired = true;
                        break;
                    }
                }
                if (!menuTrackWired)
                    errors.Add("MainMenu scene does not have the vetted menu audio track wired to an AudioSource.");
            }
            finally
            {
                if (previous.IsValid() && !string.IsNullOrEmpty(previous.path))
                    EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
            }
        }

        private static void RequireSceneComponent<T>(string label, List<string> errors) where T : Component
        {
            if (Object.FindFirstObjectByType<T>(FindObjectsInactive.Include) == null)
                errors.Add($"Generated scene is missing {label} ({typeof(T).Name}).");
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

        private static GameObject RequirePrefabWith<T>(string path, string label, List<string> errors) where T : Component
        {
            GameObject prefab = RequireAsset<GameObject>(path, label, errors);
            if (prefab != null && prefab.GetComponentInChildren<T>(true) == null)
                errors.Add($"{label} is present but has no {typeof(T).Name}: {path}");
            return prefab;
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
