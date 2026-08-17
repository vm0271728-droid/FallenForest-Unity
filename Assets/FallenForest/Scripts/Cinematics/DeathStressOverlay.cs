using UnityEngine;
using UnityEngine.UI;

namespace FallenForest.Cinematics
{
    /// <summary>
    /// Lightweight death-only red vignette + tinnitus layer. Built at runtime to avoid another
    /// permanent full-screen post-process and to keep the effect deterministic on mobile URP.
    /// </summary>
    public sealed class DeathStressOverlay : MonoBehaviour
    {
        private CanvasGroup group;
        private Image vignette;
        private AudioSource tinnitus;
        private Texture2D texture;
        private AudioClip tinnitusClip;
        private float targetIntensity;
        private float currentIntensity;

        private void Awake()
        {
            BuildVisual();
            BuildAudio();
            SetIntensity(0f);
        }

        private void Update()
        {
            currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, Time.unscaledDeltaTime * 1.8f);
            float pulse = .91f + Mathf.Sin(Time.unscaledTime * 7.1f) * .09f;
            if (group != null) group.alpha = Mathf.Clamp01(currentIntensity * pulse);
            if (tinnitus != null)
            {
                tinnitus.volume = Mathf.Clamp01(currentIntensity * .16f);
                tinnitus.pitch = Mathf.Lerp(.96f, 1.035f, currentIntensity);
            }
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
            if (tinnitusClip != null) Destroy(tinnitusClip);
        }

        public void SetIntensity(float intensity)
        {
            targetIntensity = Mathf.Clamp01(intensity);
            if (group != null && targetIntensity <= 0f && currentIntensity <= .001f) group.alpha = 0f;
        }

        public void StopImmediately()
        {
            targetIntensity = currentIntensity = 0f;
            if (group != null) group.alpha = 0f;
            if (tinnitus != null) tinnitus.volume = 0f;
        }

        private void BuildVisual()
        {
            GameObject canvasObject = new("DeathStressCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            group = canvasObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            GameObject imageObject = new("RedVignette", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            vignette = imageObject.GetComponent<Image>();
            vignette.raycastTarget = false;

            texture = BuildVignetteTexture();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 64f);
            vignette.sprite = sprite;
            vignette.color = new Color(1f, .05f, .025f, .82f);
        }

        private static Texture2D BuildVignetteTexture()
        {
            const int size = 64;
            Texture2D result = new(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "DeathRedVignette_Runtime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + .5f) / size * 2f - 1f;
                    float ny = (y + .5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float alpha = Mathf.SmoothStep(.30f, 1.18f, d);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            result.SetPixels(pixels);
            result.Apply(false, true);
            return result;
        }

        private void BuildAudio()
        {
            GameObject audioObject = new("DeathTinnitus", typeof(AudioSource));
            audioObject.transform.SetParent(transform, false);
            tinnitus = audioObject.GetComponent<AudioSource>();
            tinnitus.playOnAwake = false;
            tinnitus.loop = true;
            tinnitus.spatialBlend = 0f;
            tinnitus.ignoreListenerPause = true;
            tinnitus.volume = 0f;
            tinnitusClip = BuildTinnitusClip();
            tinnitus.clip = tinnitusClip;
            tinnitus.Play();
        }

        private static AudioClip BuildTinnitusClip()
        {
            const int sampleRate = 22050;
            const int seconds = 2;
            int count = sampleRate * seconds;
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float high = Mathf.Sin(t * Mathf.PI * 2f * 5940f) * .19f;
                float low = Mathf.Sin(t * Mathf.PI * 2f * 132f) * .025f;
                float wobble = .82f + Mathf.Sin(t * Mathf.PI * 2f * 2.15f) * .12f;
                samples[i] = (high + low) * wobble;
            }
            AudioClip clip = AudioClip.Create("DeathTinnitus_Runtime", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
