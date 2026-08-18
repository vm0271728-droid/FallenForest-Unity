#if UNITY_EDITOR
using System.IO;
using FallenForest.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallenForest.EditorTools
{
    public static class FallenForestViewmodelMotionIntegrator
    {
        private const string ForestScene = "Assets/FallenForest/Scenes/Forest.unity";

        public static void Configure()
        {
            if (!File.Exists(ForestScene)) return;

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ForestScene, OpenSceneMode.Single);
            try
            {
                PlayerMotor player = Object.FindFirstObjectByType<PlayerMotor>(FindObjectsInactive.Include);
                Transform viewmodelCamera = FindTransform("ViewmodelCamera");
                Transform arms = FindTransform("FPSArms_Final");
                Transform flashlightVisual = FindTransform("FlashlightVisual_Final");

                if (player == null || viewmodelCamera == null || arms == null || flashlightVisual == null)
                    throw new InvalidDataException("Viewmodel motion integration requires PlayerMotor, ViewmodelCamera, FPSArms_Final and FlashlightVisual_Final.");

                Camera camera = viewmodelCamera.GetComponent<Camera>();
                if (camera == null)
                    throw new InvalidDataException("ViewmodelCamera object has no Camera component.");

                // The canonical world camera is 75°, while the arms have their own narrow camera.
                // A very small near plane prevents real wrist/forearm geometry from clipping during
                // pickups and death poses without allowing the world camera to see through walls.
                camera.fieldOfView = 61f;
                camera.nearClipPlane = .015f;
                camera.farClipPlane = 8f;
                camera.useOcclusionCulling = false;

                ViewmodelMotionController motion = viewmodelCamera.GetComponent<ViewmodelMotionController>();
                if (motion == null) motion = viewmodelCamera.gameObject.AddComponent<ViewmodelMotionController>();

                SerializedObject so = new(motion);
                SetObject(so, "player", player);
                SetObject(so, "armsRoot", arms);
                SetObject(so, "flashlightVisualRoot", flashlightVisual);
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(camera);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Fallen Forest: first-person arms/flashlight runtime motion wired at fixed 61 degree viewmodel FOV.");
            }
            finally
            {
                if (previous.IsValid() && !string.IsNullOrEmpty(previous.path) && previous.path != scene.path)
                    EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
            }
        }

        private static Transform FindTransform(string exactName)
        {
            foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.name == exactName)
                    return candidate;
            return null;
        }

        private static void SetObject(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }
    }
}
#endif
