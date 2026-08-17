using FallenForest.UI;
using UnityEngine;

namespace FallenForest.Core
{
    public static class SceneFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string ForestScene = "Forest";

        public static void PlayNewGame()
        {
            SaveSystem.BeginNewRun();
            LoadingScreenController.LoadScene(ForestScene);
        }

        public static void ContinueGame() => LoadingScreenController.LoadScene(ForestScene);
        public static void MainMenu() => LoadingScreenController.LoadScene(MainMenuScene);

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
