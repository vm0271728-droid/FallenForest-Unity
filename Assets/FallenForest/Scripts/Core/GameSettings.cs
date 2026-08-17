using UnityEngine;

namespace FallenForest.Core
{
    public static class GameSettings
    {
        private const string SensitivityKey = "ff_sensitivity";
        private const string CameraShakeKey = "ff_camera_shake";

        public const float DefaultSensitivity = 1.0f;
        public const float DefaultFov = 75f;
        public const float DefaultCameraShake = 0.70f;

        public static float Sensitivity
        {
            get => PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
            set { PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, 0.3f, 2.5f)); PlayerPrefs.Save(); }
        }

        /// <summary>
        /// World FOV is a fixed design value. The setter remains only for compatibility with older
        /// generated scene code and intentionally does not persist or change the value.
        /// </summary>
        public static float Fov
        {
            get => DefaultFov;
            set { /* fixed by design: no user FOV setting */ }
        }

        public static float CameraShake
        {
            get => PlayerPrefs.GetFloat(CameraShakeKey, DefaultCameraShake);
            set { PlayerPrefs.SetFloat(CameraShakeKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }
    }
}
