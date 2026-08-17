using System;
using System.Collections.Generic;
using FallenForest.Core;
using FallenForest.World;
using UnityEngine;

namespace FallenForest.Documents
{
    /// <summary>
    /// Deterministically chooses ten valid ground positions. Hand-authored points are supported,
    /// but the final forest can generate a much larger candidate pool from Terrain at runtime so
    /// documents genuinely move between new games without ever landing inside trees or steep slopes.
    /// </summary>
    public sealed class DocumentSpawner : MonoBehaviour
    {
        [SerializeField] private DocumentPickup documentPrefab;
        [SerializeField] private List<DocumentSpawnPoint> spawnPoints = new();
        [SerializeField] private int count = 10;
        [SerializeField] private float minimumSpacing = 28f;
        [SerializeField] private float minimumFromStart = 34f;
        [SerializeField] private Transform startPoint;
        [SerializeField] private int seedOverride;

        [Header("Runtime candidate generation")]
        [SerializeField] private bool generateRuntimeCandidates = true;
        [SerializeField] private Terrain terrain;
        [SerializeField] private ForestSpatialIndex forestIndex;
        [SerializeField, Min(20)] private int runtimeCandidateCount = 140;
        [SerializeField, Min(100)] private int maximumSamplingAttempts = 1800;
        [SerializeField] private float terrainEdgePadding = 18f;
        [SerializeField, Range(0f, 60f)] private float maximumSlope = 24f;
        [SerializeField] private float treeClearance = 1.35f;
        [SerializeField] private float objectClearance = .65f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        private struct Candidate
        {
            public Vector3 position;
            public Quaternion rotation;
            public Candidate(Vector3 p, Quaternion r) { position = p; rotation = r; }
        }

        private readonly List<Vector3> chosenPositions = new();
        private readonly List<Candidate> candidates = new();

        private void Start() => SpawnDocuments();

        public void SpawnDocuments()
        {
            if (documentPrefab == null) return;
            int seed = seedOverride != 0 ? seedOverride : SaveSystem.RunSeed;
            var rng = new System.Random(seed ^ 0x5F3759DF);

            BuildCandidatePool(rng);
            if (candidates.Count == 0)
            {
                Debug.LogWarning("Fallen Forest: no valid document candidate positions are available.");
                return;
            }

            Shuffle(candidates, rng);
            chosenPositions.Clear();
            int selected = 0;

            for (int i = 0; i < candidates.Count && selected < count; i++)
            {
                Candidate candidate = candidates[i];
                bool tooClose = false;
                for (int c = 0; c < chosenPositions.Count; c++)
                {
                    if (Vector3.Distance(Flat(chosenPositions[c]), Flat(candidate.position)) < minimumSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                chosenPositions.Add(candidate.position);
                int slot = selected++;
                if (SaveSystem.IsDocumentCollected(slot))
                    continue;

                DocumentPickup instance = Instantiate(documentPrefab, candidate.position, candidate.rotation, transform);
                instance.name = $"Document_{slot + 1:00}";
                instance.Configure(slot);
            }

            if (selected < count)
                Debug.LogWarning($"Fallen Forest: only {selected}/{count} valid document positions. Increase candidate count or lower minimum spacing.");
        }

        private void BuildCandidatePool(System.Random rng)
        {
            candidates.Clear();

            foreach (DocumentSpawnPoint point in spawnPoints)
            {
                if (point == null || !point.EnabledForSpawn) continue;
                Vector3 position = point.transform.position;
                if (startPoint != null && Vector3.Distance(Flat(position), Flat(startPoint.position)) < minimumFromStart)
                    continue;
                candidates.Add(new Candidate(position, point.transform.rotation));
            }

            if (!generateRuntimeCandidates)
                return;

            if (terrain == null) terrain = FindFirstObjectByType<Terrain>();
            if (forestIndex == null) forestIndex = FindFirstObjectByType<ForestSpatialIndex>();
            if (terrain == null) return;

            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            int accepted = 0;
            int attempts = 0;
            int target = Mathf.Max(runtimeCandidateCount, count * 8);

            while (accepted < target && attempts++ < maximumSamplingAttempts)
            {
                float x = LerpRandom(rng, origin.x + terrainEdgePadding, origin.x + size.x - terrainEdgePadding);
                float z = LerpRandom(rng, origin.z + terrainEdgePadding, origin.z + size.z - terrainEdgePadding);
                Vector3 position = new(x, 0f, z);
                position.y = terrain.SampleHeight(position) + origin.y + .035f;

                if (startPoint != null && Vector3.Distance(Flat(position), Flat(startPoint.position)) < minimumFromStart)
                    continue;

                Vector3 normalized = new(
                    Mathf.InverseLerp(origin.x, origin.x + size.x, x),
                    0f,
                    Mathf.InverseLerp(origin.z, origin.z + size.z, z));
                Vector3 normal = terrain.terrainData.GetInterpolatedNormal(normalized.x, normalized.z);
                if (Vector3.Angle(normal, Vector3.up) > maximumSlope)
                    continue;

                if (forestIndex != null && !forestIndex.IsOpen(position, treeClearance))
                    continue;

                // Ignore Terrain itself but reject rocks, logs, structures or another pickup occupying the folder area.
                Collider[] overlaps = Physics.OverlapSphere(position + Vector3.up * .18f, objectClearance, obstructionMask, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int i = 0; i < overlaps.Length; i++)
                {
                    Collider col = overlaps[i];
                    if (col == null || col is TerrainCollider) continue;
                    if (col.GetComponentInParent<Player.PlayerMotor>() != null) continue;
                    blocked = true;
                    break;
                }
                if (blocked) continue;

                float yaw = (float)rng.NextDouble() * 360f;
                Quaternion groundRotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, yaw, 0f);
                candidates.Add(new Candidate(position, groundRotation));
                accepted++;
            }
        }

        private static void Shuffle(List<Candidate> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static float LerpRandom(System.Random rng, float min, float max) =>
            Mathf.Lerp(min, max, (float)rng.NextDouble());

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
