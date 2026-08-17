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
    /// Finalizes the generated menu against current rules: English first-run default, persistent
    /// EN/RU selector, no FOV setting, persistent UnityEvents, compact authorship and full Credits.
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

            // World FOV is fixed at 75 degrees by design.
            Transform oldFov = settingsPanel.transform.Find("FOV");
            if (oldFov != null) Object.DestroyImmediate(oldFov.gameObject);

            Reposition(mainPanel.transform.Find("Play") as RectTransform, new Vector2(0f, 74f));
            Reposition(mainPanel.transform.Find("Settings") as RectTransform, new Vector2(0f, -4f));
            Reposition(mainPanel.transform.Find("Quit") as RectTransform, new Vector2(0f, -160f));
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
            Text compactCredits = EnsureCompactCredits(mainPanel.transform);
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
            Set(locSo, "creditsText", compactCredits);
            locSo.ApplyModifiedPropertiesWithoutUndo();
            AddPersistent(language, localization.ToggleLanguage);

            BuildFullCredits(mainPanel, out Button creditsButton, out Text creditsButtonText);
            CreditsPanelController creditsController = mainPanel.transform.parent.GetComponent<CreditsPanelController>();
            if (creditsController != null)
            {
                AddPersistent(creditsButton, creditsController.Open);
                Button creditsBack = FindButton(mainPanel.transform.parent.Find("CreditsPanel"), "Back");
                AddPersistent(creditsBack, creditsController.Close);
                creditsController.ApplyLocalization();
            }

            // Source scene stays English. Saved preference may switch it on runtime OnEnable.
            if (playText != null) playText.text = "PLAY";
            if (settingsText != null) settingsText.text = "SETTINGS";
            if (quitText != null) quitText.text = "QUIT";
            if (settingsTitle != null) settingsTitle.text = "SETTINGS";
            if (sensitivityText != null) sensitivityText.text = "SENSITIVITY";
            if (shakeText != null) shakeText.text = "CAMERA SHAKE";
            if (backText != null) backText.text = "BACK";
            if (languageText != null) languageText.text = "LANGUAGE: ENGLISH";
            if (creditsButtonText != null) creditsButtonText.text = "CREDITS";
            if (compactCredits != null) compactCredits.text = "Idea by: Meric23\nDeveloped by: Meric23";

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (previous.IsValid() && previous.path != scene.path && !string.IsNullOrEmpty(previous.path))
                EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
        }

        private static void BuildFullCredits(GameObject mainPanel, out Button openButton, out Text openButtonText)
        {
            Transform canvas = mainPanel.transform.parent;
            Transform oldPanel = canvas.Find("CreditsPanel");
            if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);
            Transform oldButton = mainPanel.transform.Find("Credits");
            if (oldButton != null) Object.DestroyImmediate(oldButton.gameObject);

            openButton = CreateSimpleButton(mainPanel.transform, "Credits", new Vector2(0f, -82f), new Vector2(430f, 62f));
            openButtonText = Label(openButton);

            GameObject panel = new("CreditsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvas, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(.006f, .007f, .008f, .965f);

            Text title = CreateText(panel.transform, "Title", "CREDITS", 46, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(.22f, .77f), new Vector2(.78f, .9f));

            Text body = CreateText(panel.transform, "Body", string.Empty, 24, TextAnchor.UpperCenter);
            body.lineSpacing = 1.15f;
            body.color = new Color(.80f, .81f, .80f, 1f);
            SetRect(body.rectTransform, new Vector2(.17f, .25f), new Vector2(.83f, .76f));

            Button back = CreateSimpleButton(panel.transform, "Back", new Vector2(0f, -238f), new Vector2(430f, 62f));
            Text backText = Label(back);

            CreditsPanelController controller = canvas.GetComponent<CreditsPanelController>();
            if (controller == null) controller = canvas.gameObject.AddComponent<CreditsPanelController>();
            SerializedObject so = new(controller);
            Set(so, "mainPanel", mainPanel);
            Set(so, "creditsPanel", panel);
            Set(so, "titleText", title);
            Set(so, "bodyText", body);
            Set(so, "backText", backText);
            Set(so, "openButtonText", openButtonText);
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.SetActive(false);
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
                go = CreateSimpleButton(parent, "Language", new Vector2(0f, -110f), new Vector2(430f, 60f)).gameObject;
            }
            Reposition(go.GetComponent<RectTransform>(), new Vector2(0f, -110f));
            return go.GetComponent<Button>();
        }

        private static Text EnsureCompactCredits(Transform parent)
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

        private static Button CreateSimpleButton(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = go.GetComponent<Image>();
            image.color = new Color(.08f, .08f, .08f, .62f);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;

            Text label = CreateText(go.transform, "Label", name.ToUpperInvariant(), 24, TextAnchor.MiddleCenter);
            label.color = new Color(.84f, .84f, .82f, 1f);
            label.raycastTarget = false;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            return button;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, TextAnchor alignment)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color(.88f, .88f, .86f, 1f);
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
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

        private static Button FindButton(Transform parent, string child) => parent != null ? parent.Find(child)?.GetComponent<Button>() : null;
        private static Slider FindSlider(Transform parent, string child) => parent != null ? parent.Find(child)?.GetComponentInChildren<Slider>(true) : null;
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
