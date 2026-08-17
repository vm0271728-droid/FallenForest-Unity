#if UNITY_EDITOR
using System;
using System.IO;
using FallenForest.Core;
using FallenForest.Documents;
using FallenForest.Input;
using FallenForest.Player;
using FallenForest.UI;
using FallenForest.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Deterministic scene assembler used to remove the current "missing MainMenu/Forest scenes"
    /// release blocker without baking a fake flat world. The Forest scene is wired to the real
    /// runtime terrain/trail/forest/document systems, so the scene remains a source-level shell and
    /// the final world is generated in the correct order when play starts.
    ///
    /// This intentionally does not manufacture placeholder Locust/Boiled release models. The final
    /// creature prefabs are still built only after the exact user archives have been imported.
    /// </summary>
    public static class FallenForestSceneAssembler
    {
        private const string Root = "Assets/FallenForest";
        public const string MainMenuPath = Root + "/Scenes/MainMenu.unity";
        public const string ForestPath = Root + "/Scenes/Forest.unity";
        private const string GeneratedDir = Root + "/Generated/SceneBootstrap";
        private const string TreePrefabPath = GeneratedDir + "/RuntimeSpruce.prefab";
        private const string GrassPrefabPath = GeneratedDir + "/RuntimeGrass.prefab";
        private const string DocumentPrefabPath = GeneratedDir + "/DocumentFolder.prefab";
        private const string TerrainAssetPath = GeneratedDir + "/ForestTerrain.asset";

        [MenuItem("Fallen Forest/Scenes/Rebuild Required Scenes")]
        public static void RebuildRequiredScenes()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            GameObject treePrefab = EnsureTreePrefab();
            GameObject grassPrefab = EnsureGrassPrefab();
            GameObject documentPrefab = EnsureDocumentPrefab();

            BuildMainMenuScene();
            BuildForestScene(treePrefab, grassPrefab, documentPrefab);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(ForestPath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Fallen Forest: MainMenu and Forest source scenes rebuilt.");
        }

        /// <summary>CI-safe entry point. Only rebuilds when one of the required scenes is absent.</summary>
        public static void EnsureRequiredScenesForCI()
        {
            if (File.Exists(MainMenuPath) && File.Exists(ForestPath)) return;
            RebuildRequiredScenes();
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Generated");
            Directory.CreateDirectory(GeneratedDir);
            AssetDatabase.Refresh();
        }

        private static void BuildMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MainMenu";

            Camera camera = new GameObject("MenuCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.002f, .003f, .004f, 1f);
            camera.transform.SetPositionAndRotation(new Vector3(0f, 2.2f, -11.5f), Quaternion.Euler(3.5f, 0f, 0f));

            Light moon = new GameObject("DimMenuMoon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = .018f;
            moon.color = new Color(.55f, .64f, .76f);
            moon.transform.rotation = Quaternion.Euler(56f, -28f, 0f);

            // Very dark silhouettes give the menu depth even before the final background art is committed.
            Material silhouette = NewRuntimeMaterial("MenuSilhouette", new Color(.006f, .012f, .008f, 1f), 0f);
            UnityEngine.Random.State oldState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(74129);
            GameObject forestRoot = new("MenuForestSilhouettes");
            for (int i = 0; i < 34; i++)
            {
                float x = UnityEngine.Random.Range(-15f, 15f);
                float z = UnityEngine.Random.Range(1.5f, 22f);
                float h = UnityEngine.Random.Range(4.5f, 10.5f);
                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = "TreeSilhouette";
                trunk.transform.SetParent(forestRoot.transform, true);
                trunk.transform.position = new Vector3(x, h * .45f - .4f, z);
                trunk.transform.localScale = new Vector3(.22f, h * .45f, .22f);
                trunk.GetComponent<Renderer>().sharedMaterial = silhouette;
                UnityEngine.Object.DestroyImmediate(trunk.GetComponent<Collider>());
            }
            UnityEngine.Random.state = oldState;

            Canvas canvas = CreateCanvas("MenuCanvas", 100);
            GameObject mainPanel = CreatePanel(canvas.transform, "MainPanel", new Color(0f, 0f, 0f, .18f));
            GameObject settingsPanel = CreatePanel(canvas.transform, "SettingsPanel", new Color(0f, 0f, 0f, .88f));
            settingsPanel.SetActive(false);

            MainMenuController controller = mainPanel.AddComponent<MainMenuController>();
            SetObject(controller, "mainPanel", mainPanel);
            SetObject(controller, "settingsPanel", settingsPanel);

            Text title = CreateText(mainPanel.transform, "Title", "FALLEN FOREST", 68, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(.18f, .72f), new Vector2(.82f, .91f), Vector2.zero, Vector2.zero);
            title.color = new Color(.76f, .77f, .75f, 1f);

            Button play = CreateButton(mainPanel.transform, "Play", "ИГРАТЬ", new Vector2(0f, 48f));
            Button settings = CreateButton(mainPanel.transform, "Settings", "НАСТРОЙКИ", new Vector2(0f, -30f));
            Button quit = CreateButton(mainPanel.transform, "Quit", "ВЫЙТИ", new Vector2(0f, -108f));
            play.onClick.AddListener(controller.Play);
            settings.onClick.AddListener(controller.OpenSettings);
            quit.onClick.AddListener(controller.Quit);

            SettingsMenuController settingsController = settingsPanel.AddComponent<SettingsMenuController>();
            Text settingsTitle = CreateText(settingsPanel.transform, "Title", "НАСТРОЙКИ", 44, TextAnchor.MiddleCenter);
            SetRect(settingsTitle.rectTransform, new Vector2(.25f, .77f), new Vector2(.75f, .9f), Vector2.zero, Vector2.zero);

            Slider sensitivity = CreateSlider(settingsPanel.transform, "Sensitivity", "ЧУВСТВИТЕЛЬНОСТЬ", new Vector2(0f, 95f), out Text sensValue);
            Slider fov = CreateSlider(settingsPanel.transform, "FOV", "FOV", new Vector2(0f, 5f), out Text fovValue);
            Slider shake = CreateSlider(settingsPanel.transform, "Shake", "ТРЯСКА КАМЕРЫ", new Vector2(0f, -85f), out Text shakeValue);
            Button back = CreateButton(settingsPanel.transform, "Back", "НАЗАД", new Vector2(0f, -195f));

            sensitivity.onValueChanged.AddListener(settingsController.SetSensitivity);
            fov.onValueChanged.AddListener(settingsController.SetFov);
            shake.onValueChanged.AddListener(settingsController.SetShake);
            back.onClick.AddListener(controller.CloseSettings);

            SetObject(settingsController, "sensitivitySlider", sensitivity);
            SetObject(settingsController, "fovSlider", fov);
            SetObject(settingsController, "shakeSlider", shake);
            SetObject(settingsController, "sensitivityValue", sensValue);
            SetObject(settingsController, "fovValue", fovValue);
            SetObject(settingsController, "shakeValue", shakeValue);

            EnsureEventSystem();
            EditorSceneManager.SaveScene(scene, MainMenuPath);
            UnityEngine.Object.DestroyImmediate(silhouette);
        }

        private static void BuildForestScene(GameObject treePrefab, GameObject grassPrefab, GameObject documentPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Forest";

            Terrain terrain = CreateTerrainAsset();
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            Vector3 startPosition = origin + new Vector3(size.x * .5f, 0f, size.z * .5f);
            startPosition.y = terrain.SampleHeight(startPosition) + origin.y + .08f;

            GameObject startObject = new("PlayerStart");
            startObject.transform.position = startPosition;

            TerrainReliefGenerator relief = terrain.gameObject.AddComponent<TerrainReliefGenerator>();
            SetObject(relief, "terrain", terrain);
            SetObject(relief, "startPoint", startObject.transform);
            SetBool(relief, "generateOnAwake", false);

            GameObject worldRoot = new("WorldGeneration");
            ForestSpatialIndex spatial = worldRoot.AddComponent<ForestSpatialIndex>();
            TrailNetworkGenerator trails = worldRoot.AddComponent<TrailNetworkGenerator>();
            SetObject(trails, "terrain", terrain);
            SetBool(trails, "generateOnAwake", false);

            ForestScatterer scatterer = worldRoot.AddComponent<ForestScatterer>();
            SetObject(scatterer, "terrain", terrain);
            SetObject(scatterer, "spatialIndex", spatial);
            SetObject(scatterer, "startPoint", startObject.transform);
            SetBool(scatterer, "generateOnStart", false);
            SetGameObjectArray(scatterer, "treePrefabs", new[] { treePrefab });
            SetGameObjectArray(scatterer, "grassPrefabs", new[] { grassPrefab });

            WorldGenerationCoordinator coordinator = worldRoot.AddComponent<WorldGenerationCoordinator>();
            SetObject(coordinator, "terrainRelief", relief);
            SetObject(coordinator, "trailNetwork", trails);
            SetObject(coordinator, "forestScatterer", scatterer);
            SetBool(coordinator, "generateOnStart", true);

            GameProgress progress = new GameObject("GameProgress").AddComponent<GameProgress>();
            GameSession session = new GameObject("GameSession").AddComponent<GameSession>();

            BuildPlayer(startPosition + Vector3.up * .05f, out PlayerMotor player, out Camera playerCamera, out FlashlightController flashlight);
            SetObject(session, "progress", progress);
            SetObject(session, "player", player);

            BuildFlashlightPickup(startPosition, terrain);
            BuildDocumentSpawner(terrain, spatial, startObject.transform, documentPrefab);
            BuildWorldLighting();
            EnsureEventSystem();

            // Keep the player camera relatively short-ranged; dense fog/forest should hide the boundary.
            playerCamera.farClipPlane = 105f;
            playerCamera.nearClipPlane = .045f;
            flashlight.gameObject.SetActive(true);

            EditorSceneManager.SaveScene(scene, ForestPath);
        }

        private static Terrain CreateTerrainAsset()
        {
            TerrainData existing = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAssetPath);
            if (existing != null) AssetDatabase.DeleteAsset(TerrainAssetPath);

            TerrainData data = new()
            {
                heightmapResolution = 513,
                size = new Vector3(720f, 40f, 720f)
            };
            AssetDatabase.CreateAsset(data, TerrainAssetPath);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "ForestTerrain_720m";
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 7f;
            terrain.basemapDistance = 110f;
            terrain.detailObjectDistance = 80f;
            return terrain;
        }

        private static void BuildPlayer(Vector3 position, out PlayerMotor motor, out Camera camera, out FlashlightController flashlight)
        {
            GameObject player = new("Player");
            player.transform.position = position;
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.76f;
            controller.radius = .33f;
            controller.center = new Vector3(0f, .88f, 0f);
            controller.stepOffset = .34f;
            controller.slopeLimit = 46f;

            motor = player.AddComponent<PlayerMotor>();

            Transform yawRoot = NewChild(player.transform, "YawRoot", Vector3.zero);
            Transform pitchRoot = NewChild(yawRoot, "PitchRoot", new Vector3(0f, 1.62f, 0f));
            camera = new GameObject("PlayerCamera").AddComponent<Camera>();
            camera.transform.SetParent(pitchRoot, false);
            camera.gameObject.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            CameraMotion cameraMotion = pitchRoot.gameObject.AddComponent<CameraMotion>();
            Canvas canvas = CreateCanvas("GameplayCanvas", 20);
            FloatingJoystickInput joystick = CreateJoystick(canvas);
            TouchLookInput look = CreateLookZone(canvas);
            CreateHud(canvas);

            SetObject(motor, "joystick", joystick);
            SetObject(motor, "movementReference", yawRoot);
            SetObject(cameraMotion, "lookInput", look);
            SetObject(cameraMotion, "player", motor);
            SetObject(cameraMotion, "yawRoot", yawRoot);
            SetObject(cameraMotion, "pitchRoot", pitchRoot);
            SetObject(cameraMotion, "targetCamera", camera);

            GameObject lightObject = new("Flashlight");
            lightObject.transform.SetParent(camera.transform, false);
            lightObject.transform.localPosition = new Vector3(.08f, -.07f, .12f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 52f;
            light.spotAngle = 53f;
            light.innerSpotAngle = 27f;
            light.intensity = 7.2f;
            light.color = new Color(.91f, .94f, .98f);
            light.shadows = LightShadows.Soft;

            flashlight = lightObject.AddComponent<FlashlightController>();
            SetObject(flashlight, "flashlight", light);
            SetObject(flashlight, "rayOrigin", camera.transform);
            SetBool(flashlight, "acquiredAtStart", false);
        }

        private static void BuildFlashlightPickup(Vector3 start, Terrain terrain)
        {
            Vector3 p = start + new Vector3(2.8f, 0f, 1.5f);
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y + .12f;

            GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pickup.name = "FlashlightPickup";
            pickup.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, 22f, 90f));
            pickup.transform.localScale = new Vector3(.11f, .36f, .11f);
            Collider collider = pickup.GetComponent<Collider>();
            collider.isTrigger = true;

            FlashlightPickup component = pickup.AddComponent<FlashlightPickup>();
            GameObject outline = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            outline.name = "SubtlePickupOutline";
            outline.transform.SetParent(pickup.transform, false);
            outline.transform.localScale = new Vector3(1.35f, 1.08f, 1.35f);
            UnityEngine.Object.DestroyImmediate(outline.GetComponent<Collider>());
            outline.GetComponent<Renderer>().sharedMaterial = NewRuntimeMaterial("FlashlightOutline", new Color(.34f, .37f, .4f, .28f), 0f);
            SetObject(component, "outlineVisual", outline);
        }

        private static void BuildDocumentSpawner(Terrain terrain, ForestSpatialIndex spatial, Transform startPoint, GameObject documentPrefab)
        {
            GameObject root = new("DocumentSystem");
            DocumentSpawner spawner = root.AddComponent<DocumentSpawner>();
            SetObject(spawner, "documentPrefab", documentPrefab.GetComponent<DocumentPickup>());
            SetObject(spawner, "terrain", terrain);
            SetObject(spawner, "forestIndex", spatial);
            SetObject(spawner, "startPoint", startPoint);
            SetInt(spawner, "count", 10);
            SetInt(spawner, "runtimeCandidateCount", 160);
            SetInt(spawner, "maximumSamplingAttempts", 2400);
        }

        private static void BuildWorldLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.0022f, .0027f, .0035f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.002f, .003f, .004f);
            RenderSettings.fogDensity = .031f;

            Light moon = new GameObject("MinimalForestMoon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = .0035f;
            moon.color = new Color(.48f, .57f, .72f);
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(61f, -29f, 0f);
        }

        private static GameObject EnsureTreePrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
            if (existing != null) return existing;

            GameObject root = new("RuntimeSpruce");
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 3.5f, 0f);
            trunk.transform.localScale = new Vector3(.31f, 3.5f, .31f);
            trunk.GetComponent<Renderer>().sharedMaterial = NewRuntimeMaterial("BootstrapBark", new Color(.08f, .045f, .025f, 1f), .04f);

            Material needles = NewRuntimeMaterial("BootstrapNeedles", new Color(.018f, .055f, .028f, 1f), .02f);
            for (int i = 0; i < 4; i++)
            {
                GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                crown.name = $"NeedleLayer_{i + 1}";
                crown.transform.SetParent(root.transform, false);
                crown.transform.localPosition = new Vector3(0f, 3.8f + i * 1.35f, 0f);
                float radius = 2.25f - i * .43f;
                crown.transform.localScale = new Vector3(radius, .07f, radius);
                crown.GetComponent<Renderer>().sharedMaterial = needles;
                UnityEngine.Object.DestroyImmediate(crown.GetComponent<Collider>());
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TreePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject EnsureGrassPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(GrassPrefabPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("FallenForest/ForestWindURP");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            Material grassMaterial = new(shader);
            grassMaterial.name = "BootstrapGrassMaterial";
            if (grassMaterial.HasProperty("_BaseColor")) grassMaterial.SetColor("_BaseColor", new Color(.045f, .13f, .055f, 1f));

            GameObject root = new("RuntimeGrassCross");
            for (int i = 0; i < 2; i++)
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = i == 0 ? "BladePlane_A" : "BladePlane_B";
                quad.transform.SetParent(root.transform, false);
                quad.transform.localPosition = new Vector3(0f, .42f, 0f);
                quad.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                quad.transform.localScale = new Vector3(.65f, .84f, 1f);
                quad.GetComponent<Renderer>().sharedMaterial = grassMaterial;
                UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, GrassPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject EnsureDocumentPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DocumentPrefabPath);
            if (existing != null) return existing;

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "DocumentFolder";
            root.transform.localScale = new Vector3(.34f, .025f, .25f);
            BoxCollider collider = root.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            root.AddComponent<DocumentPickup>();
            root.GetComponent<Renderer>().sharedMaterial = NewRuntimeMaterial("DocumentCardboard", new Color(.33f, .23f, .13f, 1f), .06f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DocumentPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Canvas CreateCanvas(string name, int sortingOrder)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            return canvas;
        }

        private static FloatingJoystickInput CreateJoystick(Canvas canvas)
        {
            GameObject zone = new("LeftMovementZone", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            zone.transform.SetParent(canvas.transform, false);
            RectTransform rect = zone.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(.5f, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            zone.GetComponent<Image>().color = new Color(0f, 0f, 0f, .001f);

            FloatingJoystickInput joystick = zone.AddComponent<FloatingJoystickInput>();
            GameObject ring = new("FloatingRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            ring.transform.SetParent(zone.transform, false);
            RectTransform ringRect = ring.GetComponent<RectTransform>();
            ringRect.sizeDelta = new Vector2(178f, 178f);
            ring.GetComponent<Image>().color = new Color(.82f, .86f, .9f, .14f);

            GameObject knob = new("Knob", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            knob.transform.SetParent(ring.transform, false);
            RectTransform knobRect = knob.GetComponent<RectTransform>();
            knobRect.anchorMin = knobRect.anchorMax = new Vector2(.5f, .5f);
            knobRect.sizeDelta = new Vector2(72f, 72f);
            knob.GetComponent<Image>().color = new Color(.92f, .94f, .96f, .34f);

            SetObject(joystick, "baseRing", ringRect);
            SetObject(joystick, "knob", knobRect);
            SetObject(joystick, "visualGroup", ring.GetComponent<CanvasGroup>());
            return joystick;
        }

        private static TouchLookInput CreateLookZone(Canvas canvas)
        {
            GameObject zone = new("RightLookZone", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            zone.transform.SetParent(canvas.transform, false);
            RectTransform rect = zone.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.5f, 0f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            zone.GetComponent<Image>().color = new Color(0f, 0f, 0f, .001f);
            return zone.AddComponent<TouchLookInput>();
        }

        private static void CreateHud(Canvas canvas)
        {
            GameObject root = new("HUD", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(canvas.transform, false);
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            HUDController hud = root.AddComponent<HUDController>();
            Text text = CreateText(root.transform, "Message", string.Empty, 31, TextAnchor.UpperCenter);
            SetRect(text.rectTransform, new Vector2(.25f, .77f), new Vector2(.75f, .96f), Vector2.zero, Vector2.zero);
            SetObject(hud, "messageGroup", group);
            SetObject(hud, "messageText", text);
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            FillRect(go.GetComponent<RectTransform>());
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, TextAnchor anchor)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = new Color(.84f, .84f, .82f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(430f, 60f);
            rect.anchoredPosition = anchoredPosition;
            go.GetComponent<Image>().color = new Color(.08f, .08f, .08f, .62f);
            Text text = CreateText(go.transform, "Label", label, 26, TextAnchor.MiddleCenter);
            FillRect(text.rectTransform);
            return go.GetComponent<Button>();
        }

        private static Slider CreateSlider(Transform parent, string name, string label, Vector2 anchoredPosition, out Text valueText)
        {
            GameObject root = new(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(550f, 72f);
            rect.anchoredPosition = anchoredPosition;

            Text labelText = CreateText(root.transform, "Label", label, 22, TextAnchor.UpperLeft);
            SetRect(labelText.rectTransform, new Vector2(0f, .54f), new Vector2(.72f, 1f), Vector2.zero, Vector2.zero);
            valueText = CreateText(root.transform, "Value", string.Empty, 21, TextAnchor.UpperRight);
            SetRect(valueText.rectTransform, new Vector2(.72f, .54f), Vector2.one, Vector2.zero, Vector2.zero);

            GameObject sliderObject = new("Slider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(root.transform, false);
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(.02f, .08f);
            sliderRect.anchorMax = new Vector2(.98f, .5f);
            sliderRect.offsetMin = sliderRect.offsetMax = Vector2.zero;

            GameObject track = new("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            track.transform.SetParent(sliderObject.transform, false);
            FillRect(track.GetComponent<RectTransform>());
            track.GetComponent<Image>().color = new Color(.15f, .15f, .15f, .88f);

            GameObject fillArea = new("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            FillRect(fillArea.GetComponent<RectTransform>());
            GameObject fill = new("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            FillRect(fill.GetComponent<RectTransform>());
            fill.GetComponent<Image>().color = new Color(.56f, .57f, .55f, .9f);

            GameObject handleArea = new("HandleSlideArea", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            FillRect(handleArea.GetComponent<RectTransform>());
            GameObject handle = new("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(28f, 44f);
            handle.GetComponent<Image>().color = new Color(.78f, .79f, .77f, 1f);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static Material NewRuntimeMaterial(string name, Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private static Transform NewChild(Transform parent, string name, Vector3 localPosition)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            GameObject go = new("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private static void SetObject(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            if (target == null) return;
            SerializedObject so = new(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(UnityEngine.Object target, string property, bool value)
        {
            SerializedObject so = new(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) return;
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(UnityEngine.Object target, string property, int value)
        {
            SerializedObject so = new(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) return;
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetGameObjectArray(UnityEngine.Object target, string property, GameObject[] values)
        {
            SerializedObject so = new(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null || !p.isArray) return;
            p.arraySize = values?.Length ?? 0;
            for (int i = 0; i < p.arraySize; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void FillRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
#endif
