using FallenForest.Player;
using UnityEngine;

namespace FallenForest.Core
{
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] private GameProgress progress;
        [SerializeField] private PlayerMotor player;
        [SerializeField] private bool continueSavedRun = true;

        private void Start()
        {
            if (progress == null) progress = FindFirstObjectByType<GameProgress>();
            if (player == null) player = FindFirstObjectByType<PlayerMotor>();
            _ = SaveSystem.RunSeed;

            if (continueSavedRun && SaveSystem.TryLoad(out int docs, out bool boiledSeen, out Vector3 pos))
            {
                progress?.Restore(docs, boiledSeen);
                player?.Teleport(pos);
            }
            else
            {
                progress?.ResetRun();
                if (player != null) SaveSystem.SavePlayerPosition(player.transform.position);
            }
        }
    }
}
