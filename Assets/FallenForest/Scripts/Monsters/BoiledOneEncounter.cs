using System.Collections;
using FallenForest.Cinematics;
using FallenForest.Core;
using UnityEngine;

namespace FallenForest.Monsters
{
    public sealed class BoiledOneEncounter : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform headBone;
        [SerializeField] private float reactionDelay = .08f;
        [SerializeField] private float kneelDuration = 1.15f;
        [SerializeField] private Vector2 untriggeredLifetime = new(28f, 42f);
        [SerializeField] private AudioClip preVideoSting;
        [SerializeField] private BoiledOneSequence sequence;

        private Transform player;
        private MonsterDirector director;
        private MonsterSpawnPoint spawnPoint;
        private bool triggered;
        private float expireAt;
        private Vector3 visualStartLocalPosition;
        private Quaternion visualStartLocalRotation;

        private void OnEnable() => MonsterRegistry.Boiled.Add(this);
        private void OnDisable() => MonsterRegistry.Boiled.Remove(this);

        public void BeginEncounter(Transform p, MonsterDirector owner, MonsterSpawnPoint point = null)
        {
            player = p;
            director = owner;
            spawnPoint = point;
            if (visualRoot == null) visualRoot = transform;
            visualStartLocalPosition = visualRoot.localPosition;
            visualStartLocalRotation = visualRoot.localRotation;
            FacePlayerOnce();
            animator?.SetTrigger("IdleStand");
            expireAt = Time.time + Random.Range(untriggeredLifetime.x, untriggeredLifetime.y);
        }

        private void Update()
        {
            if (triggered) return;
            TrackHeadVerySlowly();
            if (Time.time >= expireAt)
                FinishWithoutTrigger();
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
            headBone.rotation = Quaternion.Slerp(headBone.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), 1f - Mathf.Exp(-.7f * Time.deltaTime));
        }

        public void OnIlluminated()
        {
            if (triggered || GameProgress.Instance == null) return;
            triggered = true;
            // Boiled is consumed only after the player really finds it. An unseen timed-out spawn may occur later.
            GameProgress.Instance.MarkBoiledEncountered();
            StartCoroutine(ReactionRoutine());
        }

        private IEnumerator ReactionRoutine()
        {
            yield return new WaitForSeconds(reactionDelay);
            if (preVideoSting != null)
                AudioSource.PlayClipAtPoint(preVideoSting, transform.position, 1f);

            if (animator != null)
            {
                animator.SetTrigger("Kneel");
                animator.SetBool("EyesClosed", true);
            }
            SetEyeCloseBlendShapes(100f);
            yield return AnimateKneelFallback();

            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

            if (sequence == null)
                sequence = FindFirstObjectByType<BoiledOneSequence>();
            if (sequence != null)
                yield return sequence.PlayAtCurrentPlayerPosition(player);

            FinishAndDestroy();
        }

        private IEnumerator AnimateKneelFallback()
        {
            if (visualRoot == null)
            {
                yield return new WaitForSeconds(kneelDuration);
                yield break;
            }

            Vector3 start = visualStartLocalPosition;
            Vector3 end = start + Vector3.down * .63f;
            Quaternion startRot = visualStartLocalRotation;
            Quaternion endRot = startRot * Quaternion.Euler(11f, 0f, 0f);
            Quaternion headStart = headBone != null ? headBone.localRotation : Quaternion.identity;
            Quaternion headEnd = headStart * Quaternion.Euler(24f, 0f, 0f);
            float t = 0f;

            while (t < kneelDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(.01f, kneelDuration)));
                visualRoot.localPosition = Vector3.Lerp(start, end, u);
                visualRoot.localRotation = Quaternion.Slerp(startRot, endRot, u);
                if (headBone != null)
                    headBone.localRotation = Quaternion.Slerp(headStart, headEnd, u);
                yield return null;
            }
        }

        private void SetEyeCloseBlendShapes(float value)
        {
            foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = smr.sharedMesh;
                if (mesh == null) continue;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string n = mesh.GetBlendShapeName(i).ToLowerInvariant().Replace("_", "").Replace(" ", "");
                    if (n.Contains("blink") || n.Contains("eyeclose") || n.Contains("eyesclose") || n.Contains("closeeye"))
                        smr.SetBlendShapeWeight(i, value);
                }
            }
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
