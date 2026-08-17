using System;
using UnityEngine;

namespace FallenForest.Core
{
    public enum GameLanguage
    {
        English = 0,
        Russian = 1
    }

    /// <summary>Persistent localization core. English is always the first-run default.</summary>
    public static class LocalizationSettings
    {
        private const string LanguageKey = "ff_language";
        public static event Action LanguageChanged;

        public static GameLanguage Language
        {
            get
            {
                int raw = PlayerPrefs.GetInt(LanguageKey, (int)GameLanguage.English);
                return raw == (int)GameLanguage.Russian ? GameLanguage.Russian : GameLanguage.English;
            }
            set
            {
                GameLanguage normalized = value == GameLanguage.Russian ? GameLanguage.Russian : GameLanguage.English;
                PlayerPrefs.SetInt(LanguageKey, (int)normalized);
                PlayerPrefs.Save();
                LanguageChanged?.Invoke();
            }
        }

        public static bool IsRussian => Language == GameLanguage.Russian;

        public static string Text(string key)
        {
            bool ru = IsRussian;
            return key switch
            {
                "play" => ru ? "ИГРАТЬ" : "PLAY",
                "settings" => ru ? "НАСТРОЙКИ" : "SETTINGS",
                "quit" => ru ? "ВЫЙТИ" : "QUIT",
                "back" => ru ? "НАЗАД" : "BACK",
                "main_menu" => ru ? "ГЛАВНОЕ МЕНЮ" : "MAIN MENU",
                "sensitivity" => ru ? "ЧУВСТВИТЕЛЬНОСТЬ" : "SENSITIVITY",
                "camera_shake" => ru ? "ТРЯСКА КАМЕРЫ" : "CAMERA SHAKE",
                "language" => ru ? "ЯЗЫК" : "LANGUAGE",
                "language_value" => ru ? "РУССКИЙ" : "ENGLISH",
                "loading" => ru ? "Загрузка..." : "Loading...",
                "documents" => ru ? "Документы" : "Documents",
                "run" => ru ? "БЕГИ" : "RUN",
                "end" => ru ? "КОНЕЦ" : "END",
                "you_died" => ru ? "ВЫ ПОГИБЛИ" : "YOU DIED",
                "continue" => ru ? "ПРОДОЛЖИТЬ" : "CONTINUE",
                "credits" => ru ? "АВТОРЫ" : "CREDITS",
                "idea" => ru ? "Идея: Meric23" : "Idea by: Meric23",
                "developed" => ru ? "Реализовал: Meric23" : "Developed by: Meric23",
                _ => key
            };
        }
    }
}
