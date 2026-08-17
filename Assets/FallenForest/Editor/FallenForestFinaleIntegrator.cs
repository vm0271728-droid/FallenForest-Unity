#if UNITY_EDITOR
using System.IO;
using FallenForest.Audio;
using FallenForest.Cinematics;
using FallenForest.Player;
using FallenForest.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Wires the final-run boundary, old road and exact physics pickup into the generated Forest scene.
    /// </summary>
    public static class FallenForestFinaleIntegrator
    {
        private const string Root = "Assets/FallenForest";
        private const string ForestScene = Root + "/Scenes/Forest.unity";
        private const string RoadMaterialPath = Root + "/Materials/FinalRoad.mat";

        [MenuItem("Fallen Forest/Release/Finalize Forest Ending")]
        public static void FinalizeFromMenu() => FinalizeForestEnding();

        public static void FinalizeForestEnding()
        {
            if (!File.Exists(ForestScene)) return;

            GameObject pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FinalUserAssetPrefabBuilder.PickupPrefab);
            if (pickupPrefab == null || pickupPrefab.GetComponent<CinematicPickupVehicle>() == null)
            {
                Debug.LogWarning("Fallen Forest: physics Pickup_Final is not available yet; finale wiring postponed.");
                return;
            }

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ForestScene, OpenSceneMode.Single);
            Terrain terrain = Object.FindFirstObjectByType<Terrain>();
            PlayerMotor player = Object.FindFirstObjectByType<PlayerMotor>();
            if (terrain == null || player == null)
            {
                Debug.LogWarning("Fallen Forest: terrain/player missing while finalizing ending.");
                return;
            }

            RemoveRoot(scene, "FinaleSystem");
            GameObject root = new("FinaleSystem");

            GameObject pickupObject = PrefabUtility.InstantiatePrefab(pickupPrefab, scene) as GameObject;
            if (pickupObject == null) pickupObject = Object.Instantiate(pickupPrefab);
            pickupObject.name = "FinalPickup_Physics";
            pickupObject.transform.SetParent(root.transform, true);
            CinematicPickupVehicle vehicle = pickupObject.GetComponent<CinematicPickupVehicle>();
            pickupObject.SetActive(false);

            AudioSource engine = pickupObject.GetComponent<AudioSource>();
            if (engine == null) engine = pickupObject.AddComponent<AudioSource>();
            engine.loop = true;
            engine.playOnAwake = false;
            engine.spatialBlend = 1f;
            engine.minDistance = 5f;
            engine.maxDistance = 48f;
            AudioClip engineClip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Ending/car_pass_engine.wav");
            if (engineClip != null) engine.clip = engineClip;

            EndSequence ending = root.AddComponent<EndSequence>();
            CanvasGroup fade = CreateFadeOverlay(root.transform, out Text endText);
            Material roadMaterial = EnsureRoadMaterial();

            SerializedObject so = new(ending);
            SetObject(so, "terrain", terrain);
            SetObject(so, "pickup", vehicle);
            SetObject(so, "pickupAudio", engine);
            SetObject(so, "audioDirector", Object.FindFirstObjectByType<AudioDirector>());
            SetObject(so, "fade", fade);
            SetObject(so, "endText", endText);
            SetObject(so, "roadMaterial", roadMaterial);
            so.ApplyModifiedPropertiesWithoutUndo();

            BoundaryContact contact = player.GetComponent<BoundaryContact>();
            if (contact == null) contact = player.gameObject.AddComponent<BoundaryContact>();
            SerializedObject contactSo = new(contact);
            SetObject(contactSo, "motor", player);
            contactSo.ApplyModifiedPropertiesWithoutUndo();

            BuildFourBoundaries(root.transform, terrain, ending);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            if (previous.IsValid() && previous.path != scene.path && !string.IsNullOrEmpty(previous.path))
                EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
        }

        private static void BuildFourBoundaries(Transform parent, Terrain terrain, EndSequence ending)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float y = origin.y + size.y * .5f;
            float height = Mathf.Max(80f, size.y + 30f);
            float thickness = 1.2f;

            CreateBoundary(parent, ending, "Boundary_North",
                new Vector3(origin.x + size.x * .5f, y, origin.z + size.z + thickness * .5f),
                new Vector3(size.x + 12f, height, thickness), Vector3.forward);
            CreateBoundary(parent, ending, "Boundary_South",
                new Vector3(origin.x + size.x * .5f, y, origin.z - thickness * .5f),
                new Vector3(size.x + 12f, height, thickness), Vector3.back);
            CreateBoundary(parent, ending, "Boundary_East",
                new Vector3(origin.x + size.x + thickness * .5f, y, origin.z + size.z * .5f),
                new Vector3(thickness, height, size.z + 12f), Vector3.right);
            CreateBoundary(parent, ending, "Boundary_West",
                new Vector3(origin.x - thickness * .5f, y, origin.z + size.z * .5f),
                new Vector3(thickness, height, size.z + 12f), Vector3.left);
        }

        private static void CreateBoundary(
            Transform parent,
            EndSequence ending,
            string name,
            Vector3 position,
            Vector3 size,
            Vector3 outward)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;

            InvisibleBoundary boundary = go.AddComponent<InvisibleBoundary>();
            SerializedObject so = new(boundary);
            SetObject(so, "endSequence", ending);
            SerializedProperty direction = so.FindProperty("outwardDirection");
            if (direction != null) direction.vector3Value = outward;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CanvasGroup CreateFadeOverlay(Transform parent, out Text endText)
        {
            GameObject canvasObject = new("EndingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            GameObject black = new("BlackFade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            black.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = black.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            black.GetComponent<Image>().color = Color.black;
            CanvasGroup group = black.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            GameObject end = new("EndText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            end.transform.SetParent(black.transform, false);
            RectTransform endRect = end.GetComponent<RectTransform>();
            endRect.anchorMin = new Vector2(.2f, .36f);
            endRect.anchorMax = new Vector2(.8f, .64f);
            endRect.offsetMin = endRect.offsetMax = Vector2.zero;
            endText = end.GetComponent<Text>();
            endText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            endText.fontSize = 54;
            endText.alignment = TextAnchor.MiddleCenter;
            endText.color = new Color(.86f, .86f, .84f, 1f);
            endText.text = "END";
            endText.gameObject.SetActive(false);
            return group;
        }

        private static Material EnsureRoadMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(RoadMaterialPath);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new(shader) { name = "FinalRoad" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(.052f, .049f, .044f, 1f));
            else if (material.HasProperty("_Color")) material.color = new Color(.052f, .049f, .044f, 1f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .07f);
            AssetDatabase.CreateAsset(material, RoadMaterialPath);
            return material;
        }

        private static void RemoveRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == name)
                    Object.DestroyImmediate(go);
        }

        private static void SetObject(SerializedObject so, string property, Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.objectReferenceValue = value;
        }
    }
}
#endif
