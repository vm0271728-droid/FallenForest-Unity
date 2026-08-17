using UnityEngine;

namespace FallenForest.Monsters
{
    /// <summary>
    /// Self-contained procedural stress layer for the Boiled encounter. It synthesizes a low,
    /// breath-like loop and a narrow tinnitus tone at runtime so the effect does not depend on a
    /// missing placeholder audio asset. Intensity is explicitly driven by the encounter timeline.
    /// </summary>
    public sealed class BoiledStressAudio : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float maximumBreathVolume = .32f;
        [SerializeField, Range(0f, 1f)] private float maximumTinnitusVolume = .18f;
        [SerializeField] private float response = 5.5f;

        private AudioSource breathSource;
        private AudioSource tinnitusSource;
        private AudioClip breathClip;
        private AudioClip tinnitusClip;
        private float targetIntensity;

        private void Awake()
        {
            breathSource = CreateSource("Boiled_Breath");
            tinnitusSource = CreateSource("Boiled_Tinnitus");
            breathClip = CreateBreathClip();
            tinnitusClip = CreateTinnitusClip();
            breathSource.clip = breathClip;
            tinnitusSource.clip = tinnitusClip;
            breathSource.loop = true;
            tinnitusSource.loop = true;
            breathSource.Play();
            tinnitusSource.Play();
        }

        private void Update()
        {
            float k = 1f - Mathf.Exp(-response * Time.unscaledDeltaTime);
            breathSource.volume = Mathf.Lerp(breathSource.volume, targetIntensity * maximumBreathVolume, k);
            float tinnitusCurve = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.18f, 1f, targetIntensity));
            tinnitusSource.volume = Mathf.Lerp(tinnitusSource.volume, tinnitusCurve * maximumTinnitusVolume, k);
            tinnitusSource.pitch = Mathf.Lerp(.96f, 1.045f, targetIntensity);
        }

        private void OnDestroy()
        {
            if (breathClip != null) Destroy(breathClip);
            if (tinnitusClip != null) Destroy(tinnitusClip);
        }

        public void SetIntensity(float intensity)
        {
            targetIntensity = Mathf.Clamp01(intensity);
        }

        public void StopStress()
        {
            targetIntensity = 0f;
        }

        private AudioSource CreateSource(string sourceName)
        {
            GameObject go = new(sourceName);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.dopplerLevel = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        private static AudioClip CreateBreathClip()
        {
            const int frequency = 22050;
            const int seconds = 4;
            int count = frequency * seconds;
            float[] data = new float[count];
            System.Random random = new(731923);
            float filtered = 0f;
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)frequency;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered = Mathf.Lerp(filtered, noise, .025f);
                float breathEnvelope = Mathf.Pow(.5f + .5f * Mathf.Sin(time * Mathf.PI * .62f - 1.2f), 1.65f);
                float chest = Mathf.Sin(time * Mathf.PI * 2f * 47f) * .055f;
                data[i] = Mathf.Clamp((filtered * .32f + chest) * breathEnvelope, -.72f, .72f);
            }
            AudioClip clip = AudioClip.Create("Boiled_ProceduralBreath", count, 1, frequency, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateTinnitusClip()
        {
            const int frequency = 22050;
            const int seconds = 2;
            int count = frequency * seconds;
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)frequency;
                float carrier = Mathf.Sin(time * Mathf.PI * 2f * 6150f);
                float beating = .72f + Mathf.Sin(time * Mathf.PI * 2f * 1.7f) * .08f;
                data[i] = carrier * beating * .22f;
            }
            AudioClip clip = AudioClip.Create("Boiled_ProceduralTinnitus", count, 1, frequency, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
