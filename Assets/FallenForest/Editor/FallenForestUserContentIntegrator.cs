#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using FallenForest.Cinematics;
using FallenForest.Documents;
using FallenForest.Monsters;
using FallenForest.Player;
using FallenForest.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

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
        private const string ViewmodelLayerName = "Viewmodel";

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
            FinalUserAssetPrefabBuilder.BuildAvailable();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void PatchGeneratedForestScene()
        {
            if (!File.Exists(ForestScene)) return;

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ForestScene, OpenSceneMode.Single);
            bool dirty = false;

            dirty |= PatchGrassScatterer();
            dirty |= PatchExactDocumentPrefab();
            dirty |= PatchPlayerViewmodel(scene);
            dirty |= PatchFlashlightPickup(scene);
            dirty |= PatchMonsterSystems(scene);
            dirty |= PatchBoiledVideoSequence();

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (previous.IsValid() && previous.path != scene.path && !string.IsNullOrEmpty(previous.path))
                EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
        }

        private static bool PatchGrassScatterer()
        {
            GameObject large = AssetDatabase.LoadAssetAtPath<GameObject>(LargeGrass);
            GameObject small = AssetDatabase.LoadAssetAtPath<GameObject>(SmallGrass);
            GameObject tiny = AssetDatabase.LoadAssetAtPath<GameObject>(TinyGrass);
            if (large == null || small == null || tiny == null) return false;

            ForestScatterer scatterer = UnityEngine.Object.FindFirstObjectByType<ForestScatterer>();
            if (scatterer == null) return false;

            SerializedObject so = new(scatterer);
            SerializedProperty grasses = so.FindProperty("grassPrefabs");
            if (grasses == null || !grasses.isArray) return false;

            grasses.arraySize = 3;
            grasses.GetArrayElementAtIndex(0).objectReferenceValue = large;
            grasses.GetArrayElementAtIndex(1).objectReferenceValue = small;
            grasses.GetArrayElementAtIndex(2).objectReferenceValue = tiny;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool PatchExactDocumentPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FinalUserAssetPrefabBuilder.DocumentPrefab);
            if (prefab == null) return false;
            DocumentPickup pickup = prefab.GetComponent<DocumentPickup>();
            DocumentSpawner spawner = UnityEngine.Object.FindFirstObjectByType<DocumentSpawner>();
            if (pickup == null || spawner == null) return false;

            SerializedObject so = new(spawner);
            SerializedProperty prop = so.FindProperty("documentPrefab");
            if (prop == null) return false;
            prop.objectReferenceValue = pickup;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool PatchPlayerViewmodel(Scene scene)
        {
            GameObject armsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FinalUserAssetPrefabBuilder.ArmsPrefab);
            GameObject flashlightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FinalUserAssetPrefabBuilder.FlashlightPrefab);
            if (armsPrefab == null && flashlightPrefab == null) return false;

            Camera worldCamera = FindByName<Camera>("PlayerCamera");
            if (worldCamera == null) return false;

            int viewmodelLayer = EnsureLayer(ViewmodelLayerName);
            if (viewmodelLayer < 0)
            {
                Debug.LogWarning("Fallen Forest: no free Unity layer available for the FPS viewmodel.");
                return false;
            }

            int mask = 1 << viewmodelLayer;
            worldCamera.cullingMask &= ~mask;
            worldCamera.fieldOfView = 75f;

            Transform old = worldCamera.transform.Find("ViewmodelCamera");
            Camera viewmodelCamera;
            if (old != null)
            {
                viewmodelCamera = old.GetComponent<Camera>();
                if (viewmodelCamera == null) viewmodelCamera = old.gameObject.AddComponent<Camera>();
            }
            else
            {
                GameObject vm = new("ViewmodelCamera");
                vm.transform.SetParent(worldCamera.transform, false);
                viewmodelCamera = vm.AddComponent<Camera>();
            }

            viewmodelCamera.transform.localPosition = Vector3.zero;
            viewmodelCamera.transform.localRotation = Quaternion.identity;
            viewmodelCamera.fieldOfView = 61f;
            viewmodelCamera.nearClipPlane = .01f;
            viewmodelCamera.farClipPlane = 8f;
            viewmodelCamera.cullingMask = mask;
            viewmodelCamera.clearFlags = CameraClearFlags.Depth;
            viewmodelCamera.depth = worldCamera.depth + 1f;

            UniversalAdditionalCameraData worldData = worldCamera.GetUniversalAdditionalCameraData();
            UniversalAdditionalCameraData vmData = viewmodelCamera.GetUniversalAdditionalCameraData();
            vmData.renderType = CameraRenderType.Overlay;
            vmData.renderPostProcessing = false;
            if (!worldData.cameraStack.Contains(viewmodelCamera))
                worldData.cameraStack.Add(viewmodelCamera);

            RemoveChild(viewmodelCamera.transform, "FPSArms_Final");
            if (armsPrefab != null)
            {
                GameObject arms = InstantiatePrefab(armsPrefab, scene, viewmodelCamera.transform, "FPSArms_Final");
                SetLayerRecursively(arms, viewmodelLayer);
            }

            GameObject flashlightObject = GameObject.Find("Flashlight");
            if (flashlightObject != null && flashlightPrefab != null)
            {
                RemoveChild(flashlightObject.transform, "FlashlightVisual_Final");
                GameObject visual = InstantiatePrefab(flashlightPrefab, scene, flashlightObject.transform, "FlashlightVisual_Final");
                SetLayerRecursively(visual, viewmodelLayer);
            }

            return true;
        }

        private static bool PatchFlashlightPickup(Scene scene)
        {
            GameObject exact = AssetDatabase.LoadAssetAtPath<GameObject>(FinalUserAssetPrefabBuilder.FlashlightPrefab);
            GameObject pickup = GameObject.Find("FlashlightPickup");
            if (exact == null || pickup == null) return false;

            MeshRenderer primitive = pickup.GetComponent<MeshRenderer>();
            if (primitive != null) primitive.enabled = false;

            Transform outline = pickup.transform.Find("SubtlePickupOutline");
            if (outline != null)
            {
                Renderer r = outline.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }

            RemoveChild(pickup.transform, "FlashlightModel_Final");
            GameObject model = InstantiatePrefab(exact, scene, pickup.transform, "FlashlightModel_Final");
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            return true;
        }

        private static bool PatchMonsterSystems(Scene scene)
        {
            GameObject locustPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Locust_Final.prefab");
            GameObject boiledPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/BoiledOne_Final.prefab");
            if (locustPrefab == null || boiledPrefab == null) return false;

            PlayerMotor player = UnityEngine.Object.FindFirstObjectByType<PlayerMotor>();
            Camera playerCamera = FindByName<Camera>("PlayerCamera");
            ForestSpatialIndex forest = UnityEngine.Object.FindFirstObjectByType<ForestSpatialIndex>();
            Terrain terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            if (player == null || playerCamera == null) return false;

            MonsterDirector director = UnityEngine.Object.FindFirstObjectByType<MonsterDirector>();
            if (director == null)
                director = new GameObject("MonsterDirector").AddComponent<MonsterDirector>();

            SerializedObject so = new(director);
            SetObject(so, "player", player.transform);
            SetObject(so, "playerCamera", playerCamera);
            SetObject(so, "playerMotor", player);
            SetObject(so, "locustPrefab", locustPrefab.GetComponent<LocustAI>());
            SetObject(so, "boiledPrefab", boiledPrefab.GetComponent<BoiledOneEncounter>());
            SetObject(so, "forestIndex", forest);
            SetObject(so, "terrain", terrain);
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool PatchBoiledVideoSequence()
        {
            VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(Root + "/Video/boiled_one_jumpscare.mp4");
            if (clip == null) return false;
            Camera camera = FindByName<Camera>("PlayerCamera");
            PlayerMotor player = UnityEngine.Object.FindFirstObjectByType<PlayerMotor>();
            CameraMotion motion = UnityEngine.Object.FindFirstObjectByType<CameraMotion>();
            if (camera == null) return false;

            BoiledOneSequence sequence = UnityEngine.Object.FindFirstObjectByType<BoiledOneSequence>();
            if (sequence == null)
                sequence = new GameObject("BoiledOneSequence").AddComponent<BoiledOneSequence>();

            VideoPlayer video = sequence.GetComponent<VideoPlayer>();
            if (video == null) video = sequence.gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.waitForFirstFrame = true;
            video.isLooping = false;
            video.clip = clip;
            video.renderMode = VideoRenderMode.CameraNearPlane;
            video.targetCamera = camera;
            video.targetCameraAlpha = 1f;
            video.aspectRatio = VideoAspectRatio.FitInside;

            SerializedObject so = new(sequence);
            SetObject(so, "videoPlayer", video);
            SetObject(so, "playerMotor", player);
            SetObject(so, "cameraMotion", motion);
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static T FindByName<T>(string name) where T : Component
        {
            foreach (T component in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (component.gameObject.name == name)
                    return component;
            return null;
        }

        private static GameObject InstantiatePrefab(GameObject prefab, Scene scene, Transform parent, string name)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null) instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return instance;
        }

        private static void RemoveChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return -1;
            SerializedObject tagManager = new(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null) return -1;

            for (int i = 8; i < 32; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(element.stringValue)) continue;
                element.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return i;
            }
            return -1;
        }

        private static void SetObject(SerializedObject so, string property, UnityEngine.Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.objectReferenceValue = value;
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

                SaveVariant(candidates[0].Object, "UserGrass_Large", LargeGrass);
                SaveVariant(candidates[1].Object, "UserGrass_Small", SmallGrass);
                SaveVariant(candidates[2].Object, "UserGrass_Tiny", TinyGrass);

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
