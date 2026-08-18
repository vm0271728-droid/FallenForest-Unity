#if UNITY_EDITOR
using System;
using System.IO;
using FallenForest.Documents;
using FallenForest.Player;
using UnityEditor;
using UnityEngine;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Builds reusable prefabs from the exact user-supplied arms, flashlight, document and pickup assets.
    /// Missing source assets remain explicit warnings; no primitive release substitutes are created here.
    /// </summary>
    public static class FinalUserAssetPrefabBuilder
    {
        private const string Root = "Assets/FallenForest";
        private const string PrefabRoot = Root + "/Prefabs/UserContent";
        public const string ArmsPrefab = PrefabRoot + "/FPSArms_Final.prefab";
        public const string FlashlightPrefab = PrefabRoot + "/Flashlight_Final.prefab";
        public const string DocumentPrefab = PrefabRoot + "/DocumentFolder_Final.prefab";
        public const string PickupPrefab = PrefabRoot + "/Pickup_Final.prefab";

        [MenuItem("Fallen Forest/Release/Rebuild User Asset Prefabs")]
        public static void BuildFromMenu()
        {
            BuildAvailable();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BuildAvailable()
        {
            Directory.CreateDirectory(PrefabRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            BuildSimpleModelPrefab(
                Root + "/Art/Viewmodel/Arms",
                "fpsarms",
                ArmsPrefab,
                "FPSArms_Final",
                removeColliders: true);

            BuildSimpleModelPrefab(
                Root + "/Art/Viewmodel/Flashlight",
                "flashlight",
                FlashlightPrefab,
                "Flashlight_Final",
                removeColliders: true);

            BuildDocumentIfAvailable();

            BuildSimpleModelPrefab(
                Root + "/Art/Vehicles/Pickup",
                "pickup",
                PickupPrefab,
                "Pickup_Final",
                removeColliders: false);
        }

        private static void BuildDocumentIfAvailable()
        {
            const string folder = Root + "/Art/Documents/UserDocument";
            GameObject source = FindImportedModel(folder, "document");
            if (source == null)
            {
                Debug.LogWarning(
                    "Fallen Forest: exact document source is present as GLB but is not imported as a GameObject yet. " +
                    "The release integration must add/enable a GLB importer or convert the exact mesh before final build.");
                return;
            }

            GameObject root = InstantiateSource(source, "DocumentFolder_Final");
            try
            {
                Collider collider = root.GetComponentInChildren<Collider>(true);
                if (collider == null)
                {
                    BoxCollider box = root.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                }
                else collider.isTrigger = true;

                if (root.GetComponent<DocumentPickup>() == null)
                    root.AddComponent<DocumentPickup>();

                Save(root, DocumentPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildSimpleModelPrefab(
            string folder,
            string token,
            string output,
            string rootName,
            bool removeColliders)
        {
            GameObject source = FindImportedModel(folder, token);
            if (source == null)
            {
                Debug.LogWarning($"Fallen Forest: user model '{token}' is not imported under {folder}.");
                return;
            }

            GameObject root = InstantiateSource(source, rootName);
            try
            {
                if (removeColliders)
                    foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                        UnityEngine.Object.DestroyImmediate(collider);
                Save(root, output);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject FindImportedModel(string folder, string preferredToken)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { folder });
            GameObject fallback = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".fbx" && ext != ".obj" && ext != ".gltf" && ext != ".glb") continue;
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null || model.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                if (fallback == null) fallback = model;
                if (Path.GetFileNameWithoutExtension(path).IndexOf(preferredToken, StringComparison.OrdinalIgnoreCase) >= 0)
                    return model;
            }
            return fallback;
        }

        private static GameObject InstantiateSource(GameObject source, string name)
        {
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (root == null) root = UnityEngine.Object.Instantiate(source);
            if (root == null) throw new InvalidOperationException($"Could not instantiate user model {source.name}.");
            root.name = name;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return root;
        }

        private static void Save(GameObject root, string output)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(output) != null)
                AssetDatabase.DeleteAsset(output);
            PrefabUtility.SaveAsPrefabAsset(root, output);
            Debug.Log($"Fallen Forest: rebuilt exact user prefab {output}.");
        }
    }
}
#endif
