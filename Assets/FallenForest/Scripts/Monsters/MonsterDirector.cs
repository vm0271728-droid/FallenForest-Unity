using System.Collections;
using System.Collections.Generic;
using FallenForest.Core;
using FallenForest.Player;
using FallenForest.World;
using UnityEngine;

namespace FallenForest.Monsters
{
    /// <summary>
    /// Central horror pacing system. Normal encounters are generated in a ring around the player.
    /// Locust uses actual nearby tree cover from ForestSpatialIndex, while Boiled One searches for
    /// an open patch and can only complete its encounter once per run.
    /// </summary>
    public sealed class MonsterDirector : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private Transform player;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerMotor playerMotor;

        [Header("Creatures")]
        [SerializeField] private LocustAI locustPrefab;
        [SerializeField] private BoiledOneEncounter boiledPrefab;

        [Header("Dynamic world sampling")]
        [SerializeField] private ForestSpatialIndex forestIndex;
        [SerializeField] private Terrain terrain;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private bool preferDynamicSpawnRing = true;
        [SerializeField, Min(4)] private int boiledOpenPointAttempts = 20;
        [SerializeField] private float boiledTreeClearance = 4.2f;

        [Header("Fallback authored points")]
        [SerializeField] private List<MonsterSpawnPoint> spawnPoints = new();

        [Header("Pacing")]
        [Range(0f, 1f)] [SerializeField] private float baseEventChance = .34f;
        [SerializeField] private float checkIntervalMin = 10f;
        [SerializeField] private float checkIntervalMax = 19f;
        [SerializeField] private float postEventCooldown = 14f;
        [SerializeField] private float documentChanceDecay = .0285f;
        [Range(0f, 1f)] [SerializeField] private float rearWeight = .35f;
        [SerializeField] private float boiledRelativeChance = .20f;

        [Header("Spawn radii")]
        [SerializeField] private Vector2 locustDistance = new(18f, 42f);
        [SerializeField] private Vector2 boiledDistance = new(28f, 55f);
        [SerializeField] private float directViewExclusionAngle = 15f;
        [SerializeField] private LayerMask visibilityMask = ~0;

        private readonly List<ForestSpatialIndex.TreeRecord> treeCandidates = new(256);
        private bool busy;
        private float nextAllowedEventTime;

        public float EffectiveSpawnChance
        {
            get
            {
                int docs = GameProgress.Instance != null ? GameProgress.Instance.DocumentsCollected : 0;
                return baseEventChance * Mathf.Pow(1f - documentChanceDecay, docs);
            }
        }

        private void Start()
        {
            ResolveReferences();
            if (GameProgress.Instance != null)
                GameProgress.Instance.FinalRunStarted += StartFinalChase;
            StartCoroutine(SpawnLoop());
        }

        private void OnDestroy()
        {
            if (GameProgress.Instance != null)
                GameProgress.Instance.FinalRunStarted -= StartFinalChase;
        }

        private void ResolveReferences()
        {
            if (player == null)
            {
                PlayerMotor p = FindFirstObjectByType<PlayerMotor>();
                if (p != null)
                {
                    playerMotor = p;
                    player = p.transform;
                }
            }
            if (playerCamera == null) playerCamera = Camera.main;
            if (forestIndex == null) forestIndex = FindFirstObjectByType<ForestSpatialIndex>();
            if (terrain == null) terrain = FindFirstObjectByType<Terrain>();
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(checkIntervalMin, checkIntervalMax));
                if (busy || Time.time < nextAllowedEventTime || player == null || GameProgress.Instance == null || GameProgress.Instance.FinalRun)
                    continue;
                if (Random.value > EffectiveSpawnChance)
                    continue;

                bool boiledAllowed = !GameProgress.Instance.BoiledEncountered &&
                                     GameProgress.Instance.DocumentsCollected >= 2 &&
                                     GameProgress.Instance.DocumentsCollected <= 8;

                // Relative odds: Boiled = 0.20 * Locust => exactly five times rarer.
                float boiledProbability = boiledAllowed ? boiledRelativeChance / (1f + boiledRelativeChance) : 0f;
                bool wantBoiled = boiledAllowed && Random.value < boiledProbability;
                bool spawned = wantBoiled ? TrySpawnBoiled() : TrySpawnLocust();
                if (!spawned && wantBoiled)
                    spawned = TrySpawnLocust();
                if (spawned)
                    busy = true;
            }
        }

        private bool TrySpawnLocust()
        {
            if (locustPrefab == null) return false;
            MonsterSpawnPoint point = preferDynamicSpawnRing ? CreateDynamicLocustPoint(locustDistance) : null;
            if (point == null)
                point = SelectAuthoredPoint(MonsterSpawnPoint.SpawnKind.LocustPeek, locustDistance, true);
            if (point == null) return false;

            point.PrepareFor(player);
            LocustAI locust = Instantiate(locustPrefab, point.HidePoint.position, point.HidePoint.rotation);
            locust.BeginEncounter(player, playerMotor, point, this);
            return true;
        }

        private bool TrySpawnBoiled()
        {
            if (boiledPrefab == null) return false;
            MonsterSpawnPoint point = preferDynamicSpawnRing ? CreateDynamicBoiledPoint() : null;
            if (point == null)
                point = SelectAuthoredPoint(MonsterSpawnPoint.SpawnKind.BoiledOpen, boiledDistance, false);
            if (point == null) return false;

            BoiledOneEncounter boiled = Instantiate(boiledPrefab, point.PeekPoint.position, point.PeekPoint.rotation);
            boiled.BeginEncounter(player, this, point);
            return true;
        }

        private MonsterSpawnPoint CreateDynamicLocustPoint(Vector2 range)
        {
            if (forestIndex == null || player == null)
                return null;
            if (forestIndex.QueryAnnulus(player.position, range.x, range.y, treeCandidates) == 0)
                return null;

            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            float totalWeight = 0f;
            float[] weights = new float[treeCandidates.Count];

            for (int i = 0; i < treeCandidates.Count; i++)
            {
                ForestSpatialIndex.TreeRecord tree = treeCandidates[i];
                Vector3 delta = tree.position - player.position;
                Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
                if (flat.sqrMagnitude < .01f) continue;

                float viewAngle = playerCamera != null ? Vector3.Angle(playerCamera.transform.forward, delta.normalized) : 180f;
                if (viewAngle < directViewExclusionAngle)
                    continue;

                Vector3 flatDir = flat.normalized;
                float weight = Vector3.Dot(forward, flatDir) < 0f ? rearWeight : 1f;
                float distance = flat.magnitude;
                float mid = (range.x + range.y) * .5f;
                float half = Mathf.Max(1f, (range.y - range.x) * .5f);
                float distancePreference = 1f - Mathf.Clamp01(Mathf.Abs(distance - mid) / half) * .35f;
                weight *= distancePreference;

                Vector3 hidden = tree.position - flatDir * (tree.radius + .48f) + Vector3.up * 1.4f;
                if (playerCamera != null)
                {
                    Vector3 ray = hidden - playerCamera.transform.position;
                    if (ray.sqrMagnitude > .1f && !Physics.Raycast(playerCamera.transform.position, ray.normalized, ray.magnitude - .2f, visibilityMask, QueryTriggerInteraction.Ignore))
                        continue;
                }

                weights[i] = Mathf.Max(.001f, weight);
                totalWeight += weights[i];
            }

            if (totalWeight <= 0f)
                return null;

            float pick = Random.value * totalWeight;
            for (int i = 0; i < treeCandidates.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                pick -= weights[i];
                if (pick > 0f) continue;
                ForestSpatialIndex.TreeRecord chosen = treeCandidates[i];
                return MonsterSpawnPoint.CreateRuntimeLocust(chosen.position, chosen.radius, player);
            }
            return null;
        }

        private MonsterSpawnPoint CreateDynamicBoiledPoint()
        {
            if (player == null)
                return null;

            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            for (int i = 0; i < boiledOpenPointAttempts; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                bool rear = Vector3.Dot(forward, dir) < 0f;
                if (rear && Random.value > rearWeight)
                    continue;

                float distance = Random.Range(boiledDistance.x, boiledDistance.y);
                Vector3 candidate = player.position + dir * distance;
                if (!TryProjectToGround(candidate, out candidate))
                    continue;
                if (forestIndex != null && !forestIndex.IsOpen(candidate, boiledTreeClearance))
                    continue;

                if (playerCamera != null)
                {
                    Vector3 delta = candidate + Vector3.up * 1.7f - playerCamera.transform.position;
                    if (Vector3.Angle(playerCamera.transform.forward, delta.normalized) < Mathf.Max(20f, directViewExclusionAngle))
                        continue; // never materialise while directly watched
                }

                return MonsterSpawnPoint.CreateRuntimeBoiled(candidate, player);
            }
            return null;
        }

        private bool TryProjectToGround(Vector3 point, out Vector3 grounded)
        {
            if (terrain != null)
            {
                Vector3 tp = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (point.x >= tp.x && point.x <= tp.x + size.x && point.z >= tp.z && point.z <= tp.z + size.z)
                {
                    point.y = terrain.SampleHeight(point) + tp.y;
                    grounded = point;
                    return true;
                }
            }

            Vector3 origin = point + Vector3.up * 20f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 45f, groundMask, QueryTriggerInteraction.Ignore))
            {
                grounded = hit.point;
                return true;
            }
            grounded = point;
            return false;
        }

        private MonsterSpawnPoint SelectAuthoredPoint(MonsterSpawnPoint.SpawnKind kind, Vector2 range, bool hidden)
        {
            if (player == null) return null;
            List<MonsterSpawnPoint> candidates = new();
            List<float> cumulative = new();
            float total = 0f;
            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;

            foreach (MonsterSpawnPoint p in spawnPoints)
            {
                if (p == null || p.Kind != kind) continue;
                Vector3 delta = p.ReferencePosition - player.position;
                float d = delta.magnitude;
                if (d < range.x || d > range.y) continue;
                Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up).normalized;
                float weight = Vector3.Dot(forward, flat) < 0f ? rearWeight : 1f;

                if (playerCamera != null)
                {
                    if (Vector3.Angle(playerCamera.transform.forward, delta.normalized) < directViewExclusionAngle)
                        continue;
                    if (hidden)
                    {
                        Vector3 eye = playerCamera.transform.position;
                        Vector3 target = p.GetHiddenPositionFor(player) + Vector3.up * 1.4f;
                        Vector3 ray = target - eye;
                        if (!Physics.Raycast(eye, ray.normalized, ray.magnitude - .2f, visibilityMask, QueryTriggerInteraction.Ignore))
                            continue;
                    }
                }

                candidates.Add(p);
                total += Mathf.Max(.001f, weight);
                cumulative.Add(total);
            }

            if (candidates.Count == 0) return null;
            float pick = Random.value * total;
            for (int i = 0; i < candidates.Count; i++)
                if (pick <= cumulative[i]) return candidates[i];
            return candidates[^1];
        }

        public void NotifyEncounterFinished()
        {
            busy = false;
            nextAllowedEventTime = Time.time + postEventCooldown;
        }

        private void StartFinalChase()
        {
            StopAllCoroutines();
            busy = true;

            foreach (BoiledOneEncounter b in FindObjectsByType<BoiledOneEncounter>(FindObjectsSortMode.None))
                Destroy(b.gameObject);

            if (locustPrefab == null || player == null)
                return;

            for (int i = 0; i < 3; i++)
            {
                MonsterSpawnPoint point = preferDynamicSpawnRing ? CreateDynamicLocustPoint(new Vector2(24f, 48f)) : null;
                if (point == null)
                    point = SelectAuthoredPoint(MonsterSpawnPoint.SpawnKind.LocustPeek, new Vector2(24f, 48f), true);
                if (point == null) continue;

                point.PrepareFor(player);
                LocustAI locust = Instantiate(locustPrefab, point.HidePoint.position, point.HidePoint.rotation);
                point.Release();
                locust.BeginFinalChase(player, playerMotor);
            }
        }
    }
}
