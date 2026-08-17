using System.Collections;
using FallenForest.Cinematics;
using FallenForest.Core;
using FallenForest.Player;
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

        [Header("Forced encounter")]
        [SerializeField] private float reactionDelay = .08f;
        [SerializeField] private float forcedStareDuration = 3f;
        [SerializeField] private float autoAimResponse = 11f;
        [SerializeField] private float stareFov = 58f;
        [SerializeField] private float collapseDuration = 1.08f;
        [SerializeField] private AudioClip preVideoSting;
        [SerializeField] private BoiledOneSequence sequence;
        [SerializeField] private WakeUpSequence wakeUpSequence;

        [Header("Lifetime")]
        [SerializeField] private Vector2 untriggeredLifetime = new(28f, 42f);

        private Transform player;
        private PlayerMotor playerMotor;
        private CameraMotion cameraMotion;
        private MonsterDirector director;
        private MonsterSpawnPoint spawnPoint;
        private bool triggered;
        private float expireAt;
        private float gazeTimer;

        private void OnEnable() => MonsterRegistry.Boiled.Add(this);

        private void OnDisable()
        {
            MonsterRegistry.Boiled.Remove(this);
        }

        private void OnDestroy()
        {
            if (!triggered) return;
            cameraMotion?.ClearForcedLookTarget();
            cameraMotion?.ClearCinematicFov();
        }

        public void BeginEncounter(Transform p, MonsterDirector owner, MonsterSpawnPoint point = null)
        {
            player = p;
            director = owner;
            spawnPoint = point;

            // Fixed design rule: the first Boiled spawn consumes the special encounter for this run,
            // even if the player never finds it before its untriggered lifetime expires.
            GameProgress.Instance?.MarkBoiledEncountered();

            if (visualRoot == null) visualRoot = transform;
            ResolvePlayerReferences();
            FacePlayerOnce();
            animator?.SetTrigger("IdleStand");
            expireAt = Time.time + Random.Range(untriggeredLifetime.x, untriggeredLifetime.y);
        }

        private void Update()
        {
            if (triggered) return;

            ResolvePlayerReferences();
            TrackHeadVerySlowly();

            if (IsPlayerLookingAtMe(gazeTriggerAngle))
            {
                gazeTimer += Time.unscaledDeltaTime;
                if (gazeTimer >= gazeConfirmationTime)
                    TriggerEncounter();
            }
            else
            {
                gazeTimer = 0f;
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
        }

        private void FacePlayerOnce()
        {
            if (player == null) return;
            Vector3 flat = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up);
            if (flat.sqrMagnitude > .001f)
                transform.rotation = Quaternion.LookRotation(flat.normalized);
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

            Vector3 direction = delta / distance;
            if (Physics.Raycast(
                    playerCamera.transform.position,
                    direction,
                    out RaycastHit hit,
                    distance + .35f,
                    gazeVisibilityMask,
                    QueryTriggerInteraction.Ignore))
            {
                Transform hitTransform = hit.transform;
                if (hitTransform != transform && !hitTransform.IsChildOf(transform))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Flashlight exposure no longer starts the cinematic by itself. The player must actually
        /// catch the Boiled One in their view; illumination merely gives a slightly wider gaze cone.
        /// </summary>
        public void OnIlluminated()
        {
            if (triggered || GameProgress.Instance == null) return;
            ResolvePlayerReferences();
            if (IsPlayerLookingAtMe(gazeTriggerAngle * 1.25f))
                TriggerEncounter();
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

            playerMotor?.SetControlsEnabled(false);
            cameraMotion?.SetInputEnabled(false);

            Transform lookTarget = headBone != null ? headBone : (visualRoot != null ? visualRoot : transform);
            Vector3 lookOffset = headBone != null ? Vector3.zero : Vector3.up * 1.65f;
            cameraMotion?.SetForcedLookTarget(lookTarget, lookOffset, autoAimResponse);
            cameraMotion?.SetCinematicFov(stareFov);
            cameraMotion?.AddShake(.035f, .18f);

            if (preVideoSting != null)
                AudioSource.PlayClipAtPoint(preVideoSting, transform.position, 1f);

            animator?.SetTrigger("IdleStand");

            // Exactly three seconds of helpless eye contact. Touch-look is disabled and the camera
            // continuously corrects itself toward the creature's head.
            float stare = 0f;
            while (stare < forcedStareDuration)
            {
                stare += Time.unscaledDeltaTime;
                TrackHeadVerySlowly();
                yield return null;
            }

            // The PLAYER collapses while still being forced to look at the Boiled One.
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

            cameraMotion?.ClearForcedLookTarget();
            cameraMotion?.ClearCinematicFov();
            wakeUpSequence?.PrepareForScreamerVideo();

            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

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

            // Persistent after-effect starts only once the cinematic/wake-up has finished. It is
            // saved independently from the spawn flag because an unseen Boiled spawn must not corrupt the screen.
            SaveSystem.MarkBoiledInfluenced();
            FinishAndDestroy();
        }

        private void FinishWithoutTrigger()
        {
            if (triggered) return;
            spawnPoint?.Release();
            director?.NotifyEncounterFinished();
            Destroy(gameObject);
        }

        private void FinishAndDestroy()
        {
            spawnPoint?.Release();
            director?.NotifyEncounterFinished();
            Destroy(gameObject);
        }
    }
}
