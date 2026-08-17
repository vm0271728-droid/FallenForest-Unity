using UnityEngine;

namespace FallenForest.Core
{
    public static class GameSettings
    {
        private const string SensitivityKey = "ff_sensitivity";
        private const string FovKey = "ff_fov";
        private const string CameraShakeKey = "ff_camera_shake";

        public const float DefaultSensitivity = 1.0f;
        public const float DefaultFov = 75f;
        public const float DefaultCameraShake = 0.70f;

        public static float Sensitivity
        {
            get => PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
            set { PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, 0.3f, 2.5f)); PlayerPrefs.Save(); }
        }

        public static float Fov
        {
            get => PlayerPrefs.GetFloat(FovKey, DefaultFov);
            set { PlayerPrefs.SetFloat(FovKey, Mathf.Clamp(value, 60f, 100f)); PlayerPrefs.Save(); }
        }

        public static float CameraShake
        {
            get => PlayerPrefs.GetFloat(CameraShakeKey, DefaultCameraShake);
            set { PlayerPrefs.SetFloat(CameraShakeKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }
    }
}
