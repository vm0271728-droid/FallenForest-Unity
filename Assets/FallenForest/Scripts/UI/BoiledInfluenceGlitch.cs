using FallenForest.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FallenForest.UI
{
    /// <summary>
    /// Mobile-friendly persistent corruption after the Boiled encounter. It survives Continue via
    /// SaveSystem and clears only with a new run. No full-screen post-process shader is required.
    /// </summary>
    public sealed class BoiledInfluenceGlitch : MonoBehaviour
    {
        private const string GameplayScene = "Forest";
        private const int StripCount = 10;

        [SerializeField] private Vector2 burstInterval = new(4.2f, 11.5f);
        [SerializeField] private Vector2 burstDuration = new(.055f, .18f);
        [SerializeField] private Vector2 flickerInterval = new(.018f, .05f);

        private CanvasGroup canvasGroup;
        private Image[] strips;
        private float nextBurstAt;
        private float burstEndsAt;
        private float nextFlickerAt;
        private bool bursting;
        private bool activeLastFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (FindFirstObjectByType<BoiledInfluenceGlitch>() != null) return;
            GameObject go = new("Boiled Influence Glitch");
            DontDestroyOnLoad(go);
            go.AddComponent<BoiledInfluenceGlitch>();
        }

        private void Awake()
        {
            BuildCanvas();
            HideAll();
        }

        private void Update()
        {
            bool shouldBeActive = SceneManager.GetActiveScene().name == GameplayScene && SaveSystem.BoiledInfluenced;

            if (!shouldBeActive)
            {
                if (activeLastFrame) HideAll();
                activeLastFrame = false;
                bursting = false;
                return;
            }

            if (!activeLastFrame)
            {
                activeLastFrame = true;
                nextBurstAt = Time.unscaledTime + Random.Range(1.7f, 4.2f);
                HideAll();
            }

            float now = Time.unscaledTime;
            if (!bursting)
            {
                if (now >= nextBurstAt) BeginBurst(now);
                return;
            }

            if (now >= burstEndsAt)
            {
                EndBurst(now);
                return;
            }

            if (now >= nextFlickerAt)
            {
                ArrangeStrips();
                nextFlickerAt = now + Random.Range(flickerInterval.x, flickerInterval.y);
            }
        }

        private void BuildCanvas()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20000;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            gameObject.AddComponent<GraphicRaycaster>().enabled = false;
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            strips = new Image[StripCount];
            for (int i = 0; i < strips.Length; i++)
            {
                GameObject stripObject = new($"Glitch Strip {i + 1}", typeof(RectTransform), typeof(Image));
                stripObject.transform.SetParent(transform, false);
                RectTransform rect = (RectTransform)stripObject.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                Image image = stripObject.GetComponent<Image>();
                image.raycastTarget = false;
                image.enabled = false;
                strips[i] = image;
            }
        }

        private void BeginBurst(float now)
        {
            bursting = true;
            burstEndsAt = now + Random.Range(burstDuration.x, burstDuration.y);
            nextFlickerAt = now;
            canvasGroup.alpha = Random.Range(.72f, 1f);
            ArrangeStrips();
        }

        private void EndBurst(float now)
        {
            bursting = false;
            HideAll();
            float spacing = Random.Range(burstInterval.x, burstInterval.y);
            if (Random.value < .14f) spacing = Random.Range(.22f, .7f);
            nextBurstAt = now + spacing;
        }

        private void ArrangeStrips()
        {
            HideAll();
            int visible = Random.Range(2, 6);
            const int referenceWidth = 1920;
            const int referenceHeight = 1080;

            for (int i = 0; i < visible && i < strips.Length; i++)
            {
                Image image = strips[i];
                RectTransform rect = image.rectTransform;
                bool tinyBlock = Random.value < .28f;
                float width = tinyBlock ? Random.Range(42f, 180f) : Random.Range(referenceWidth * .13f, referenceWidth * .54f);
                float height = tinyBlock ? Random.Range(4f, 18f) : Random.Range(2f, 11f);
                rect.sizeDelta = new Vector2(width, height);
                rect.anchoredPosition = new Vector2(
                    Random.Range(-referenceWidth * .43f, referenceWidth * .43f),
                    Random.Range(-referenceHeight * .43f, referenceHeight * .43f));

                float alpha = Random.Range(.055f, .19f);
                float colorRoll = Random.value;
                if (colorRoll < .18f) image.color = new Color(.74f, .96f, 1f, alpha);
                else if (colorRoll < .31f) image.color = new Color(1f, .54f, .54f, alpha * .82f);
                else
                {
                    float gray = Random.Range(.63f, .96f);
                    image.color = new Color(gray, gray, gray, alpha);
                }
                image.enabled = true;
            }

            if (visible < strips.Length && Random.value < .42f)
            {
                Image source = strips[0];
                Image pair = strips[visible];
                pair.rectTransform.sizeDelta = source.rectTransform.sizeDelta;
                pair.rectTransform.anchoredPosition = source.rectTransform.anchoredPosition + new Vector2(Random.Range(3f, 9f), Random.Range(-2f, 2f));
                pair.color = new Color(.55f, .92f, 1f, source.color.a * .55f);
                pair.enabled = true;
            }
        }

        private void HideAll()
        {
            if (strips == null) return;
            for (int i = 0; i < strips.Length; i++)
                if (strips[i] != null) strips[i].enabled = false;
        }
    }
}
