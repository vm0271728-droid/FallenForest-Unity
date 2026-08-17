#if UNITY_EDITOR
using System.IO;
using FallenForest.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Converts the legacy generated menu shell into the canonical current menu:
    /// English default, EN/RU runtime selector, no FOV control, persistent UI events and credits.
    /// </summary>
    public static class FallenForestMenuFinalizer
    {
        private const string ScenePath = "Assets/FallenForest/Scenes/MainMenu.unity";

        [MenuItem("Fallen Forest/Release/Finalize Main Menu")]
        public static void FinalizeFromMenu() => FinalizeMainMenu();

        public static void FinalizeMainMenu()
        {
            if (!File.Exists(ScenePath)) return;

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject mainPanel = Find("MainPanel");
            GameObject settingsPanel = Find("SettingsPanel");
            if (mainPanel == null || settingsPanel == null)
            {
                Debug.LogWarning("Fallen Forest: generated menu panels were not found for finalization.");
                return;
            }

            MainMenuController menu = mainPanel.GetComponent<MainMenuController>();
            SettingsMenuController settings = settingsPanel.GetComponent<SettingsMenuController>();

            Button play = FindButton(mainPanel.transform, "Play");
            Button settingsButton = FindButton(mainPanel.transform, "Settings");
            Button quit = FindButton(mainPanel.transform, "Quit");
            Button back = FindButton(settingsPanel.transform, "Back");
            Slider sensitivity = FindSlider(settingsPanel.transform, "Sensitivity");
            Slider shake = FindSlider(settingsPanel.transform, "Shake");

            // Current design has fixed 75-degree world FOV. Remove the legacy user-facing FOV row.
            Transform oldFov = settingsPanel.transform.Find("FOV");
            if (oldFov != null) Object.DestroyImmediate(oldFov.gameObject);

            Reposition(settingsPanel.transform.Find("Sensitivity") as RectTransform, new Vector2(0f, 85f));
            Reposition(settingsPanel.transform.Find("Shake") as RectTransform, new Vector2(0f, -15f));
            Reposition(settingsPanel.transform.Find("Back") as RectTransform, new Vector2(0f, -205f));

            if (menu != null)
            {
                AddPersistent(play, menu.Play);
                AddPersistent(settingsButton, menu.OpenSettings);
                AddPersistent(quit, menu.Quit);
                AddPersistent(back, menu.CloseSettings);
            }
            if (settings != null)
            {
                AddPersistent(sensitivity, settings.SetSensitivity);
                AddPersistent(shake, settings.SetShake);

                SerializedObject settingsSo = new(settings);
                SerializedProperty fovSlider = settingsSo.FindProperty("fovSlider");
                SerializedProperty fovValue = settingsSo.FindProperty("fovValue");
                if (fovSlider != null) fovSlider.objectReferenceValue = null;
                if (fovValue != null) fovValue.objectReferenceValue = null;
                settingsSo.ApplyModifiedPropertiesWithoutUndo();
            }

            Button language = EnsureLanguageButton(settingsPanel.transform);
            Text credits = EnsureCredits(mainPanel.transform);
            MenuLocalizationController localization = mainPanel.GetComponent<MenuLocalizationController>();
            if (localization == null) localization = mainPanel.AddComponent<MenuLocalizationController>();

            Text playText = Label(play);
            Text settingsText = Label(settingsButton);
            Text quitText = Label(quit);
            Text settingsTitle = ChildText(settingsPanel.transform, "Title");
            Text sensitivityText = ChildText(settingsPanel.transform.Find("Sensitivity"), "Label");
            Text shakeText = ChildText(settingsPanel.transform.Find("Shake"), "Label");
            Text backText = Label(back);
            Text languageText = Label(language);

            SerializedObject locSo = new(localization);
            Set(locSo, "playText", playText);
            Set(locSo, "settingsText", settingsText);
            Set(locSo, "quitText", quitText);
            Set(locSo, "settingsTitle", settingsTitle);
            Set(locSo, "sensitivityText", sensitivityText);
            Set(locSo, "shakeText", shakeText);
            Set(locSo, "backText", backText);
            Set(locSo, "languageText", languageText);
            Set(locSo, "creditsText", credits);
            locSo.ApplyModifiedPropertiesWithoutUndo();

            AddPersistent(language, localization.ToggleLanguage);

            // Source scene is English. Saved runtime preference may switch it to Russian on OnEnable.
            if (playText != null) playText.text = "PLAY";
            if (settingsText != null) settingsText.text = "SETTINGS";
            if (quitText != null) quitText.text = "QUIT";
            if (settingsTitle != null) settingsTitle.text = "SETTINGS";
            if (sensitivityText != null) sensitivityText.text = "SENSITIVITY";
            if (shakeText != null) shakeText.text = "CAMERA SHAKE";
            if (backText != null) backText.text = "BACK";
            if (languageText != null) languageText.text = "LANGUAGE: ENGLISH";
            if (credits != null) credits.text = "Idea by: Meric23\nDeveloped by: Meric23";

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (previous.IsValid() && previous.path != scene.path && !string.IsNullOrEmpty(previous.path))
                EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
        }

        private static Button EnsureLanguageButton(Transform parent)
        {
            Transform existing = parent.Find("Language");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("Language", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(parent, false);
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(430f, 60f);
                rect.anchoredPosition = new Vector2(0f, -110f);
                go.GetComponent<Image>().color = new Color(.08f, .08f, .08f, .62f);

                GameObject labelGo = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelGo.transform.SetParent(go.transform, false);
                RectTransform labelRect = labelGo.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                Text label = labelGo.GetComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 24;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = new Color(.84f, .84f, .82f, 1f);
                label.raycastTarget = false;
            }
            Reposition(go.GetComponent<RectTransform>(), new Vector2(0f, -110f));
            return go.GetComponent<Button>();
        }

        private static Text EnsureCredits(Transform parent)
        {
            Transform existing = parent.Find("CompactCredits");
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject("CompactCredits", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            if (existing == null) go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.66f, .015f);
            rect.anchorMax = new Vector2(.985f, .12f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 17;
            text.alignment = TextAnchor.LowerRight;
            text.color = new Color(.68f, .69f, .68f, .72f);
            text.raycastTarget = false;
            return text;
        }

        private static GameObject Find(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in all)
                    if (t.name == name) return t.gameObject;
            }
            return null;
        }

        private static Button FindButton(Transform parent, string child) => parent.Find(child)?.GetComponent<Button>();
        private static Slider FindSlider(Transform parent, string child) => parent.Find(child)?.GetComponentInChildren<Slider>(true);
        private static Text Label(Button button) => button != null ? ChildText(button.transform, "Label") : null;
        private static Text ChildText(Transform parent, string child) => parent != null ? parent.Find(child)?.GetComponent<Text>() : null;

        private static void Reposition(RectTransform rect, Vector2 position)
        {
            if (rect != null) rect.anchoredPosition = position;
        }

        private static void Set(SerializedObject so, string property, Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void AddPersistent(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null) return;
            if (button.onClick.GetPersistentEventCount() == 0)
                UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        private static void AddPersistent(Slider slider, UnityEngine.Events.UnityAction<float> action)
        {
            if (slider == null || action == null) return;
            if (slider.onValueChanged.GetPersistentEventCount() == 0)
                UnityEventTools.AddPersistentListener(slider.onValueChanged, action);
        }
    }
}
#endif
