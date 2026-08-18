#if UNITY_EDITOR
using System.IO;
using FallenForest.Cinematics;
using FallenForest.Input;
using FallenForest.Player;
using FallenForest.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Final wiring for canonical systems added after the original scene bootstrap pipeline.
    /// Keeps the generated scene deterministic while ensuring runtime references are serialized.
    /// </summary>
    public static class FallenForestCanonicalRuntimeIntegrator
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
                Camera camera = Camera.main;
                FlashlightController flashlight = Object.FindFirstObjectByType<FlashlightController>(FindObjectsInactive.Include);
                ViewmodelMotionController viewmodel = Object.FindFirstObjectByType<ViewmodelMotionController>(FindObjectsInactive.Include);
                TouchLookInput look = Object.FindFirstObjectByType<TouchLookInput>(FindObjectsInactive.Include);
                AtmosphereController atmosphere = Object.FindFirstObjectByType<AtmosphereController>(FindObjectsInactive.Include);
                JumpscareController jumpscare = Object.FindFirstObjectByType<JumpscareController>(FindObjectsInactive.Include);

                if (player == null || camera == null || flashlight == null)
                    throw new InvalidDataException("Canonical runtime wiring requires PlayerMotor, MainCamera and FlashlightController.");

                UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;

                GameObject flashlightVisual = FindGameObject("FlashlightVisual_Final");
                SerializedObject flashSo = new(flashlight);
                SetObject(flashSo, "rayOrigin", flashlight.transform);
                SetObject(flashSo, "visualRoot", flashlightVisual);
                flashSo.ApplyModifiedPropertiesWithoutUndo();

                if (look != null)
                {
                    SerializedObject lookSo = new(look);
                    SetObject(lookSo, "flashlight", flashlight);
                    lookSo.ApplyModifiedPropertiesWithoutUndo();
                }

                if (viewmodel != null)
                {
                    SerializedObject vmSo = new(viewmodel);
                    SetObject(vmSo, "gameplayFlashlightRoot", flashlight.transform);
                    vmSo.ApplyModifiedPropertiesWithoutUndo();

                    FlashlightIdleAnimator idleAnimator = viewmodel.GetComponent<FlashlightIdleAnimator>();
                    if (idleAnimator == null) idleAnimator = viewmodel.gameObject.AddComponent<FlashlightIdleAnimator>();
                    SerializedObject idleSo = new(idleAnimator);
                    SetObject(idleSo, "player", player);
                    SetObject(idleSo, "flashlight", flashlight);
                    SetObject(idleSo, "viewmodel", viewmodel);
                    idleSo.ApplyModifiedPropertiesWithoutUndo();
                }

                if (atmosphere != null)
                {
                    SerializedObject atmosphereSo = new(atmosphere);
                    SetObject(atmosphereSo, "targetCamera", camera);
                    SetObject(atmosphereSo, "flashlight", flashlight);
                    atmosphereSo.ApplyModifiedPropertiesWithoutUndo();
                }

                if (jumpscare != null)
                {
                    SerializedObject jumpSo = new(jumpscare);
                    SetObject(jumpSo, "viewmodelMotion", viewmodel);
                    SetObject(jumpSo, "flashlight", flashlight);
                    jumpSo.ApplyModifiedPropertiesWithoutUndo();
                }

                GameObject helpers = FindGameObject("RuntimeWorldHelpers");
                if (helpers == null) helpers = new GameObject("RuntimeWorldHelpers");
                WhiteEyesHallucination eyes = helpers.GetComponent<WhiteEyesHallucination>();
                if (eyes == null) eyes = helpers.AddComponent<WhiteEyesHallucination>();
                SerializedObject eyeSo = new(eyes);
                SetObject(eyeSo, "player", player);
                SetObject(eyeSo, "playerCamera", camera);
                eyeSo.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(camera);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Fallen Forest: canonical flashlight, idle variants, exposure, viewmodel, jumpscare and White Eyes wiring completed.");
            }
            finally
            {
                if (previous.IsValid() && !string.IsNullOrEmpty(previous.path) && previous.path != scene.path)
                    EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
            }
        }

        private static GameObject FindGameObject(string exactName)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == exactName) return t.gameObject;
            return null;
        }

        private static void SetObject(SerializedObject so, string property, Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.objectReferenceValue = value;
        }
    }
}
#endif
