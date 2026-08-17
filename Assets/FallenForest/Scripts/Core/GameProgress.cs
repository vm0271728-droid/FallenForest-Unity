using System;
using UnityEngine;

namespace FallenForest.Core
{
    public sealed class GameProgress : MonoBehaviour
    {
        public static GameProgress Instance { get; private set; }
        public const int RequiredDocuments = 10;

        public event Action<int> DocumentsChanged;
        public event Action FinalRunStarted;

        [SerializeField] private int documentsCollected;
        [SerializeField] private bool finalRun;
        [SerializeField] private bool boiledEncountered;

        public int DocumentsCollected => documentsCollected;
        public bool FinalRun => finalRun;
        public bool BoiledEncountered => boiledEncountered;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void ResetRun()
        {
            documentsCollected = 0;
            finalRun = false;
            boiledEncountered = false;
            DocumentsChanged?.Invoke(documentsCollected);
        }

        public bool CollectDocument(int documentSlot)
        {
            if (documentsCollected >= RequiredDocuments || SaveSystem.IsDocumentCollected(documentSlot)) return false;
            documentsCollected++;
            SaveSystem.MarkDocumentCollected(documentSlot, documentsCollected, boiledEncountered);
            DocumentsChanged?.Invoke(documentsCollected);

            if (documentsCollected >= RequiredDocuments && !finalRun)
            {
                finalRun = true;
                FinalRunStarted?.Invoke();
            }
            return true;
        }

        public bool CollectDocument()
        {
            for (int slot = 0; slot < RequiredDocuments; slot++)
                if (!SaveSystem.IsDocumentCollected(slot)) return CollectDocument(slot);
            return false;
        }

        public void MarkBoiledEncountered()
        {
            boiledEncountered = true;
            SaveSystem.Save(documentsCollected, boiledEncountered);
        }

        public void Restore(int documents, bool boiledSeen)
        {
            documentsCollected = Mathf.Clamp(documents, 0, RequiredDocuments);
            boiledEncountered = boiledSeen;
            finalRun = documentsCollected >= RequiredDocuments;
            DocumentsChanged?.Invoke(documentsCollected);
            if (finalRun) FinalRunStarted?.Invoke();
        }
    }
}
