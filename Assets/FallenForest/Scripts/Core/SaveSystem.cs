using UnityEngine;

namespace FallenForest.Core
{
    public static class SaveSystem
    {
        private const string DocsKey = "ff_docs";
        private const string DocMaskKey = "ff_doc_mask";
        private const string BoiledKey = "ff_boiled_seen";
        private const string SeedKey = "ff_run_seed";
        private const string HasRunKey = "ff_has_run";
        private const string PosX = "ff_pos_x";
        private const string PosY = "ff_pos_y";
        private const string PosZ = "ff_pos_z";
        private const string HasPosition = "ff_has_position";

        public static int RunSeed
        {
            get
            {
                if (!PlayerPrefs.HasKey(SeedKey))
                {
                    int seed = Random.Range(100000, int.MaxValue);
                    PlayerPrefs.SetInt(SeedKey, seed);
                    PlayerPrefs.SetInt(HasRunKey, 1);
                    PlayerPrefs.Save();
                }
                return PlayerPrefs.GetInt(SeedKey);
            }
        }

        public static int DocumentMask => PlayerPrefs.GetInt(DocMaskKey, 0);
        public static bool HasRun => PlayerPrefs.GetInt(HasRunKey, 0) == 1;

        public static void BeginNewRun()
        {
            DeleteRun();
            int seed = Random.Range(100000, int.MaxValue);
            PlayerPrefs.SetInt(SeedKey, seed);
            PlayerPrefs.SetInt(HasRunKey, 1);
            PlayerPrefs.Save();
        }

        public static void Save(int documents, bool boiledSeen)
        {
            PlayerPrefs.SetInt(DocsKey, Mathf.Clamp(documents, 0, GameProgress.RequiredDocuments));
            PlayerPrefs.SetInt(BoiledKey, boiledSeen ? 1 : 0);
            PlayerPrefs.SetInt(HasRunKey, 1);
            _ = RunSeed;
            PlayerPrefs.Save();
        }

        public static void MarkDocumentCollected(int documentSlot, int documents, bool boiledSeen)
        {
            int slot = Mathf.Clamp(documentSlot, 0, GameProgress.RequiredDocuments - 1);
            int mask = DocumentMask | (1 << slot);
            PlayerPrefs.SetInt(DocMaskKey, mask);
            Save(documents, boiledSeen);
        }

        public static bool IsDocumentCollected(int documentSlot)
        {
            if (documentSlot < 0 || documentSlot >= 31) return false;
            return (DocumentMask & (1 << documentSlot)) != 0;
        }

        public static void SavePlayerPosition(Vector3 position)
        {
            PlayerPrefs.SetFloat(PosX, position.x);
            PlayerPrefs.SetFloat(PosY, position.y);
            PlayerPrefs.SetFloat(PosZ, position.z);
            PlayerPrefs.SetInt(HasPosition, 1);
            PlayerPrefs.SetInt(HasRunKey, 1);
            _ = RunSeed;
            PlayerPrefs.Save();
        }

        public static bool TryLoad(out int documents, out bool boiledSeen, out Vector3 position)
        {
            documents = PlayerPrefs.GetInt(DocsKey, 0);
            boiledSeen = PlayerPrefs.GetInt(BoiledKey, 0) == 1;
            position = new Vector3(PlayerPrefs.GetFloat(PosX, 0f), PlayerPrefs.GetFloat(PosY, 1.5f), PlayerPrefs.GetFloat(PosZ, 0f));
            return HasRun && PlayerPrefs.GetInt(HasPosition, 0) == 1;
        }

        public static void DeleteRun()
        {
            PlayerPrefs.DeleteKey(DocsKey);
            PlayerPrefs.DeleteKey(DocMaskKey);
            PlayerPrefs.DeleteKey(BoiledKey);
            PlayerPrefs.DeleteKey(SeedKey);
            PlayerPrefs.DeleteKey(HasRunKey);
            PlayerPrefs.DeleteKey(PosX);
            PlayerPrefs.DeleteKey(PosY);
            PlayerPrefs.DeleteKey(PosZ);
            PlayerPrefs.DeleteKey(HasPosition);
            PlayerPrefs.Save();
        }
    }
}
