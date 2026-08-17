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

                ViewmodelMotionController motion = viewmodelCamera.GetComponent<ViewmodelMotionController>();
                if (motion == null) motion = viewmodelCamera.gameObject.AddComponent<ViewmodelMotionController>();

                SerializedObject so = new(motion);
                SetObject(so, "player", player);
                SetObject(so, "armsRoot", arms);
                SetObject(so, "flashlightVisualRoot", flashlightVisual);
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Fallen Forest: first-person arms/flashlight runtime motion wired.");
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
