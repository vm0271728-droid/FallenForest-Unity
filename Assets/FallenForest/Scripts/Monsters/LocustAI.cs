using System.Collections;
using FallenForest.Cinematics;
using FallenForest.Player;
using UnityEngine;

namespace FallenForest.Monsters
{
    public sealed class LocustAI : MonoBehaviour
    {
        public enum LocustState { Hidden, Peeking, Observing, Retreating, Rage, Chasing, Attacking }

        [SerializeField] private Animator animator;
        [SerializeField] private Transform headBone;

        [Header("Encounter distances")]
        [SerializeField] private float observeTime = 4f;
        [SerializeField] private float mediumDistance = 24f;
        [SerializeField, Range(.6f, .95f)] private float safeRetreatFactor = .85f;
        [SerializeField] private float rageTriggerDistance = 13.2f;
        [SerializeField] private float aggressiveAdvanceSpeed = 1.15f;
        [SerializeField] private float flashlightRetreatDistance = 30f;
        [SerializeField] private float warningDistance = 14f;
        [SerializeField] private float instantKillDistance = 7.2f;

        [Header("Movement")]
        [SerializeField] private float peekMoveDuration = .62f;
        [SerializeField] private float retreatMoveDuration = .42f;
        [SerializeField] private float headTrackSpeed = 7f;
        [SerializeField] private float rageSpeed = 4.45f;
        [SerializeField] private float finalChaseSpeedFactor = .975f;
        [SerializeField] private float obstacleProbe = 2.4f;
        [SerializeField] private LayerMask chaseObstacleMask = ~0;

        [Header("Presentation")]
        [SerializeField] private AudioClip nearSting;
        [SerializeField] private JumpscareController jumpscare;

        private Transform player;
        private PlayerMotor playerMotor;
        private MonsterSpawnPoint point;
        private MonsterDirector director;
        private LocustState state;
        private bool finalChase;
        private bool raging;
        private bool warningPlayed;
        private bool startedClose;
        private float previousDistance;
        private int hideVariant;

        public Transform HeadBone => headBone != null ? headBone : transform;
        public Animator Animator => animator;
        public LocustState State => state;
        public bool IsRaging => raging || state == LocustState.Rage;
        public bool IsChasing => state == LocustState.Chasing || state == LocustState.Rage;
        public int HideVariant => hideVariant;
        public float SafeRetreatDistance => mediumDistance * safeRetreatFactor;

        private void OnEnable() => MonsterRegistry.Locusts.Add(this);
        private void OnDisable() => MonsterRegistry.Locusts.Remove(this);

        public void BeginEncounter(Transform p, PlayerMotor motor, MonsterSpawnPoint spawnPoint, MonsterDirector owner)
        {
            player = p;
            playerMotor = motor;
            point = spawnPoint;
            director = owner;
            state = LocustState.Hidden;
            finalChase = false;
            raging = false;
            warningPlayed = false;

            float distance = player != null ? Vector3.Distance(point.ReferencePosition, player.position) : mediumDistance;
            startedClose = distance <= mediumDistance;
            previousDistance = distance;
            hideVariant = ChooseHideVariant(distance);
            transform.SetPositionAndRotation(point.GetHiddenPositionFor(player), point.HidePoint.rotation);
            StartCoroutine(EncounterRoutine());
        }

        public void BeginFinalChase(Transform p, PlayerMotor motor)
        {
            player = p;
            playerMotor = motor;
            point = null;
            finalChase = true;
            raging = false;
            state = LocustState.Chasing;
            animator?.SetTrigger("Run");
        }

        private IEnumerator EncounterRoutine()
        {
            yield return new WaitForSeconds(Random.Range(.35f, 1.15f));
            if (player == null || point == null)
            {
                FinishEncounter();
                yield break;
            }

            state = LocustState.Peeking;
            TriggerHideVariant();
            yield return MoveTo(GetVariantPeekPosition(), peekMoveDuration, true);
            if (state == LocustState.Rage || state == LocustState.Chasing || state == LocustState.Attacking)
                yield break;

            if (player == null)
            {
                FinishEncounter();
                yield break;
            }

            float currentDistance = Vector3.Distance(transform.position, player.position);
            if (currentDistance <= instantKillDistance)
            {
                Attack();
                yield break;
            }

            state = LocustState.Observing;
            float timer = 0f;
            previousDistance = currentDistance;
            while (timer < observeTime && state == LocustState.Observing)
            {
                timer += Time.deltaTime;
                if (player == null) break;

                currentDistance = Vector3.Distance(transform.position, player.position);
                if (EvaluateEncounterPressure(currentDistance))
                    yield break;
                TrackHead();
                previousDistance = currentDistance;
                yield return null;
            }

            if (state == LocustState.Observing)
                yield return RetreatAndFinish(false);
        }

        private void Update()
        {
            if (player == null) return;

            if ((state == LocustState.Chasing || state == LocustState.Rage) && (finalChase || raging))
            {
                ChasePlayer();
                return;
            }

            if (state == LocustState.Observing)
            {
                float distance = Vector3.Distance(transform.position, player.position);
                if (!EvaluateEncounterPressure(distance))
                    TrackHead();
                previousDistance = distance;
            }
        }

        private bool EvaluateEncounterPressure(float distance)
        {
            if (distance <= instantKillDistance)
            {
                Attack();
                return true;
            }

            PlayNearWarning(distance);

            float dt = Mathf.Max(Time.deltaTime, .001f);
            float approachSpeed = (previousDistance - distance) / dt;
            bool aggressiveAdvance = approachSpeed >= aggressiveAdvanceSpeed;

            if (startedClose && distance >= SafeRetreatDistance && approachSpeed < .15f)
            {
                StopAllCoroutines();
                StartCoroutine(RetreatAndFinish(false));
                return true;
            }

            if (distance <= rageTriggerDistance || (aggressiveAdvance && distance < mediumDistance))
            {
                EnterRage();
                return true;
            }

            return false;
        }

        private void ChasePlayer()
        {
            Vector3 to = player.position - transform.position;
            Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
            if (flat.sqrMagnitude > .001f)
            {
                Vector3 dir = AvoidObstacles(flat.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    1f - Mathf.Exp(-(raging ? 11f : 9f) * Time.deltaTime));

                float speed = finalChase
                    ? (playerMotor != null ? playerMotor.CurrentMaxSpeed : 6.99f) * finalChaseSpeedFactor
                    : rageSpeed;
                transform.position += dir * speed * Time.deltaTime;
                SnapToGround();
            }

            if (to.magnitude <= instantKillDistance)
                Attack();
            else
                PlayNearWarning(to.magnitude);
        }

        private void EnterRage()
        {
            if (finalChase || raging || state == LocustState.Attacking) return;
            StopAllCoroutines();
            raging = true;
            state = LocustState.Rage;
            animator?.SetTrigger("Rage");
            FindFirstObjectByType<CameraMotion>()?.AddShake(.075f, .45f);
            StartCoroutine(RageCommit());
        }

        private IEnumerator RageCommit()
        {
            float commit = 0f;
            while (commit < .34f)
            {
                commit += Time.deltaTime;
                if (player == null) yield break;
                TrackHead();
                yield return null;
            }
            if (state == LocustState.Attacking) yield break;
            state = LocustState.Chasing;
            animator?.SetTrigger("Run");
        }

        public void OnHitByFlashlightFromDistance()
        {
            if (raging || finalChase) return;
            if (state != LocustState.Peeking && state != LocustState.Observing) return;
            if (player == null || Vector3.Distance(transform.position, player.position) < flashlightRetreatDistance) return;
            StopAllCoroutines();
            StartCoroutine(RetreatAndFinish(true));
        }

        private IEnumerator RetreatAndFinish(bool startled)
        {
            if (state == LocustState.Attacking || raging || finalChase) yield break;
            state = LocustState.Retreating;
            animator?.SetTrigger(startled ? "StartledRetreat" : "Retreat");

            Vector3 target = point != null ? point.RetreatPoint.position : transform.position - transform.forward * 2f;
            Vector3 start = transform.position;
            float t = 0f;
            float lastDistance = player != null ? Vector3.Distance(transform.position, player.position) : float.MaxValue;
            float duration = startled ? retreatMoveDuration * .72f : retreatMoveDuration;

            while (t < duration)
            {
                t += Time.deltaTime;
                if (player != null)
                {
                    float distance = Vector3.Distance(transform.position, player.position);
                    float approachSpeed = (lastDistance - distance) / Mathf.Max(Time.deltaTime, .001f);
                    if ((distance < SafeRetreatDistance && approachSpeed >= aggressiveAdvanceSpeed) || distance <= rageTriggerDistance)
                    {
                        EnterRage();
                        yield break;
                    }
                    lastDistance = distance;
                }

                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(duration, .01f)));
                transform.position = Vector3.Lerp(start, target, p);
                yield return null;
            }

            FinishEncounter();
        }

        private int ChooseHideVariant(float distance)
        {
            if (distance > mediumDistance + 6f)
                return Random.value < .5f ? 0 : 1; // Far A/B
            if (distance > mediumDistance - 3f)
                return 2; // Medium
            return Random.value < .5f ? 3 : 4; // Close A/B
        }

        private void TriggerHideVariant()
        {
            if (animator == null) return;
            string trigger = hideVariant switch
            {
                0 => "FarHideA",
                1 => "FarHideB",
                2 => "MediumHide",
                3 => "CloseHideA",
                _ => "CloseHideB"
            };
            animator.SetTrigger(trigger);
        }

        private Vector3 GetVariantPeekPosition()
        {
            Vector3 basePosition = point.PeekPoint.position;
            Vector3 right = point.CoverTransform != null ? point.CoverTransform.right : transform.right;
            return hideVariant switch
            {
                0 => basePosition + right * .12f + Vector3.down * .18f,
                1 => basePosition - right * .24f + Vector3.up * .10f,
                2 => basePosition + Vector3.down * .34f,
                3 => basePosition + right * .32f + Vector3.down * .12f,
                _ => basePosition - right * .38f + Vector3.up * .20f
            };
        }

        private IEnumerator MoveTo(Vector3 target, float duration, bool pressureSensitive)
        {
            Vector3 start = transform.position;
            float t = 0f;
            previousDistance = player != null ? Vector3.Distance(start, player.position) : float.MaxValue;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (pressureSensitive && player != null)
                {
                    float distance = Vector3.Distance(transform.position, player.position);
                    if (distance <= instantKillDistance)
                    {
                        Attack();
                        yield break;
                    }
                    if (distance <= rageTriggerDistance)
                    {
                        EnterRage();
                        yield break;
                    }
                    previousDistance = distance;
                }

                transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(duration, .01f))));
                yield return null;
            }
            transform.position = target;
        }

        private void PlayNearWarning(float distance)
        {
            if (warningPlayed || distance > warningDistance) return;
            warningPlayed = true;
            if (nearSting != null)
                AudioSource.PlayClipAtPoint(nearSting, transform.position, 1f);
            FindFirstObjectByType<CameraMotion>()?.AddShake(.045f, .65f);
        }

        private void TrackHead()
        {
            if (headBone == null || player == null) return;
            Vector3 dir = (player.position + Vector3.up * 1.35f - headBone.position).normalized;
            if (dir.sqrMagnitude < .001f) return;
            headBone.rotation = Quaternion.Slerp(
                headBone.rotation,
                Quaternion.LookRotation(dir, Vector3.up),
                1f - Mathf.Exp(-headTrackSpeed * Time.deltaTime));
        }

        private Vector3 AvoidObstacles(Vector3 desired)
        {
            Vector3 origin = transform.position + Vector3.up * 1.8f;
            if (!Physics.SphereCast(origin, .35f, desired, out RaycastHit hit, obstacleProbe, chaseObstacleMask, QueryTriggerInteraction.Ignore))
                return desired;
            if (hit.collider.GetComponentInParent<PlayerMotor>() != null)
                return desired;

            Vector3 a = Vector3.Cross(Vector3.up, hit.normal).normalized;
            Vector3 b = -a;
            Vector3 to = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up).normalized;
            return Vector3.Dot(a, to) > Vector3.Dot(b, to) ? a : b;
        }

        private void SnapToGround()
        {
            Vector3 origin = transform.position + Vector3.up * 3f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<PlayerMotor>() == null)
                transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
        }

        private void Attack()
        {
            if (state == LocustState.Attacking) return;
            state = LocustState.Attacking;
            StopAllCoroutines();
            point?.Release();
            point = null;
            animator?.SetTrigger("Attack");
            if (jumpscare == null) jumpscare = FindFirstObjectByType<JumpscareController>();
            jumpscare?.KillByLocust(this, player);
        }

        public void PrepareJumpscareVariant(int variant)
        {
            if (animator == null) return;
            animator.ResetTrigger("Attack");
            animator.SetTrigger(variant == 0 ? "JumpscareA" : "JumpscareB");
        }

        private void FinishEncounter()
        {
            point?.Release();
            point = null;
            director?.NotifyEncounterFinished();
            Destroy(gameObject);
        }
    }
}
