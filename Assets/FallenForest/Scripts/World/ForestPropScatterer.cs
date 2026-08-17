using System;
using UnityEngine;

namespace FallenForest.World
{
    /// <summary>
    /// Deterministically scatters the user's non-tree forest props (primarily rocks) after trails
    /// and trees exist. Props stay off trail centres, follow terrain normals and participate in
    /// physics/gaze occlusion through colliders authored on their release prefabs.
    /// </summary>
    public sealed class ForestPropScatterer : MonoBehaviour
    {
        [SerializeField] private Terrain terrain;
        [SerializeField] private GameObject[] propPrefabs;
        [SerializeField] private int seed = 451909;
        [SerializeField, Range(0, 180)] private int targetCount = 68;
        [SerializeField, Range(0f, 45f)] private float maxSlope = 31f;
        [SerializeField] private Vector2 uniformScaleRange = new(.72f, 1.38f);
        [SerializeField] private float trailClearance = 2.3f;
        [SerializeField] private float edgeMargin = 11f;
        [SerializeField] private Transform generatedRoot;

        public int GeneratedCount { get; private set; }

        [ContextMenu("Generate Forest Props")]
        public void Generate()
        {
            if (terrain == null) terrain = Terrain.activeTerrain;
            EnsureRoot();
            ClearRoot();
            GeneratedCount = 0;
            if (terrain == null || terrain.terrainData == null || propPrefabs == null || propPrefabs.Length == 0)
                return;

            var rng = new System.Random(seed);
            Vector3 terrainOrigin = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            int attempts = Mathf.Max(targetCount * 18, 64);

            for (int attempt = 0; attempt < attempts && GeneratedCount < targetCount; attempt++)
            {
                float x = terrainOrigin.x + Next(rng, edgeMargin, Mathf.Max(edgeMargin, terrainSize.x - edgeMargin));
                float z = terrainOrigin.z + Next(rng, edgeMargin, Mathf.Max(edgeMargin, terrainSize.z - edgeMargin));
                float nx = Mathf.InverseLerp(terrainOrigin.x, terrainOrigin.x + terrainSize.x, x);
                float nz = Mathf.InverseLerp(terrainOrigin.z, terrainOrigin.z + terrainSize.z, z);
                Vector3 normal = terrain.terrainData.GetInterpolatedNormal(nx, nz);
                float slope = Vector3.Angle(normal, Vector3.up);
                if (slope > maxSlope) continue;

                Vector3 position = new(x, terrain.SampleHeight(new Vector3(x, 0f, z)) + terrainOrigin.y, z);
                if (IsNearTrail(position)) continue;
                if (IsNearProtectedAnchor(position)) continue;

                GameObject prefab = propPrefabs[rng.Next(0, propPrefabs.Length)];
                if (prefab == null) continue;
                GameObject instance = Instantiate(prefab, position, Quaternion.identity, generatedRoot);
                instance.name = $"{prefab.name}_{GeneratedCount + 1:000}";

                float yaw = Next(rng, 0f, 360f);
                Quaternion groundTilt = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, .55f));
                instance.transform.rotation = groundTilt * Quaternion.Euler(0f, yaw, 0f);
                float scale = Next(rng, uniformScaleRange.x, uniformScaleRange.y);
                instance.transform.localScale *= scale;
                GeneratedCount++;
            }

            Physics.SyncTransforms();
        }

        private bool IsNearTrail(Vector3 position)
        {
            Collider[] overlaps = Physics.OverlapSphere(position + Vector3.up * 1.2f, trailClearance, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider overlap in overlaps)
                if (overlap != null && overlap.GetComponentInParent<TrailZone>() != null)
                    return true;
            return false;
        }

        private static bool IsNearProtectedAnchor(Vector3 position)
        {
            GameObject start = GameObject.Find("PlayerStart");
            if (start != null && Vector3.SqrMagnitude(start.transform.position - position) < 9f * 9f) return true;
            GameObject flashlight = GameObject.Find("FlashlightPickup");
            if (flashlight != null && Vector3.SqrMagnitude(flashlight.transform.position - position) < 7f * 7f) return true;
            return false;
        }

        private void EnsureRoot()
        {
            if (generatedRoot != null) return;
            Transform existing = transform.Find("GeneratedForestProps");
            if (existing != null)
            {
                generatedRoot = existing;
                return;
            }
            GameObject root = new("GeneratedForestProps");
            root.transform.SetParent(transform, false);
            generatedRoot = root.transform;
        }

        private void ClearRoot()
        {
            if (generatedRoot == null) return;
            for (int i = generatedRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = generatedRoot.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        private static float Next(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * Mathf.Max(0f, max - min);
        }
    }
}
