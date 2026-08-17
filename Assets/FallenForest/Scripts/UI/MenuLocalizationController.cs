using FallenForest.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FallenForest.UI
{
    public sealed class MenuLocalizationController : MonoBehaviour
    {
        [SerializeField] private Text playText;
        [SerializeField] private Text settingsText;
        [SerializeField] private Text quitText;
        [SerializeField] private Text settingsTitle;
        [SerializeField] private Text sensitivityText;
        [SerializeField] private Text shakeText;
        [SerializeField] private Text backText;
        [SerializeField] private Text languageText;
        [SerializeField] private Text creditsText;

        private void OnEnable()
        {
            LocalizationSettings.LanguageChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            LocalizationSettings.LanguageChanged -= Apply;
        }

        public void ToggleLanguage()
        {
            LocalizationSettings.Language = LocalizationSettings.IsRussian
                ? GameLanguage.English
                : GameLanguage.Russian;
        }

        public void Apply()
        {
            Set(playText, LocalizationSettings.Text("play"));
            Set(settingsText, LocalizationSettings.Text("settings"));
            Set(quitText, LocalizationSettings.Text("quit"));
            Set(settingsTitle, LocalizationSettings.Text("settings"));
            Set(sensitivityText, LocalizationSettings.Text("sensitivity"));
            Set(shakeText, LocalizationSettings.Text("camera_shake"));
            Set(backText, LocalizationSettings.Text("back"));
            Set(languageText, LocalizationSettings.Text("language") + ": " + LocalizationSettings.Text("language_value"));
            Set(creditsText, LocalizationSettings.Text("idea") + "\n" + LocalizationSettings.Text("developed"));
        }

        private static void Set(Text target, string value)
        {
            if (target != null) target.text = value;
        }
    }
}
