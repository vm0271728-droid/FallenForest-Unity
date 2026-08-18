#if UNITY_EDITOR
using FallenForest.Monsters;
using UnityEditor;
using UnityEngine;

namespace FallenForest.EditorTools
{
    /// <summary>Adds skeletal procedural motion fallbacks to the exact user creature prefabs.</summary>
    public static class FallenForestCreatureMotionIntegrator
    {
        private const string LocustPrefab = "Assets/FallenForest/Prefabs/Locust_Final.prefab";
        private const string BoiledPrefab = "Assets/FallenForest/Prefabs/BoiledOne_Final.prefab";

        public static void Apply()
        {
            WireLocust();
            WireBoiled();
        }

        private static void WireLocust()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LocustPrefab) == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(LocustPrefab);
            try
            {
                LocustProceduralAnimator motion = root.GetComponent<LocustProceduralAnimator>();
                if (motion == null) motion = root.AddComponent<LocustProceduralAnimator>();
                Animator animator = root.GetComponentInChildren<Animator>(true);
                LocustAI ai = root.GetComponent<LocustAI>();
                SerializedObject so = new(motion);
                SetObject(so, "animator", animator);
                SetObject(so, "ai", ai);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, LocustPrefab);
                Debug.Log("Fallen Forest: Locust skeletal motion fallback wired.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireBoiled()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BoiledPrefab) == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(BoiledPrefab);
            try
            {
                BoiledProceduralAnimator motion = root.GetComponent<BoiledProceduralAnimator>();
                if (motion == null) motion = root.AddComponent<BoiledProceduralAnimator>();
                BoiledStressAudio stress = root.GetComponent<BoiledStressAudio>();
                if (stress == null) stress = root.AddComponent<BoiledStressAudio>();
                Animator animator = root.GetComponentInChildren<Animator>(true);
                SerializedObject so = new(motion);
                SetObject(so, "animator", animator);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, BoiledPrefab);
                Debug.Log("Fallen Forest: Boiled skeletal motion and stress audio wired.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetObject(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }
    }
}
#endif
