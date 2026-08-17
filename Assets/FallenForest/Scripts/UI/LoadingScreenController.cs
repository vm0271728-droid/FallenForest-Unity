using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FallenForest.UI
{
    /// <summary>
    /// Prefab-free loading overlay used by SceneFlow. It survives scene changes,
    /// reports real AsyncOperation progress and keeps the progress line flush with
    /// the absolute bottom edge of the display.
    ///
    /// If Resources/UI/loading_forest exists it is used as the background artwork;
    /// otherwise a dark fallback is shown so loading remains functional before the
    /// final forest artwork is committed.
    /// </summary>
    public sealed class LoadingScreenController : MonoBehaviour
    {
        private static LoadingScreenController instance;

        private CanvasGroup canvasGroup;
        private RectTransform progressFill;
        private Text loadingText;
        private bool loading;

        public static void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return;
            LoadingScreenController loader = Ensure();
            if (loader.loading) return;
            loader.gameObject.SetActive(true);
            loader.StartCoroutine(loader.LoadRoutine(sceneName));
        }

        private static LoadingScreenController Ensure()
        {
            if (instance != null) return instance;
            GameObject go = new GameObject("FallenForest_LoadingScreen");
            instance = go.AddComponent<LoadingScreenController>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUi();
            gameObject.SetActive(false);
        }

        private void BuildUi()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();

            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = false;

            GameObject background = UiObject("Background", transform);
            Image backgroundImage = background.AddComponent<Image>();
            backgroundImage.raycastTarget = true;
            RectTransform bgRect = background.GetComponent<RectTransform>();
            Stretch(bgRect);

            Sprite forest = Resources.Load<Sprite>("UI/loading_forest");
            if (forest != null)
            {
                backgroundImage.sprite = forest;
                backgroundImage.color = Color.white;
                backgroundImage.preserveAspect = false;
            }
            else
            {
                backgroundImage.color = new Color(.018f, .026f, .028f, 1f);
            }

            GameObject shade = UiObject("AtmosphereShade", transform);
            Image shadeImage = shade.AddComponent<Image>();
            shadeImage.color = new Color(0f, 0f, 0f, .48f);
            shadeImage.raycastTarget = false;
            Stretch(shade.GetComponent<RectTransform>());

            Text title = CreateText("Title", transform, "FALLEN FOREST", 54, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(.91f, .93f, .93f, .96f);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(.5f, .5f);
            titleRect.anchorMax = new Vector2(.5f, .5f);
            titleRect.pivot = new Vector2(.5f, .5f);
            titleRect.sizeDelta = new Vector2(900f, 100f);
            titleRect.anchoredPosition = new Vector2(0f, 56f);

            loadingText = CreateText("LoadingText", transform, "Загрузка...", 23, TextAnchor.MiddleCenter);
            loadingText.color = new Color(.82f, .84f, .84f, .92f);
            RectTransform loadingRect = loadingText.rectTransform;
            loadingRect.anchorMin = new Vector2(.5f, 0f);
            loadingRect.anchorMax = new Vector2(.5f, 0f);
            loadingRect.pivot = new Vector2(.5f, 0f);
            loadingRect.sizeDelta = new Vector2(620f, 50f);
            loadingRect.anchoredPosition = new Vector2(0f, 20f);

            GameObject progressRoot = UiObject("ProgressRoot", transform);
            RectTransform rootRect = progressRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(.5f, 0f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = new Vector2(0f, 6f);

            GameObject fill = UiObject("ProgressFill", progressRoot.transform);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(.82f, .86f, .87f, .96f);
            fillImage.raycastTarget = false;
            progressFill = fill.GetComponent<RectTransform>();
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.pivot = new Vector2(0f, .5f);
            progressFill.offsetMin = Vector2.zero;
            progressFill.offsetMax = Vector2.zero;
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            loading = true;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            SetProgress(0f);
            loadingText.text = "Загрузка...";

            float visibleFor = 0f;
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                loading = false;
                gameObject.SetActive(false);
                yield break;
            }

            operation.allowSceneActivation = false;
            float shownProgress = 0f;

            while (operation.progress < .9f)
            {
                visibleFor += Time.unscaledDeltaTime;
                float target = Mathf.Clamp01(operation.progress / .9f);
                shownProgress = Mathf.MoveTowards(shownProgress, target, Time.unscaledDeltaTime * .7f);
                SetProgress(shownProgress);
                yield return null;
            }

            while (shownProgress < 1f || visibleFor < .55f)
            {
                visibleFor += Time.unscaledDeltaTime;
                shownProgress = Mathf.MoveTowards(shownProgress, 1f, Time.unscaledDeltaTime * 1.8f);
                SetProgress(shownProgress);
                yield return null;
            }

            SetProgress(1f);
            yield return new WaitForSecondsRealtime(.08f);
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;

            float fade = 1f;
            while (fade > 0f)
            {
                fade -= Time.unscaledDeltaTime / .28f;
                canvasGroup.alpha = Mathf.Clamp01(fade);
                yield return null;
            }

            canvasGroup.blocksRaycasts = false;
            loading = false;
            gameObject.SetActive(false);
        }

        private void SetProgress(float value)
        {
            if (progressFill == null) return;
            Vector2 max = progressFill.anchorMax;
            max.x = Mathf.Clamp01(value);
            progressFill.anchorMax = max;
        }

        private static GameObject UiObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment)
        {
            GameObject go = UiObject(name, parent);
            Text text = go.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
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
