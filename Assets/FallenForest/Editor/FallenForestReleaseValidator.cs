#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Video;

namespace FallenForest.EditorTools
{
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

            RequireExternalModel("Locust", errors);
            RequireExternalModel("Boiled", errors);

            RequireAsset<AudioClip>(Root + "/Audio/Menu/creepy_forest_menu.ogg", "CC0 horror menu track", errors);
            RequireAsset<AudioClip>(Root + "/Audio/Ambience/forest_ambience_cc0.mp3", "CC0 base forest ambience", errors);
            RequireAsset<AudioClip>(Root + "/Audio/Ambience/ambient_horror_cc0.ogg", "CC0 horror ambience layer", errors);

            RequireAsset<AudioClip>(Root + "/Audio/Screamers/jakes-screamer.mp3", "Locust jumpscare A", errors);
            RequireAsset<AudioClip>(Root + "/Audio/Screamers/the-screamer-shared-between-mallie-and-jenny.mp3", "Locust jumpscare B", errors);
            RequireAsset<AudioClip>(Root + "/Audio/Monster/locust_near_sting.wav", "Locust close-contact sting", errors);
            RequireAsset<AudioClip>(Root + "/Audio/Monster/boiled_trigger_sting.wav", "Boiled trigger sting", errors);
            RequireAsset<AudioClip>(Root + "/Audio/Ending/car_pass_engine.wav", "ending car engine", errors);
            RequireAsset<VideoClip>(Root + "/Video/boiled_one_jumpscare.mp4", "Boiled One video", errors);

            if (File.Exists(Root + "/Audio/Screamers/amazing-grace-analog-horror.mp3"))
                errors.Add("Forbidden file is still present: amazing-grace-analog-horror.mp3");

            Texture2D icon = RequireAsset<Texture2D>(Root + "/Art/Icon/app_icon_1024.png", "Android icon", errors);
            if (icon != null && (icon.width < 1024 || icon.height < 1024))
                errors.Add($"Android icon must be >=1024x1024; imported {icon.width}x{icon.height}.");

            string[] textures =
            {
                Root + "/Art/Textures/Ground/forest_ground_03_diff_2k.jpg",
                Root + "/Art/Textures/Ground/forest_ground_03_normal_gl_2k.jpg",
                Root + "/Art/Textures/PineBark/pine_bark_diff_2k.jpg",
                Root + "/Art/Textures/PineBark/pine_bark_normal_gl_2k.jpg",
                Root + "/Art/Textures/Path/forest_path_diff_2k.jpg",
                Root + "/Art/Textures/Path/forest_path_normal_gl_2k.jpg",
                Root + "/Art/Textures/Vegetation/spruce_branch_rgba_2k.png",
                Root + "/Art/Textures/Vegetation/grass_clump_rgba_2k.png",
                Root + "/Art/Textures/Documents/folder_cardboard_diff_2k.png",
                Root + "/Art/Textures/Documents/folder_cardboard_normal_gl_2k.png",
                Root + "/Art/Textures/Documents/dirty_documents_diff_2k.png"
            };
            foreach (string path in textures)
                RequireAsset<Texture2D>(path, "2K environment texture", errors);

            RequireAsset<SceneAsset>(Root + "/Scenes/MainMenu.unity", "MainMenu scene", errors);
            RequireAsset<SceneAsset>(Root + "/Scenes/Forest.unity", "Forest scene", errors);
            RequireAsset<GameObject>(Root + "/Prefabs/Locust_Final.prefab", "final Locust gameplay prefab", errors);
            RequireAsset<GameObject>(Root + "/Prefabs/BoiledOne_Final.prefab", "final Boiled gameplay prefab", errors);

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

            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null && model.GetComponentsInChildren<Renderer>(true).Length > 0)
                    return;
            }
            errors.Add($"Exact downloadable {token} model has not been imported into {folder}. Placeholder geometry is forbidden in release APKs.");
        }

        private static T RequireAsset<T>(string path, string label, List<string> errors) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                errors.Add($"Missing {label}: {path}");
            return asset;
        }
    }
}
#endif
