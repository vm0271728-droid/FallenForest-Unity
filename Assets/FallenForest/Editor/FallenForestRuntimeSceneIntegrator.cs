#if UNITY_EDITOR
using System.IO;
using FallenForest.Audio;
using FallenForest.Cinematics;
using FallenForest.Monsters;
using FallenForest.Player;
using FallenForest.UI;
using FallenForest.World;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Final deterministic wiring pass for runtime-only systems which a successful C# compile cannot
    /// prove are actually present in the generated scenes.
    /// </summary>
    public static class FallenForestRuntimeSceneIntegrator
    {
        private const string ForestScene = "Assets/FallenForest/Scenes/Forest.unity";
        private const string MainMenuScene = "Assets/FallenForest/Scenes/MainMenu.unity";
        private const string Root = "Assets/FallenForest";

        [MenuItem("Fallen Forest/Release/Finalize Forest Runtime Systems")]
        public static void FinalizeFromMenu() => FinalizeForestRuntimeSystems();

        public static void FinalizeForestRuntimeSystems()
        {
            if (!File.Exists(ForestScene)) return;

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ForestScene, OpenSceneMode.Single);

            PlayerMotor player = Object.FindFirstObjectByType<PlayerMotor>(FindObjectsInactive.Include);
            CameraMotion motion = Object.FindFirstObjectByType<CameraMotion>(FindObjectsInactive.Include);
            FlashlightController flashlight = Object.FindFirstObjectByType<FlashlightController>(FindObjectsInactive.Include);
            Camera playerCamera = FindByName<Camera>("PlayerCamera");

            if (player == null || motion == null || flashlight == null || playerCamera == null)
                throw new InvalidDataException("Forest runtime finalization requires PlayerMotor, CameraMotion, FlashlightController and PlayerCamera.");

            EnsureFlashlightMonsterDetector(flashlight);
            EnsureWorldRuntimeHelpers(player.transform, playerCamera);
            AudioDirector audioDirector = EnsureForestAudio(player.transform);
            WakeUpSequence wakeUp = EnsureWakeUp(player, motion);
            DeathMenuController deathMenu = EnsureDeathAndJumpscare(player, motion, playerCamera);
            WireBoiledSequence(player, motion, wakeUp);
            WireEndingAudio(audioDirector);

            if (deathMenu == null)
                throw new InvalidDataException("Death/jumpscare runtime system could not be created.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (previous.IsValid() && previous.path != scene.path && !string.IsNullOrEmpty(previous.path))
                EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
        }

        public static void FinalizeMainMenuRuntimeSystems()
        {
            if (!File.Exists(MainMenuScene)) return;

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(MainMenuScene, OpenSceneMode.Single);
            GameObject old = FindGameObject("RuntimeMenuAudio");
            if (old != null) Object.DestroyImmediate(old);

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Menu/creepy_forest_menu.ogg");
            if (clip != null)
            {
                GameObject audioObject = new("RuntimeMenuAudio");
                AudioSource source = audioObject.AddComponent<AudioSource>();
                source.clip = clip;
                source.loop = true;
                source.playOnAwake = true;
                source.spatialBlend = 0f;
                source.volume = .55f;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (previous.IsValid() && previous.path != scene.path && !string.IsNullOrEmpty(previous.path))
                EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
        }

        private static void EnsureFlashlightMonsterDetector(FlashlightController flashlight)
        {
            FlashlightMonsterDetector detector = flashlight.GetComponent<FlashlightMonsterDetector>();
            if (detector == null) detector = flashlight.gameObject.AddComponent<FlashlightMonsterDetector>();
            SerializedObject so = new(detector);
            SetObject(so, "flashlight", flashlight);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureWorldRuntimeHelpers(Transform player, Camera camera)
        {
            GameObject root = FindGameObject("RuntimeWorldHelpers");
            if (root == null) root = new GameObject("RuntimeWorldHelpers");

            WindInteractor wind = root.GetComponent<WindInteractor>();
            if (wind == null) wind = root.AddComponent<WindInteractor>();
            SerializedObject windSo = new(wind);
            SetObject(windSo, "player", player);
            windSo.ApplyModifiedPropertiesWithoutUndo();

            if (root.GetComponent<RuntimeQualityController>() == null)
                root.AddComponent<RuntimeQualityController>();

            AtmosphereController atmosphere = root.GetComponent<AtmosphereController>();
            if (atmosphere == null) atmosphere = root.AddComponent<AtmosphereController>();
            SerializedObject atmosphereSo = new(atmosphere);
            SetObject(atmosphereSo, "targetCamera", camera);
            atmosphereSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioDirector EnsureForestAudio(Transform player)
        {
            GameObject old = FindGameObject("RuntimeForestAudio");
            if (old != null) Object.DestroyImmediate(old);

            GameObject root = new("RuntimeForestAudio");
            AudioDirector director = root.AddComponent<AudioDirector>();

            AudioSource forest = CreateAudioSource(root.transform, "ForestLoop", false);
            forest.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Ambience/forest_ambience_cc0.mp3");
            forest.loop = true;
            forest.volume = .62f;

            AudioSource horror = CreateAudioSource(root.transform, "HorrorDrone", false);
            horror.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Ambience/ambient_horror_cc0.ogg");
            horror.loop = true;
            horror.volume = 0f;

            AudioSource oneShot = CreateAudioSource(root.transform, "SpatialOneShots", true);
            oneShot.volume = 1f;

            SerializedObject so = new(director);
            SetObject(so, "forestLoop", forest);
            SetObject(so, "windLoop", null);
            SetObject(so, "horrorDrone", horror);
            SetObject(so, "oneShotSource", oneShot);
            SetObject(so, "player", player);
            so.ApplyModifiedPropertiesWithoutUndo();
            return director;
        }

        private static AudioSource CreateAudioSource(Transform parent, string name, bool spatial)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 4f;
            source.maxDistance = 40f;
            return source;
        }

        private static WakeUpSequence EnsureWakeUp(PlayerMotor player, CameraMotion motion)
        {
            GameObject old = FindGameObject("RuntimeWakeUp");
            if (old != null) Object.DestroyImmediate(old);

            GameObject root = new("RuntimeWakeUp", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 23000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            CanvasGroup blur = CreateFullscreenGroup(root.transform, "WakeBlur", new Color(.02f, .025f, .03f, 1f));
            CanvasGroup eyelids = CreateFullscreenGroup(root.transform, "Eyelids", Color.black);
            blur.alpha = 0f;
            eyelids.alpha = 0f;

            WakeUpSequence wake = root.AddComponent<WakeUpSequence>();
            SerializedObject so = new(wake);
            SetObject(so, "eyelids", eyelids);
            SetObject(so, "blurOverlay", blur);
            SetObject(so, "playerMotor", player);
            SetObject(so, "cameraMotion", motion);
            so.ApplyModifiedPropertiesWithoutUndo();
            return wake;
        }

        private static DeathMenuController EnsureDeathAndJumpscare(PlayerMotor player, CameraMotion motion, Camera camera)
        {
            GameObject old = FindGameObject("RuntimeDeathSystem");
            if (old != null) Object.DestroyImmediate(old);

            GameObject root = new("RuntimeDeathSystem", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 26000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            CanvasGroup blackout = CreateFullscreenGroup(root.transform, "DeathBlackout", Color.black);
            blackout.alpha = 0f;
            blackout.blocksRaycasts = false;

            GameObject panel = new("DeathPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            Fill(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = new Color(.004f, .004f, .005f, .97f);

            Text title = CreateText(panel.transform, "Title", "YOU DIED", 54, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(.2f, .62f);
            title.rectTransform.anchorMax = new Vector2(.8f, .82f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

            Button continueButton = CreateButton(panel.transform, "Continue", new Vector2(0f, -10f), out Text continueText);
            Button menuButton = CreateButton(panel.transform, "MainMenu", new Vector2(0f, -100f), out Text menuText);

            DeathMenuController death = root.AddComponent<DeathMenuController>();
            SerializedObject deathSo = new(death);
            SetObject(deathSo, "panel", panel);
            SetObject(deathSo, "player", player);
            SetObject(deathSo, "cameraMotion", motion);
            SetObject(deathSo, "blackout", blackout);
            SetObject(deathSo, "continueText", continueText);
            SetObject(deathSo, "mainMenuText", menuText);
            deathSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(continueButton.onClick, death.Continue);
            UnityEventTools.AddPersistentListener(menuButton.onClick, death.MainMenu);

            GameObject anchor = new("LocustJumpscareAnchor");
            anchor.transform.SetParent(camera.transform, false);
            anchor.transform.localPosition = new Vector3(0f, -.04f, .52f);
            anchor.transform.localRotation = Quaternion.identity;

            AudioSource source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            JumpscareController jumpscare = root.AddComponent<JumpscareController>();
            SerializedObject jumpSo = new(jumpscare);
            SetObject(jumpSo, "source", source);
            SetObject(jumpSo, "blackout", blackout);
            SetObject(jumpSo, "playerMotor", player);
            SetObject(jumpSo, "cameraMotion", motion);
            SetObject(jumpSo, "jumpscareAnchor", anchor.transform);
            SetObject(jumpSo, "deathMenu", death);
            SerializedProperty screamers = jumpSo.FindProperty("locustScreamers");
            screamers.arraySize = 2;
            screamers.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Screamers/jakes-screamer.mp3");
            screamers.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Screamers/the-screamer-shared-between-mallie-and-jenny.mp3");
            jumpSo.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);
            return death;
        }

        private static void WireBoiledSequence(PlayerMotor player, CameraMotion motion, WakeUpSequence wake)
        {
            BoiledOneSequence sequence = Object.FindFirstObjectByType<BoiledOneSequence>(FindObjectsInactive.Include);
            if (sequence == null) return;
            SerializedObject so = new(sequence);
            SetObject(so, "wakeUp", wake);
            SetObject(so, "playerMotor", player);
            SetObject(so, "cameraMotion", motion);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireEndingAudio(AudioDirector audioDirector)
        {
            EndSequence ending = Object.FindFirstObjectByType<EndSequence>(FindObjectsInactive.Include);
            if (ending == null) return;
            SerializedObject so = new(ending);
            SetObject(so, "audioDirector", audioDirector);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CanvasGroup CreateFullscreenGroup(Transform parent, string name, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            Fill(go.GetComponent<RectTransform>());
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            CanvasGroup group = go.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        private static Button CreateButton(Transform parent, string name, Vector2 position, out Text label)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(440f, 68f);
            rect.anchoredPosition = position;
            go.GetComponent<Image>().color = new Color(.10f, .10f, .105f, .94f);
            label = CreateText(go.transform, "Label", name, 25, TextAnchor.MiddleCenter);
            Fill(label.rectTransform);
            return go.GetComponent<Button>();
        }

        private static Text CreateText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(.88f, .88f, .86f, 1f);
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static GameObject FindGameObject(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }

        private static T FindByName<T>(string name) where T : Component
        {
            foreach (T component in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (component.gameObject.name == name) return component;
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
