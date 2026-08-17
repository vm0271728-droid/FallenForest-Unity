#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Makes the generated PlayerCamera a valid URP base camera before the content integrator
    /// touches cameraStack. Clean CI checkouts generate their URP assets at build time, and a
    /// freshly added UniversalAdditionalCameraData can otherwise resolve no renderer and return
    /// a null camera stack in Unity 6.
    /// </summary>
    public static class FallenForestViewmodelCameraUrpGuard
    {
        private const string ForestScene = "Assets/FallenForest/Scenes/Forest.unity";

        public static void PrepareForestBaseCamera()
        {
            if (!File.Exists(ForestScene))
                throw new FileNotFoundException("Forest scene is missing before URP camera preparation.", ForestScene);

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ForestScene, OpenSceneMode.Single);

            try
            {
                Camera worldCamera = FindCamera("PlayerCamera");
                if (worldCamera == null)
                    throw new InvalidOperationException("Forest PlayerCamera is missing before viewmodel integration.");

                UniversalAdditionalCameraData worldData = worldCamera.GetUniversalAdditionalCameraData();
                worldData.SetRenderer(0);
                worldData.renderType = CameraRenderType.Base;
                EditorUtility.SetDirty(worldData);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                Debug.Log("Fallen Forest: PlayerCamera prepared as URP Base camera on renderer 0 before viewmodel stacking.");
            }
            finally
            {
                if (previous.IsValid() && previous.path != scene.path && !string.IsNullOrEmpty(previous.path))
                    EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
            }
        }

        private static Camera FindCamera(string name)
        {
            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (camera.gameObject.name == name)
                    return camera;
            return null;
        }
    }
}
#endif
