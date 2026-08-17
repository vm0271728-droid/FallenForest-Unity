using FallenForest.Core;
using FallenForest.Player;
using UnityEngine;
using UnityEngine.UI;

namespace FallenForest.UI
{
    public sealed class DeathMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private PlayerMotor player;
        [SerializeField] private CameraMotion cameraMotion;
        [SerializeField] private CanvasGroup blackout;
        [SerializeField] private Text continueText;
        [SerializeField] private Text mainMenuText;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            RefreshLocalization();
        }

        private void OnEnable()
        {
            LocalizationSettings.LanguageChanged += RefreshLocalization;
            RefreshLocalization();
        }

        private void OnDisable() => LocalizationSettings.LanguageChanged -= RefreshLocalization;

        public void Show()
        {
            RefreshLocalization();
            if (panel != null) panel.SetActive(true);
        }

        public void Continue()
        {
            if (player == null) player = FindFirstObjectByType<PlayerMotor>();
            if (cameraMotion == null) cameraMotion = FindFirstObjectByType<CameraMotion>();
            if (SaveSystem.TryLoad(out int docs, out bool boiled, out Vector3 pos))
            {
                GameProgress.Instance?.Restore(docs, boiled);
                player?.Teleport(pos);
            }
            if (blackout != null) blackout.alpha = 0f;
            if (panel != null) panel.SetActive(false);
            cameraMotion?.ClearCinematicFov();
            cameraMotion?.ClearCinematicTransform();
            cameraMotion?.SetInputEnabled(true);
            player?.SetControlsEnabled(true);
        }

        public void MainMenu() => SceneFlow.MainMenu();

        private void RefreshLocalization()
        {
            if (continueText != null) continueText.text = LocalizationSettings.Text("continue");
            if (mainMenuText != null) mainMenuText.text = LocalizationSettings.Text("main_menu");
        }
    }
}
