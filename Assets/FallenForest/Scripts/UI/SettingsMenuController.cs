using FallenForest.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FallenForest.UI
{
    /// <summary>Runtime settings exposed by the canonical design. World FOV stays fixed at 75°.</summary>
    public sealed class SettingsMenuController : MonoBehaviour
    {
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Slider shakeSlider;
        [SerializeField] private Text sensitivityValue;
        [SerializeField] private Text shakeValue;

        private void OnEnable()
        {
            if (sensitivitySlider != null)
            {
                sensitivitySlider.minValue = .3f;
                sensitivitySlider.maxValue = 2.5f;
                sensitivitySlider.value = GameSettings.Sensitivity;
            }

            if (shakeSlider != null)
            {
                shakeSlider.minValue = 0f;
                shakeSlider.maxValue = 1f;
                shakeSlider.value = GameSettings.CameraShake;
            }

            RefreshLabels();
        }

        public void SetSensitivity(float value)
        {
            GameSettings.Sensitivity = value;
            RefreshLabels();
        }

        public void SetShake(float value)
        {
            GameSettings.CameraShake = value;
            RefreshLabels();
        }

        // Compatibility shim for the legacy source-scene assembler. The release finalizer removes
        // the old FOV slider entirely, and this method deliberately changes no runtime state.
        // Keeping the no-op here makes older generated scenes safe to import while world FOV remains 75°.
        public void SetFov(float ignored)
        {
        }

        private void RefreshLabels()
        {
            if (sensitivityValue != null)
                sensitivityValue.text = $"{GameSettings.Sensitivity:0.00}x";
            if (shakeValue != null)
                shakeValue.text = $"{GameSettings.CameraShake * 100f:0}%";
        }
    }
}
