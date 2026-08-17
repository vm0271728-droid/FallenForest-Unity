#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FallenForest.UI
{
    /// <summary>
    /// Shows a one-per-launch safety warning before the player starts interacting
    /// with the main menu. Built entirely at runtime so it cannot disappear because
    /// a scene prefab was forgotten during automated builds.
    /// </summary>
    public sealed class StartupDisclaimer : MonoBehaviour
    {
        private static bool shownThisLaunch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ShowOncePerLaunch()
        {
            if (shownThisLaunch) return;
            shownThisLaunch = true;

            GameObject root = new GameObject("FallenForest_Disclaimer");
            root.AddComponent<StartupDisclaimer>().Build();
        }

        private void Build()
        {
            DontDestroyOnLoad(gameObject);

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();

            CanvasGroup group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;

            GameObject background = CreateUiObject("Background", transform);
            Image backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = new Color(.008f, .008f, .01f, 1f);
            Stretch(background.GetComponent<RectTransform>());

            Text heading = CreateText("Heading", transform, "ПРЕДУПРЕЖДЕНИЕ", 44, TextAnchor.MiddleCenter);
            heading.fontStyle = FontStyle.Bold;
            heading.color = new Color(.92f, .92f, .92f, 1f);
            RectTransform headingRect = heading.rectTransform;
            headingRect.anchorMin = headingRect.anchorMax = new Vector2(.5f, .5f);
            headingRect.pivot = new Vector2(.5f, .5f);
            headingRect.sizeDelta = new Vector2(1200f, 90f);
            headingRect.anchoredPosition = new Vector2(0f, 180f);

            string bodyText =
                "В игре присутствуют скримеры, резкие звуки, тревожные сцены и мигающие визуальные эффекты.\n\n" +
                "Если вы чувствительны к вспышкам или страдаете фоточувствительной эпилепсией, " +
                "прекратите игру при появлении дискомфорта.";

            Text body = CreateText("Body", transform, bodyText, 28, TextAnchor.MiddleCenter);
            body.color = new Color(.78f, .79f, .8f, 1f);
            body.lineSpacing = 1.12f;
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = bodyRect.anchorMax = new Vector2(.5f, .5f);
            bodyRect.pivot = new Vector2(.5f, .5f);
            bodyRect.sizeDelta = new Vector2(1300f, 300f);
            bodyRect.anchoredPosition = new Vector2(0f, -5f);

            Button button = CreateButton(transform);
            button.onClick.AddListener(Dismiss);

            EnsureEventSystem();
        }

        private void Dismiss()
        {
            StartCoroutine(FadeAndDestroy());
        }

        private System.Collections.IEnumerator FadeAndDestroy()
        {
            CanvasGroup group = GetComponent<CanvasGroup>();
            group.interactable = false;
            float alpha = group.alpha;
            while (alpha > 0f)
            {
                alpha -= Time.unscaledDeltaTime / .22f;
                group.alpha = Mathf.Clamp01(alpha);
                yield return null;
            }
            Destroy(gameObject);
        }

        private static Button CreateButton(Transform parent)
        {
            GameObject go = CreateUiObject("ContinueButton", parent);
            Image image = go.AddComponent<Image>();
            image.color = new Color(.12f, .125f, .13f, .96f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.9f, .9f, .9f, 1f);
            colors.pressedColor = new Color(.72f, .72f, .72f, 1f);
            button.colors = colors;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(430f, 82f);
            rect.anchoredPosition = new Vector2(0f, -245f);

            Text label = CreateText("Label", go.transform, "ПРОДОЛЖИТЬ", 25, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(.92f, .92f, .92f, 1f);
            Stretch(label.rectTransform);
            return button;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            GameObject events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            events.AddComponent<InputSystemUIInputModule>();
#else
            events.AddComponent<StandaloneInputModule>();
#endif
            DontDestroyOnLoad(events);
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor anchor)
        {
            GameObject go = CreateUiObject(name, parent);
            Text text = go.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
