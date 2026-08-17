using FallenForest.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FallenForest.World
{
    /// <summary>
    /// Mobile-friendly night adaptation. The forest keeps a tiny natural-night baseline when the
    /// flashlight is off; when the beam is on, post exposure/contrast adapts toward the bright beam
    /// so the surrounding forest perceptually falls toward black without hard-toggling world light.
    /// </summary>
    public sealed class AtmosphereController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private FlashlightController flashlight;
        [SerializeField] private Color fogColor = new(.002f, .003f, .004f, 1f);
        [SerializeField] private float fogDensity = .028f;
        [SerializeField] private Color flashlightOnAmbient = new(.0025f, .0032f, .0042f, 1f);
        [SerializeField] private Color adaptedNightAmbient = new(.0060f, .0070f, .0085f, 1f);
        [SerializeField] private float flashlightOnExposure = -1.05f;
        [SerializeField] private float adaptedNightExposure = .12f;
        [SerializeField] private float adaptToBeamSpeed = 5.5f;
        [SerializeField] private float recoverNightVisionSpeed = .72f;

        private Volume volume;
        private VolumeProfile profile;
        private ColorAdjustments colorAdjustments;
        private Vignette vignette;
        private float adaptation;

        private void Awake()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = flashlightOnAmbient;

            if (targetCamera == null) targetCamera = Camera.main;
            if (flashlight == null) flashlight = FindFirstObjectByType<FlashlightController>();
            if (targetCamera != null)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = Color.black;
                targetCamera.allowHDR = true;
            }

            BuildRuntimeVolume();
            adaptation = flashlight != null && flashlight.IsOn ? 0f : 1f;
            ApplyVisualState();
        }

        private void Update()
        {
            bool beamOn = flashlight != null && flashlight.Acquired && flashlight.IsOn;
            float target = beamOn ? 0f : 1f;
            float speed = beamOn ? adaptToBeamSpeed : recoverNightVisionSpeed;
            adaptation = Mathf.Lerp(adaptation, target, 1f - Mathf.Exp(-Mathf.Max(.01f, speed) * Time.unscaledDeltaTime));
            ApplyVisualState();
        }

        private void OnDestroy()
        {
            if (profile != null) Destroy(profile);
        }

        private void BuildRuntimeVolume()
        {
            volume = GetComponent<Volume>();
            if (volume == null) volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 80f;
            volume.weight = 1f;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "FallenForest_RuntimeNightAdaptation";
            volume.profile = profile;

            colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.Override(flashlightOnExposure);
            colorAdjustments.contrast.Override(28f);
            colorAdjustments.saturation.Override(-8f);

            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(.30f);
            vignette.smoothness.Override(.72f);
            vignette.rounded.Override(false);

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);
        }

        private void ApplyVisualState()
        {
            RenderSettings.ambientLight = Color.Lerp(flashlightOnAmbient, adaptedNightAmbient, adaptation);
            RenderSettings.fogColor = Color.Lerp(fogColor * .72f, fogColor * 1.35f, adaptation);

            if (colorAdjustments != null)
            {
                colorAdjustments.postExposure.value = Mathf.Lerp(flashlightOnExposure, adaptedNightExposure, adaptation);
                colorAdjustments.contrast.value = Mathf.Lerp(32f, 12f, adaptation);
                colorAdjustments.saturation.value = Mathf.Lerp(-10f, -5f, adaptation);
            }

            if (vignette != null)
                vignette.intensity.value = Mathf.Lerp(.34f, .22f, adaptation);
        }

        public void SetFlashlight(FlashlightController controller)
        {
            flashlight = controller;
        }
    }
}
