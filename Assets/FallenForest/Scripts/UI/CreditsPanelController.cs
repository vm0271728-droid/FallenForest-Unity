using FallenForest.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FallenForest.UI
{
    public sealed class CreditsPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Text backText;
        [SerializeField] private Text openButtonText;

        private void OnEnable()
        {
            LocalizationSettings.LanguageChanged += ApplyLocalization;
            ApplyLocalization();
        }

        private void OnDisable()
        {
            LocalizationSettings.LanguageChanged -= ApplyLocalization;
        }

        public void Open()
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(true);
            ApplyLocalization();
        }

        public void Close()
        {
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);
        }

        public void ApplyLocalization()
        {
            bool ru = LocalizationSettings.IsRussian;
            if (openButtonText != null) openButtonText.text = ru ? "АВТОРЫ" : "CREDITS";
            if (titleText != null) titleText.text = ru ? "АВТОРЫ" : "CREDITS";
            if (backText != null) backText.text = ru ? "НАЗАД" : "BACK";
            if (bodyText == null) return;

            bodyText.text = ru
                ? "Идея: Meric23\nРеализовал: Meric23\n\n" +
                  "Оригинальные образы существ: Doctor Nowhere\n" +
                  "Модель Locust: Doumty\n" +
                  "Модель The Boiled One: MG Rips\n\n" +
                  "Fallen Forest — бесплатная некоммерческая фанатская игра.\n" +
                  "Лицензии и полная атрибуция сохранены в материалах проекта."
                : "Idea by: Meric23\nDeveloped by: Meric23\n\n" +
                  "Original creature concepts: Doctor Nowhere\n" +
                  "Locust model: Doumty\n" +
                  "The Boiled One model: MG Rips\n\n" +
                  "Fallen Forest is a free, non-commercial fan game.\n" +
                  "Licenses and full attribution are preserved with the project materials.";
        }
    }
}
