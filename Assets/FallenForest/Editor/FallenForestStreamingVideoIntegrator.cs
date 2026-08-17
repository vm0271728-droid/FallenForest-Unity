#if UNITY_EDITOR
using System;
using System.IO;
using FallenForest.Cinematics;
using FallenForest.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Wires the user's original H.264 Boiled One video without importing it as a VideoClip.
    /// Unity 6 on Linux only imports VP8 video, while Android can play the original MP4 directly.
    /// </summary>
    public static class FallenForestStreamingVideoIntegrator
    {
        public const string StreamingVideoAssetPath = "Assets/StreamingAssets/FallenForest/Video/boiled_one_jumpscare.mp4";
        private const string ForestScene = "Assets/FallenForest/Scenes/Forest.unity";

        public static void ConfigureBoiledSequence()
        {
            if (!File.Exists(StreamingVideoAssetPath))
                throw new InvalidOperationException("Missing original Boiled One streaming video: " + StreamingVideoAssetPath);
            if (!File.Exists(ForestScene))
                throw new InvalidOperationException("Forest scene must exist before configuring Boiled One video.");

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ForestScene, OpenSceneMode.Single);
            try
            {
                Camera camera = FindByName<Camera>("PlayerCamera");
                if (camera == null)
                    throw new InvalidOperationException("Forest scene has no PlayerCamera for the Boiled One video sequence.");

                PlayerMotor player = Object.FindFirstObjectByType<PlayerMotor>(FindObjectsInactive.Include);
                CameraMotion motion = Object.FindFirstObjectByType<CameraMotion>(FindObjectsInactive.Include);

                BoiledOneSequence sequence = Object.FindFirstObjectByType<BoiledOneSequence>(FindObjectsInactive.Include);
                if (sequence == null)
                    sequence = new GameObject("BoiledOneSequence").AddComponent<BoiledOneSequence>();

                VideoPlayer video = sequence.GetComponent<VideoPlayer>();
                if (video == null)
                    video = sequence.gameObject.AddComponent<VideoPlayer>();

                video.playOnAwake = false;
                video.waitForFirstFrame = true;
                video.isLooping = false;
                video.source = VideoSource.Url;
                video.clip = null;
                // Runtime code replaces this relative marker with Application.streamingAssetsPath.
                video.url = BoiledOneSequence.StreamingVideoRelativePath;
                video.renderMode = VideoRenderMode.CameraNearPlane;
                video.targetCamera = camera;
                video.targetCameraAlpha = 1f;
                video.aspectRatio = VideoAspectRatio.FitInside;
                video.audioOutputMode = VideoAudioOutputMode.Direct;

                SerializedObject so = new(sequence);
                SetObject(so, "videoPlayer", video);
                SetObject(so, "playerMotor", player);
                SetObject(so, "cameraMotion", motion);
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("Fallen Forest: original Boiled One MP4 wired from StreamingAssets.");
            }
            finally
            {
                if (previous.IsValid() && !string.IsNullOrEmpty(previous.path) && previous.path != scene.path)
                    EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
            }
        }

        private static T FindByName<T>(string name) where T : Component
        {
            foreach (T component in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (component.gameObject.name == name)
                    return component;
            }
            return null;
        }

        private static void SetObject(SerializedObject so, string property, Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null)
                p.objectReferenceValue = value;
        }
    }
}
#endif
