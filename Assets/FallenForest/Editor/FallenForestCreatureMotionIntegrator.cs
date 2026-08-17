#if UNITY_EDITOR
using FallenForest.Monsters;
using UnityEditor;
using UnityEngine;

namespace FallenForest.EditorTools
{
    /// <summary>Adds the mobile procedural Locust motion fallback to the final exact prefab.</summary>
    public static class FallenForestCreatureMotionIntegrator
    {
        private const string LocustPrefab = "Assets/FallenForest/Prefabs/Locust_Final.prefab";

        public static void Apply()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LocustPrefab) == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(LocustPrefab);
            try
            {
                LocustProceduralAnimator motion = root.GetComponent<LocustProceduralAnimator>();
                if (motion == null) motion = root.AddComponent<LocustProceduralAnimator>();

                Animator animator = root.GetComponentInChildren<Animator>(true);
                SerializedObject so = new(motion);
                SerializedProperty animatorProperty = so.FindProperty("animator");
                if (animatorProperty != null) animatorProperty.objectReferenceValue = animator;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, LocustPrefab);
                Debug.Log("Fallen Forest: Locust procedural gameplay motion fallback wired.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
