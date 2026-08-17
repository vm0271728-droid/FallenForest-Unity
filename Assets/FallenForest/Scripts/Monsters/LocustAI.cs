using System.Collections;
using FallenForest.Cinematics;
using FallenForest.Player;
using UnityEngine;

namespace FallenForest.Monsters
{
    public sealed class LocustAI : MonoBehaviour
    {
        private enum State { Hidden, Peeking, Observing, Retreating, Chasing, Attacking }

        [SerializeField] private Animator animator;
        [SerializeField] private Transform headBone;
        [SerializeField] private float observeTime = 4f;
        [SerializeField] private float flashlightRetreatDistance = 30f;
        [SerializeField] private float warningDistance = 14f;
        [SerializeField] private float instantKillDistance = 8.5f;
        [SerializeField] private float peekMoveDuration = .55f;
        [SerializeField] private float retreatMoveDuration = .26f;
        [SerializeField] private float headTrackSpeed = 7f;
        [SerializeField] private float finalChaseSpeedFactor = .975f;
        [SerializeField] private float obstacleProbe = 2.4f;
        [SerializeField] private LayerMask chaseObstacleMask = ~0;
        [SerializeField] private AudioClip nearSting;
        [SerializeField] private JumpscareController jumpscare;

        private Transform player;
        private PlayerMotor playerMotor;
        private MonsterSpawnPoint point;
        private MonsterDirector director;
        private State state;
        private bool finalChase;
        private bool warningPlayed;

        public Transform HeadBone => headBone != null ? headBone : transform;
        public Animator Animator => animator;

        private void OnEnable() => MonsterRegistry.Locusts.Add(this);
        private void OnDisable() => MonsterRegistry.Locusts.Remove(this);

        public void BeginEncounter(Transform p, PlayerMotor motor, MonsterSpawnPoint spawnPoint, MonsterDirector owner)
        {
            player = p;
            playerMotor = motor;
            point = spawnPoint;
            director = owner;
            state = State.Hidden;
            warningPlayed = false;
            transform.SetPositionAndRotation(point.HidePoint.position, point.HidePoint.rotation);
            StartCoroutine(EncounterRoutine());
        }

        public void BeginFinalChase(Transform p, PlayerMotor motor)
        {
            player = p;
            playerMotor = motor;
            point = null;
            finalChase = true;
            state = State.Chasing;
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

            state = State.Peeking;
            animator?.SetTrigger(Random.value < .5f ? "PeekLeft" : "PeekRight");
            yield return MoveTo(point.PeekPoint.position, peekMoveDuration);

            if (player == null)
            {
                FinishEncounter();
                yield break;
            }
            if (Vector3.Distance(transform.position, player.position) <= instantKillDistance)
            {
                Attack();
                yield break;
            }

            state = State.Observing;
            float timer = 0f;
            while (timer < observeTime)
            {
                timer += Time.deltaTime;
                if (player == null) break;
                EvaluateProximity(Vector3.Distance(transform.position, player.position));
                if (state == State.Attacking) yield break;
                TrackHead();
                yield return null;
            }

            if (state == State.Observing)
            {
                state = State.Retreating;
                animator?.SetTrigger("Retreat");
                yield return MoveTo(point.RetreatPoint.position, retreatMoveDuration);
                FinishEncounter();
            }
        }

        private void Update()
        {
            if (player == null) return;

            if (finalChase && state == State.Chasing)
            {
                Vector3 to = player.position - transform.position;
                Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
                if (flat.sqrMagnitude > .001f)
                {
                    Vector3 dir = AvoidObstacles(flat.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-9f * Time.deltaTime));
                    // User specification: exactly 2.5% slower than current player top speed.
                    float speed = (playerMotor != null ? playerMotor.CurrentMaxSpeed : 6.99f) * finalChaseSpeedFactor;
                    transform.position += dir * speed * Time.deltaTime;
                    SnapToGround();
                }
                EvaluateProximity(to.magnitude);
            }
            else if (state == State.Observing)
            {
                EvaluateProximity(Vector3.Distance(transform.position, player.position));
                if (state == State.Observing) TrackHead();
            }
        }

        private void EvaluateProximity(float distance)
        {
            if (distance <= instantKillDistance)
            {
                Attack();
                return;
            }
            if (!warningPlayed && distance <= warningDistance)
            {
                warningPlayed = true;
                if (nearSting != null)
                    AudioSource.PlayClipAtPoint(nearSting, transform.position, 1f);
                FindFirstObjectByType<CameraMotion>()?.AddShake(.045f, .65f);
            }
        }

        public void OnHitByFlashlightFromDistance()
        {
            if (state != State.Peeking && state != State.Observing) return;
            if (player == null || Vector3.Distance(transform.position, player.position) < flashlightRetreatDistance) return;
            StopAllCoroutines();
            StartCoroutine(RetreatFromLight());
        }

        private IEnumerator RetreatFromLight()
        {
            state = State.Retreating;
            animator?.SetTrigger("StartledRetreat");
            if (point != null)
                yield return MoveTo(point.RetreatPoint.position, retreatMoveDuration * .68f);
            FinishEncounter();
        }

        private void TrackHead()
        {
            if (headBone == null || player == null) return;
            Vector3 dir = (player.position + Vector3.up * 1.35f - headBone.position).normalized;
            if (dir.sqrMagnitude < .001f) return;
            headBone.rotation = Quaternion.Slerp(headBone.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-headTrackSpeed * Time.deltaTime));
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

        private IEnumerator MoveTo(Vector3 target, float duration)
        {
            Vector3 start = transform.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(duration, .01f))));
                yield return null;
            }
            transform.position = target;
        }

        private void Attack()
        {
            if (state == State.Attacking) return;
            state = State.Attacking;
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
