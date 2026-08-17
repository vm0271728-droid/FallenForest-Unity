using System.Collections;
using System.Collections.Generic;
using FallenForest.Core;
using FallenForest.Player;
using UnityEngine;
namespace FallenForest.Monsters
{
    public sealed class MonsterDirector : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private LocustAI locustPrefab;
        [SerializeField] private BoiledOneEncounter boiledPrefab;
        [SerializeField] private List<MonsterSpawnPoint> spawnPoints = new();
        [Range(0f,1f)][SerializeField] private float baseEventChance = .34f;
        [SerializeField] private float checkIntervalMin = 10f, checkIntervalMax = 19f, postEventCooldown = 14f, documentChanceDecay = .0285f;
        [Range(0f,1f)][SerializeField] private float rearWeight = .35f;
        [SerializeField] private float boiledRelativeChance = .20f;
        [SerializeField] private Vector2 locustDistance = new(18f,42f), boiledDistance = new(28f,55f);
        [SerializeField] private float directViewExclusionAngle = 15f;
        [SerializeField] private LayerMask visibilityMask = ~0;
        private bool busy; private float nextAllowedEventTime;
        public float EffectiveSpawnChance { get { int docs = GameProgress.Instance != null ? GameProgress.Instance.DocumentsCollected : 0; return baseEventChance * Mathf.Pow(1f - documentChanceDecay, docs); } }
        private void Start() { if (player == null) { PlayerMotor p = FindFirstObjectByType<PlayerMotor>(); if (p != null) { playerMotor = p; player = p.transform; } } if (playerCamera == null) playerCamera = Camera.main; if (GameProgress.Instance != null) GameProgress.Instance.FinalRunStarted += StartFinalChase; StartCoroutine(SpawnLoop()); }
        private void OnDestroy() { if (GameProgress.Instance != null) GameProgress.Instance.FinalRunStarted -= StartFinalChase; }
        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(checkIntervalMin, checkIntervalMax));
                if (busy || Time.time < nextAllowedEventTime || player == null || GameProgress.Instance == null || GameProgress.Instance.FinalRun) continue;
                if (Random.value > EffectiveSpawnChance) continue;
                bool boiledAllowed = !GameProgress.Instance.BoiledEncountered && GameProgress.Instance.DocumentsCollected >= 2 && GameProgress.Instance.DocumentsCollected <= 8;
                float boiledProbability = boiledAllowed ? boiledRelativeChance / (1f + boiledRelativeChance) : 0f;
                bool wantBoiled = boiledAllowed && Random.value < boiledProbability;
                bool spawned = wantBoiled ? TrySpawnBoiled() : TrySpawnLocust(); if (!spawned && wantBoiled) spawned = TrySpawnLocust(); if (spawned) busy = true;
            }
        }
        private bool TrySpawnLocust() { if (locustPrefab == null) return false; MonsterSpawnPoint p = SelectPoint(MonsterSpawnPoint.SpawnKind.LocustPeek, locustDistance, true); if (p == null) return false; p.PrepareFor(player); LocustAI l = Instantiate(locustPrefab, p.HidePoint.position, p.HidePoint.rotation); l.BeginEncounter(player, playerMotor, p, this); return true; }
        private bool TrySpawnBoiled() { if (boiledPrefab == null) return false; MonsterSpawnPoint p = SelectPoint(MonsterSpawnPoint.SpawnKind.BoiledOpen, boiledDistance, false); if (p == null) return false; BoiledOneEncounter b = Instantiate(boiledPrefab, p.PeekPoint.position, p.PeekPoint.rotation); b.BeginEncounter(player, this); return true; }
        private MonsterSpawnPoint SelectPoint(MonsterSpawnPoint.SpawnKind kind, Vector2 range, bool hidden)
        {
            if (player == null) return null; List<MonsterSpawnPoint> candidates = new(); List<float> cumulative = new(); float total = 0f; Vector3 fwd = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            foreach (MonsterSpawnPoint p in spawnPoints)
            {
                if (p == null || p.Kind != kind) continue; Vector3 delta = p.ReferencePosition - player.position; float d = delta.magnitude; if (d < range.x || d > range.y) continue;
                Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up).normalized; float weight = Vector3.Dot(fwd, flat) < 0f ? rearWeight : 1f;
                if (playerCamera != null) { if (Vector3.Angle(playerCamera.transform.forward, delta.normalized) < directViewExclusionAngle) continue; if (hidden) { Vector3 eye = playerCamera.transform.position; Vector3 target = p.GetHiddenPositionFor(player) + Vector3.up * 1.4f; Vector3 ray = target-eye; if (!Physics.Raycast(eye, ray.normalized, ray.magnitude-.2f, visibilityMask, QueryTriggerInteraction.Ignore)) continue; } }
                candidates.Add(p); total += Mathf.Max(.001f, weight); cumulative.Add(total);
            }
            if (candidates.Count == 0) return null; float pick = Random.value * total; for (int i=0;i<candidates.Count;i++) if (pick <= cumulative[i]) return candidates[i]; return candidates[^1];
        }
        public void NotifyEncounterFinished() { busy = false; nextAllowedEventTime = Time.time + postEventCooldown; }
        private void StartFinalChase() { StopAllCoroutines(); busy = true; foreach (BoiledOneEncounter b in FindObjectsByType<BoiledOneEncounter>(FindObjectsSortMode.None)) Destroy(b.gameObject); if (locustPrefab == null || player == null) return; for (int i=0;i<3;i++) { MonsterSpawnPoint p = SelectPoint(MonsterSpawnPoint.SpawnKind.LocustPeek, new Vector2(24f,48f), true); if (p == null) continue; p.PrepareFor(player); LocustAI l = Instantiate(locustPrefab,p.HidePoint.position,p.HidePoint.rotation); l.BeginFinalChase(player,playerMotor); } }
    }
}
