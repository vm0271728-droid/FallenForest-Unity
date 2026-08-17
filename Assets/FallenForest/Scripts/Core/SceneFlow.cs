using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallenForest.Core
{
    public static class SceneFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string ForestScene = "Forest";

        public static void PlayNewGame()
        {
            SaveSystem.BeginNewRun();
            SceneManager.LoadScene(ForestScene);
        }

        public static void ContinueGame() => SceneManager.LoadScene(ForestScene);
        public static void MainMenu() => SceneManager.LoadScene(MainMenuScene);

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
