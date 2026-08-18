using System.Collections;
using FallenForest.Cinematics;
using FallenForest.Core;
using FallenForest.Player;
using FallenForest.World;
using UnityEngine;

namespace FallenForest.Monsters
{
    public sealed class BoiledOneEncounter : MonoBehaviour
    {
        [Header("Model")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform headBone;

        [Header("Gaze trigger")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private LayerMask gazeVisibilityMask = ~0;
        [SerializeField, Range(2f, 25f)] private float gazeTriggerAngle = 10f;
        [SerializeField] private float gazeConfirmationTime = .06f;
        [SerializeField] private float gazeMaxDistance = 70f;
        [SerializeField] private float illuminatedAngleMultiplier = 1.25f;
        [SerializeField] private float illuminationAssistMemory = .14f;

        [Header("Forced encounter")]
        [SerializeField] private float reactionDelay = .08f;
        [SerializeField] private float forcedStareDuration = 3f;
        [SerializeField, Range(.1f, 1f)] private float focusedMoveMultiplier = .33f;
        [SerializeField] private float autoAimResponse = 11f;
        [SerializeField] private float stareFov = 58f;
        [SerializeField] private float collapseDuration = 1.08f;
        [SerializeField] private AudioClip preVideoSting;
        [SerializeField] private BoiledOneSequence sequence;
        [SerializeField] private WakeUpSequence wakeUpSequence;

        [Header("Idle sway")]
        [SerializeField] private float swayDegrees = .85f;
        [SerializeField] private float swayFrequency = .31f;

        [Header("Lifetime")]
        [SerializeField] private Vector2 untriggeredLifetime = new(28f, 42f);

        private Transform player;
        private PlayerMotor playerMotor;
        private CameraMotion cameraMotion;
        private MonsterDirector director;
        private MonsterSpawnPoint spawnPoint;
        private BoiledStressAudio stressAudio;
        private bool triggered;
        private float expireAt;
        private float gazeTimer;
        private float illuminatedUntil;
        private Quaternion visualIdleRotation;
        private float swaySeed;

        private void OnEnable() => MonsterRegistry.Boiled.Add(this);

        private void OnDisable()
        {
            MonsterRegistry.Boiled.Remove(this);
        }

        private void OnDestroy()
        {
            cameraMotion?.ClearForcedLookTarget();
            cameraMotion?.ClearCinematicFov();
            playerMotor?.ClearExternalSpeedMultiplier();
            stressAudio?.StopStress();
        }

        public void BeginEncounter(Transform p, MonsterDirector owner, MonsterSpawnPoint point = null)
        {
            player = p;
            director = owner;
            spawnPoint = point;
            stressAudio = GetComponent<BoiledStressAudio>();
            stressAudio?.SetIntensity(0f);

            GameProgress.Instance?.MarkBoiledEncountered();

            if (visualRoot == null) visualRoot = transform;
            ResolvePlayerReferences();
            FacePlayerOnce();
            visualIdleRotation = visualRoot.localRotation;
            swaySeed = Random.Range(0f, 20f);
            animator?.SetTrigger("IdleStand");
            expireAt = Time.time + Random.Range(untriggeredLifetime.x, untriggeredLifetime.y);
        }

        private void Update()
        {
            if (triggered) return;

            ResolvePlayerReferences();
            ApplySlowIrregularSway();
            TrackHeadVerySlowly();

            bool recentlyIlluminated = Time.unscaledTime <= illuminatedUntil;
            float allowedAngle = gazeTriggerAngle * (recentlyIlluminated ? illuminatedAngleMultiplier : 1f);

            if (IsPlayerLookingAtMe(allowedAngle))
            {
                gazeTimer += Time.unscaledDeltaTime;
                stressAudio?.SetIntensity(Mathf.InverseLerp(0f, Mathf.Max(.01f, gazeConfirmationTime), gazeTimer) * .22f);
                if (gazeTimer >= gazeConfirmationTime)
                    TriggerEncounter();
            }
            else
            {
                gazeTimer = 0f;
                stressAudio?.SetIntensity(0f);
            }

            if (!triggered && Time.time >= expireAt)
                FinishWithoutTrigger();
        }

        private void ResolvePlayerReferences()
        {
            if (playerMotor == null)
            {
                playerMotor = player != null ? player.GetComponent<PlayerMotor>() : null;
                if (playerMotor == null) playerMotor = FindFirstObjectByType<PlayerMotor>();
            }

            if (cameraMotion == null)
                cameraMotion = FindFirstObjectByType<CameraMotion>();

            if (playerCamera == null)
            {
                playerCamera = cameraMotion != null ? cameraMotion.TargetCamera : null;
                if (playerCamera == null) playerCamera = Camera.main;
            }

            if (wakeUpSequence == null)
                wakeUpSequence = FindFirstObjectByType<WakeUpSequence>();
            if (stressAudio == null)
                stressAudio = GetComponent<BoiledStressAudio>();
        }

        private void FacePlayerOnce()
        {
            if (player == null) return;
            Vector3 flat = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up);
            if (flat.sqrMagnitude > .001f)
                transform.rotation = Quaternion.LookRotation(flat.normalized);
        }

        private void ApplySlowIrregularSway()
        {
            if (visualRoot == null) return;
            float t = Time.unscaledTime;
            float x = Mathf.Sin((t + swaySeed) * swayFrequency * .73f) * swayDegrees * .34f;
            float y = Mathf.Sin((t + swaySeed * .37f) * swayFrequency * .41f) * swayDegrees * .23f;
            float z = Mathf.Sin((t + swaySeed * 1.61f) * swayFrequency) * swayDegrees;
            visualRoot.localRotation = visualIdleRotation * Quaternion.Euler(x, y, z);
        }

        private void TrackHeadVerySlowly()
        {
            if (headBone == null || player == null) return;
            Vector3 d = player.position + Vector3.up * 1.4f - headBone.position;
            if (d.sqrMagnitude < .001f) return;
            headBone.rotation = Quaternion.Slerp(
                headBone.rotation,
                Quaternion.LookRotation(d.normalized, Vector3.up),
                1f - Mathf.Exp(-.7f * Time.unscaledDeltaTime));
        }

        private Vector3 GazePoint
        {
            get
            {
                if (headBone != null) return headBone.position;
                if (visualRoot != null) return visualRoot.position + Vector3.up * 1.65f;
                return transform.position + Vector3.up * 1.65f;
            }
        }

        private bool IsPlayerLookingAtMe(float allowedAngle)
        {
            if (playerCamera == null) return false;

            Vector3 target = GazePoint;
            Vector3 delta = target - playerCamera.transform.position;
            float distance = delta.magnitude;
            if (distance < .1f || distance > gazeMaxDistance) return false;
            if (Vector3.Angle(playerCamera.transform.forward, delta) > allowedAngle) return false;

            Vector3 viewport = playerCamera.WorldToViewportPoint(target);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                return false;

            Vector3 centre = visualRoot != null ? visualRoot.position + Vector3.up * .95f : transform.position + Vector3.up * .95f;
            Vector3 lower = visualRoot != null ? visualRoot.position + Vector3.up * .35f : transform.position + Vector3.up * .35f;
            int visible = 0;
            if (HasClearRay(target)) visible++;
            if (HasClearRay(centre)) visible++;
            if (HasClearRay(lower)) visible++;
            return visible >= 2;
        }

        private bool HasClearRay(Vector3 target)
        {
            if (playerCamera == null) return false;
            Vector3 delta = target - playerCamera.transform.position;
            float distance = delta.magnitude;
            if (distance < .05f) return true;

            RaycastHit[] hits = Physics.RaycastAll(
                playerCamera.transform.position,
                delta / distance,
                distance + .35f,
                gazeVisibilityMask,
                QueryTriggerInteraction.Collide);
            if (hits.Length == 0) return false;

            bool[] consumed = new bool[hits.Length];
            for (int consumedCount = 0; consumedCount < hits.Length; consumedCount++)
            {
                int nearest = -1;
                float nearestDistance = float.MaxValue;
                for (int i = 0; i < hits.Length; i++)
                {
                    if (consumed[i] || hits[i].distance >= nearestDistance) continue;
                    nearestDistance = hits[i].distance;
                    nearest = i;
                }
                if (nearest < 0) break;
                consumed[nearest] = true;

                RaycastHit hit = hits[nearest];
                if (hit.collider == null) continue;
                Transform hitTransform = hit.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                    return true;

                if (hit.collider.isTrigger)
                {
                    if (hit.collider.GetComponentInParent<VisibilityOccluder>() != null)
                        return false;
                    continue;
                }
                return false;
            }

            return false;
        }

        public void OnIlluminated()
        {
            if (triggered || GameProgress.Instance == null) return;
            illuminatedUntil = Mathf.Max(illuminatedUntil, Time.unscaledTime + illuminationAssistMemory);
        }

        private void TriggerEncounter()
        {
            if (triggered || GameProgress.Instance == null) return;
            triggered = true;
            StartCoroutine(ReactionRoutine());
        }

        private IEnumerator ReactionRoutine()
        {
            yield return new WaitForSecondsRealtime(reactionDelay);
            ResolvePlayerReferences();

            playerMotor?.SetExternalSpeedMultiplier(focusedMoveMultiplier);
            cameraMotion?.SetInputEnabled(false);

            Transform lookTarget = headBone != null ? headBone : (visualRoot != null ? visualRoot : transform);
            Vector3 lookOffset = headBone != null ? Vector3.zero : Vector3.up * 1.65f;
            cameraMotion?.SetForcedLookTarget(lookTarget, lookOffset, autoAimResponse);
            cameraMotion?.SetCinematicFov(stareFov);
            cameraMotion?.AddShake(.035f, .18f);

            if (preVideoSting != null)
                AudioSource.PlayClipAtPoint(preVideoSting, transform.position, 1f);

            float stare = 0f;
            while (stare < forcedStareDuration)
            {
                stare += Time.unscaledDeltaTime;
                float stress = Mathf.SmoothStep(.20f, 1f, Mathf.Clamp01(stare / Mathf.Max(.01f, forcedStareDuration)));
                stressAudio?.SetIntensity(stress);
                if (cameraMotion != null && stress > .45f)
                    cameraMotion.AddShake(Mathf.Lerp(.025f, .085f, stress), .15f);
                ApplySlowIrregularSway();
                TrackHeadVerySlowly();
                yield return null;
            }

            stressAudio?.SetIntensity(1f);
            playerMotor?.ClearExternalSpeedMultiplier();
            playerMotor?.SetControlsEnabled(false);

            if (wakeUpSequence != null)
            {
                yield return wakeUpSequence.PlayCollapseToBlack(collapseDuration);
            }
            else
            {
                float t = 0f;
                while (t < collapseDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(.01f, collapseDuration)));
                    cameraMotion?.SetCinematicPositionOffset(Vector3.Lerp(Vector3.zero, new Vector3(.05f, -1.18f, .08f), p));
                    cameraMotion?.SetCinematicRotationOffset(Vector3.Lerp(Vector3.zero, new Vector3(6f, 0f, 8f), p));
                    yield return null;
                }
            }

            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
            stressAudio?.StopStress();

            cameraMotion?.ClearForcedLookTarget();
            cameraMotion?.ClearCinematicFov();
            wakeUpSequence?.PrepareForScreamerVideo();

            if (sequence == null)
                sequence = FindFirstObjectByType<BoiledOneSequence>();

            if (sequence != null)
            {
                yield return sequence.PlayAtCurrentPlayerPosition(player);
            }
            else
            {
                cameraMotion?.ClearCinematicTransform();
                cameraMotion?.SetInputEnabled(true);
                playerMotor?.SetControlsEnabled(true);
            }

            SaveSystem.MarkBoiledInfluenced();
            FinishAndDestroy();
        }

        private void FinishWithoutTrigger()
        {
            if (triggered) return;
            stressAudio?.StopStress();
            spawnPoint?.Release();
            director?.NotifyEncounterFinished();
            Destroy(gameObject);
        }

        private void FinishAndDestroy()
        {
            stressAudio?.StopStress();
            playerMotor?.ClearExternalSpeedMultiplier();
            spawnPoint?.Release();
            director?.NotifyEncounterFinished();
            Destroy(gameObject);
        }
    }
}
