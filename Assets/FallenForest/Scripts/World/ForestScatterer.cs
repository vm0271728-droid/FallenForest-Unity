using System.Collections.Generic;
using UnityEngine;

namespace FallenForest.World
{
    /// <summary>
    /// Deterministic dense forest scatterer. Broad Perlin clusters create believable dense stands,
    /// narrow sight-lines and occasional open clearings instead of a uniform random carpet.
    /// </summary>
    public sealed class ForestScatterer : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private GameObject[] treePrefabs;
        [SerializeField] private GameObject[] grassPrefabs;
        [SerializeField] private Terrain terrain;
        [SerializeField] private ForestSpatialIndex spatialIndex;

        [Header("Density")]
        [SerializeField, Min(100)] private int treeCount = 3250;
        [SerializeField, Min(0)] private int grassClumpCount = 9000;
        [SerializeField] private int seed = 228117;
        [SerializeField, Range(.001f, .08f)] private float clusterFrequency = .012f;
        [SerializeField, Range(0f, .9f)] private float minimumClusterNoise = .20f;
        [SerializeField, Min(.25f)] private float minimumTreeSpacing = 1.75f;
        [SerializeField, Min(1)] private int placementAttemptsPerTree = 9;

        [Header("Grass rendering")]
        [SerializeField] private bool batchGrassMeshes = true;
        [SerializeField, Min(8f)] private float grassChunkSize = 30f;
        [SerializeField] private bool keepIndividualGrassInEditor;

        [Header("Map")]
        [SerializeField] private float edgePadding = 14f;
        [SerializeField] private float clearStartRadius = 9f;
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform generatedRoot;
        [SerializeField] private bool generateOnStart;

        [Header("Variation")]
        [SerializeField] private Vector2 treeScaleRange = new(.80f, 1.28f);
        [SerializeField] private Vector2 grassScaleRange = new(.70f, 1.40f);
        [SerializeField, Range(0f, 12f)] private float maxTreeLeanDegrees = 2.2f;

        private readonly List<GameObject> spawned = new();
        private readonly Dictionary<long, List<Vector2>> spacingGrid = new();
        private float spacingCellSize;

        private void Start()
        {
            if (generateOnStart)
                Generate();
        }

        [ContextMenu("Generate Forest")]
        public void Generate()
        {
            Clear();
            if (terrain == null || treePrefabs == null || treePrefabs.Length == 0)
                return;

            if (spatialIndex == null)
                spatialIndex = FindFirstObjectByType<ForestSpatialIndex>();
            if (spatialIndex == null)
                spatialIndex = gameObject.AddComponent<ForestSpatialIndex>();
            spatialIndex.Clear();

            if (generatedRoot == null)
            {
                GameObject root = new("GeneratedForest");
                root.transform.SetParent(transform, false);
                generatedRoot = root.transform;
            }

            spacingCellSize = Mathf.Max(.5f, minimumTreeSpacing);
            spacingGrid.Clear();

            Random.State old = Random.state;
            Random.InitState(seed);
            Vector3 terrainOrigin = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            Vector2 noiseOffset = new(Random.Range(-9000f, 9000f), Random.Range(-9000f, 9000f));

            int createdTrees = 0;
            int maxAttempts = Mathf.Max(treeCount * placementAttemptsPerTree, treeCount);
            for (int attempt = 0; attempt < maxAttempts && createdTrees < treeCount; attempt++)
            {
                Vector3 p = RandomPoint(terrainOrigin, terrainSize);
                Vector2 flat = new(p.x, p.z);

                if (startPoint != null && Vector2.Distance(flat, new Vector2(startPoint.position.x, startPoint.position.z)) < clearStartRadius)
                    continue;

                float n = Mathf.PerlinNoise(noiseOffset.x + p.x * clusterFrequency, noiseOffset.y + p.z * clusterFrequency);
                float acceptance = Mathf.InverseLerp(minimumClusterNoise, 1f, n);
                if (Random.value > Mathf.Lerp(.18f, 1f, acceptance))
                    continue;
                if (!HasSpacing(flat))
                    continue;

                p.y = terrain.SampleHeight(p) + terrainOrigin.y;
                GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
                if (prefab == null)
                    continue;

                Quaternion rotation = Quaternion.Euler(
                    Random.Range(-maxTreeLeanDegrees, maxTreeLeanDegrees),
                    Random.Range(0f, 360f),
                    Random.Range(-maxTreeLeanDegrees, maxTreeLeanDegrees));
                GameObject go = Instantiate(prefab, p, rotation, generatedRoot);
                float scale = Random.Range(treeScaleRange.x, treeScaleRange.y);
                go.transform.localScale *= scale;
                spawned.Add(go);
                RegisterSpacing(flat);

                Bounds bounds = ForestSpatialIndex.CalculateBounds(go);
                float radius = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z), .35f, 2.4f);
                spatialIndex.RegisterTree(go.transform.position, radius, Mathf.Max(3f, bounds.size.y), go.transform);
                createdTrees++;
            }

            if (createdTrees < treeCount)
                Debug.LogWarning($"Fallen Forest: generated {createdTrees}/{treeCount} trees. Reduce spacing or increase placement attempts if intentional density is not reached.");

            GenerateGrass(terrainOrigin, terrainSize);
            Random.state = old;
        }

        private void GenerateGrass(Vector3 terrainOrigin, Vector3 terrainSize)
        {
            if (grassPrefabs == null || grassPrefabs.Length == 0 || grassClumpCount <= 0)
                return;

            bool useBatching = batchGrassMeshes && (Application.isPlaying || !keepIndividualGrassInEditor);
            GrassMeshBatcher batcher = useBatching ? new GrassMeshBatcher(grassChunkSize, generatedRoot) : null;
            int fallbackObjects = 0;

            for (int i = 0; i < grassClumpCount; i++)
            {
                Vector3 p = RandomPoint(terrainOrigin, terrainSize);
                p.y = terrain.SampleHeight(p) + terrainOrigin.y;
                GameObject prefab = grassPrefabs[Random.Range(0, grassPrefabs.Length)];
                if (prefab == null) continue;

                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float scale = Random.Range(grassScaleRange.x, grassScaleRange.y);
                bool batched = batcher != null && batcher.Add(prefab, p, rotation, scale);
                if (batched) continue;

                GameObject go = Instantiate(prefab, p, rotation, generatedRoot);
                go.transform.localScale *= scale;
                spawned.Add(go);
                fallbackObjects++;
            }

            if (batcher != null)
            {
                int chunks = batcher.Build();
                Debug.Log($"Fallen Forest: grass batched into {chunks} renderer chunks; {fallbackObjects} unsupported clumps used GameObject fallback.");
            }
        }

        private bool HasSpacing(Vector2 p)
        {
            int cx = Mathf.FloorToInt(p.x / spacingCellSize);
            int cz = Mathf.FloorToInt(p.y / spacingCellSize);
            float minSq = minimumTreeSpacing * minimumTreeSpacing;
            for (int z = -1; z <= 1; z++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    if (!spacingGrid.TryGetValue(CellKey(cx + x, cz + z), out List<Vector2> bucket))
                        continue;
                    for (int i = 0; i < bucket.Count; i++)
                        if ((bucket[i] - p).sqrMagnitude < minSq)
                            return false;
                }
            }
            return true;
        }

        private void RegisterSpacing(Vector2 p)
        {
            int cx = Mathf.FloorToInt(p.x / spacingCellSize);
            int cz = Mathf.FloorToInt(p.y / spacingCellSize);
            long key = CellKey(cx, cz);
            if (!spacingGrid.TryGetValue(key, out List<Vector2> bucket))
            {
                bucket = new List<Vector2>(8);
                spacingGrid.Add(key, bucket);
            }
            bucket.Add(p);
        }

        private Vector3 RandomPoint(Vector3 p, Vector3 s) => new(
            Random.Range(p.x + edgePadding, p.x + s.x - edgePadding),
            0f,
            Random.Range(p.z + edgePadding, p.z + s.z - edgePadding));

        private static long CellKey(int x, int z) => ((long)x << 32) ^ (uint)z;

        [ContextMenu("Clear Forest")]
        public void Clear()
        {
            if (spatialIndex != null)
                spatialIndex.Clear();

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] == null) continue;
                if (Application.isPlaying) Destroy(spawned[i]); else DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
            spacingGrid.Clear();

            if (generatedRoot != null)
            {
                for (int i = generatedRoot.childCount - 1; i >= 0; i--)
                {
                    GameObject child = generatedRoot.GetChild(i).gameObject;
                    if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
                }
            }
        }
    }
}
