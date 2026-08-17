using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallenForest.World
{
    /// <summary>
    /// Lightweight spatial database for generated forest trees. MonsterDirector uses it to
    /// find real tree cover around the player instead of relying on hand-placed encounter points.
    /// </summary>
    public sealed class ForestSpatialIndex : MonoBehaviour
    {
        [Serializable]
        public struct TreeRecord
        {
            public Vector3 position;
            public float radius;
            public float height;
            public Transform source;

            public TreeRecord(Vector3 position, float radius, float height, Transform source)
            {
                this.position = position;
                this.radius = radius;
                this.height = height;
                this.source = source;
            }
        }

        [SerializeField, Min(6f)] private float cellSize = 24f;
        [SerializeField] private bool registerSceneTreesOnAwake;
        [SerializeField] private string sceneTreeTag = "ForestTree";

        private readonly Dictionary<long, List<TreeRecord>> cells = new();
        private readonly List<TreeRecord> allTrees = new();

        public int TreeCount => allTrees.Count;

        private void Awake()
        {
            if (registerSceneTreesOnAwake)
                RegisterTaggedSceneTrees();
        }

        public void Clear()
        {
            cells.Clear();
            allTrees.Clear();
        }

        public void RegisterTree(Vector3 worldPosition, float radius, float height, Transform source = null)
        {
            radius = Mathf.Clamp(radius, .25f, 4.5f);
            height = Mathf.Clamp(height, 2f, 60f);
            TreeRecord record = new(worldPosition, radius, height, source);
            allTrees.Add(record);

            Vector2Int c = WorldToCell(worldPosition);
            long key = CellKey(c.x, c.y);
            if (!cells.TryGetValue(key, out List<TreeRecord> bucket))
            {
                bucket = new List<TreeRecord>(8);
                cells.Add(key, bucket);
            }
            bucket.Add(record);
        }

        public int QueryAnnulus(Vector3 center, float minRadius, float maxRadius, List<TreeRecord> results)
        {
            results.Clear();
            if (maxRadius <= 0f || maxRadius < minRadius)
                return 0;

            float minSq = minRadius * minRadius;
            float maxSq = maxRadius * maxRadius;
            int reach = Mathf.CeilToInt(maxRadius / Mathf.Max(1f, cellSize));
            Vector2Int cc = WorldToCell(center);

            for (int z = -reach; z <= reach; z++)
            {
                for (int x = -reach; x <= reach; x++)
                {
                    if (!cells.TryGetValue(CellKey(cc.x + x, cc.y + z), out List<TreeRecord> bucket))
                        continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        TreeRecord t = bucket[i];
                        Vector2 delta = new(t.position.x - center.x, t.position.z - center.z);
                        float sq = delta.sqrMagnitude;
                        if (sq >= minSq && sq <= maxSq)
                            results.Add(t);
                    }
                }
            }
            return results.Count;
        }

        public bool IsOpen(Vector3 worldPosition, float clearanceRadius)
        {
            float radius = Mathf.Max(.1f, clearanceRadius);
            int reach = Mathf.CeilToInt(radius / Mathf.Max(1f, cellSize));
            Vector2Int cc = WorldToCell(worldPosition);

            for (int z = -reach; z <= reach; z++)
            {
                for (int x = -reach; x <= reach; x++)
                {
                    if (!cells.TryGetValue(CellKey(cc.x + x, cc.y + z), out List<TreeRecord> bucket))
                        continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        TreeRecord tree = bucket[i];
                        float required = radius + tree.radius;
                        Vector2 delta = new(tree.position.x - worldPosition.x, tree.position.z - worldPosition.z);
                        if (delta.sqrMagnitude < required * required)
                            return false;
                    }
                }
            }
            return true;
        }

        private void RegisterTaggedSceneTrees()
        {
            GameObject[] tagged;
            try { tagged = GameObject.FindGameObjectsWithTag(sceneTreeTag); }
            catch (UnityException) { return; }

            foreach (GameObject go in tagged)
            {
                if (go == null) continue;
                Bounds b = CalculateBounds(go);
                float radius = Mathf.Max(.3f, Mathf.Min(b.extents.x, b.extents.z));
                RegisterTree(go.transform.position, radius, Mathf.Max(2f, b.size.y), go.transform);
            }
        }

        public static Bounds CalculateBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one * 2f);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private Vector2Int WorldToCell(Vector3 p)
        {
            float inv = 1f / Mathf.Max(1f, cellSize);
            return new Vector2Int(Mathf.FloorToInt(p.x * inv), Mathf.FloorToInt(p.z * inv));
        }

        private static long CellKey(int x, int z) => ((long)x << 32) ^ (uint)z;
    }
}
