using System.Collections;
using FallenForest.Monsters;
using FallenForest.Player;
using FallenForest.UI;
using UnityEngine;

namespace FallenForest.Cinematics
{
    public sealed class JumpscareController : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip[] locustScreamers;
        [SerializeField] private CanvasGroup blackout;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private CameraMotion cameraMotion;
        [SerializeField] private ViewmodelMotionController viewmodelMotion;
        [SerializeField] private FlashlightController flashlight;
        [SerializeField] private Transform jumpscareAnchor;
        [SerializeField] private float blackHold = 1.3f;
        [SerializeField] private DeathMenuController deathMenu;

        private bool busy;

        public void KillByLocust(LocustAI locust, Transform player)
        {
            if (!busy) StartCoroutine(KillRoutine(locust));
        }

        private IEnumerator KillRoutine(LocustAI locust)
        {
            busy = true;
            ResolveReferences();
            playerMotor?.SetControlsEnabled(false);
            cameraMotion?.SetInputEnabled(false);

            int variant = Random.Range(0, 2);
            locust?.PrepareJumpscareVariant(variant);
            PlayScreamer(variant);

            if (variant == 0)
                yield return FrontGrabDeath(locust);
            else
                yield return RearAmbushDeath(locust);

            if (blackout != null) blackout.alpha = 1f;
            flashlight?.SetOn(false);
            yield return new WaitForSecondsRealtime(blackHold);

            source?.Stop();
            if (locust != null) Destroy(locust.gameObject);
            cameraMotion?.ClearForcedLookTarget();
            cameraMotion?.ClearCinematicTransform();
            cameraMotion?.ClearCinematicFov();
            viewmodelMotion?.ClearCinematicPose(20f);
            if (deathMenu == null) deathMenu = FindFirstObjectByType<DeathMenuController>();
            deathMenu?.Show();
            busy = false;
        }

        private IEnumerator FrontGrabDeath(LocustAI locust)
        {
            cameraMotion?.SetCinematicFov(58f);
            cameraMotion?.AddShake(.07f, .28f);
            if (locust != null)
                cameraMotion?.SetForcedLookTarget(locust.HeadBone, Vector3.zero, 16f);

            viewmodelMotion?.SetCinematicPose(
                new Vector3(0f, .025f, -.02f),
                new Vector3(-8f, 0f, -5f),
                new Vector3(-.025f, .02f, -.06f),
                new Vector3(-18f, -8f, 12f),
                15f);

            yield return new WaitForSecondsRealtime(.18f);
            if (locust == null || jumpscareAnchor == null)
            {
                yield return new WaitForSecondsRealtime(1.25f);
                yield break;
            }

            Vector3 start = locust.transform.position;
            Quaternion startRotation = locust.transform.rotation;
            Vector3 firstStop = jumpscareAnchor.position - jumpscareAnchor.forward * .95f + Vector3.down * .28f;
            float t = 0f;
            const float approachDuration = .92f;
            while (t < approachDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / approachDuration);
                float ease = 1f - Mathf.Pow(1f - p, 3f);
                locust.transform.position = Vector3.Lerp(start, firstStop, ease);
                locust.transform.rotation = Quaternion.Slerp(startRotation, jumpscareAnchor.rotation, ease);
                if (p > .45f) cameraMotion?.AddShake(.07f + p * .07f, .9f);
                yield return null;
            }

            // Player instinctively throws both hands/flashlight between their face and the creature.
            viewmodelMotion?.SetCinematicPose(
                new Vector3(0f, .105f, .055f),
                new Vector3(-31f, 3f, -9f),
                new Vector3(-.07f, .09f, .015f),
                new Vector3(-42f, -12f, 24f),
                22f);
            cameraMotion?.SetCinematicFov(48f);
            cameraMotion?.AddShake(.22f, .95f);

            Vector3 impactStart = locust.transform.position;
            Vector3 impactEnd = jumpscareAnchor.position + jumpscareAnchor.forward * .05f + Vector3.down * .08f;
            t = 0f;
            const float impactDuration = .38f;
            while (t < impactDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / impactDuration);
                locust.transform.position = Vector3.Lerp(impactStart, impactEnd, Mathf.SmoothStep(0f, 1f, p));
                cameraMotion?.SetCinematicPositionOffset(new Vector3(
                    Mathf.Sin(p * 19f) * .025f,
                    -.04f * p,
                    -.065f * p));
                cameraMotion?.SetCinematicRotationOffset(new Vector3(-5f * p, Mathf.Sin(p * 13f) * 3f, 7f * p));
                yield return null;
            }

            yield return FadeToBlack(.16f);
        }

        private IEnumerator RearAmbushDeath(LocustAI locust)
        {
            cameraMotion?.SetCinematicFov(72f);
            cameraMotion?.AddShake(.06f, .25f);
            viewmodelMotion?.SetCinematicPose(
                new Vector3(.015f, -.035f, -.025f),
                new Vector3(5f, 0f, 9f),
                new Vector3(.035f, -.045f, -.025f),
                new Vector3(12f, 15f, -16f),
                14f);

            // The camera reacts late: first a hit from behind, then a violent shoulder turn.
            float t = 0f;
            const float turnDuration = .58f;
            while (t < turnDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / turnDuration);
                float yaw = Mathf.SmoothStep(0f, 1f, p) * 148f;
                cameraMotion?.SetCinematicRotationOffset(new Vector3(4f * p, yaw, -13f * p));
                cameraMotion?.SetCinematicPositionOffset(new Vector3(-.05f * p, -.07f * p, -.04f * p));
                if (p > .25f) cameraMotion?.AddShake(.11f + p * .08f, .78f);
                yield return null;
            }

            viewmodelMotion?.SetCinematicPose(
                new Vector3(-.045f, .12f, .08f),
                new Vector3(-38f, -12f, 18f),
                new Vector3(.08f, .14f, .035f),
                new Vector3(-54f, 24f, -34f),
                24f);

            if (locust != null && jumpscareAnchor != null)
            {
                Vector3 start = locust.transform.position;
                Vector3 side = jumpscareAnchor.right * -1.1f;
                Vector3 control = jumpscareAnchor.position + side + jumpscareAnchor.forward * .65f + Vector3.up * .35f;
                Vector3 end = jumpscareAnchor.position + jumpscareAnchor.forward * .03f - Vector3.up * .10f;
                t = 0f;
                const float pullDuration = .72f;
                while (t < pullDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float p = Mathf.Clamp01(t / pullDuration);
                    float one = 1f - p;
                    locust.transform.position = one * one * start + 2f * one * p * control + p * p * end;
                    locust.transform.rotation = Quaternion.Slerp(
                        locust.transform.rotation,
                        jumpscareAnchor.rotation * Quaternion.Euler(-8f, 0f, 18f),
                        1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
                    cameraMotion?.AddShake(.14f + p * .12f, .85f);
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(.72f);
            }

            cameraMotion?.SetCinematicFov(46f);
            yield return FadeToBlack(.18f);
        }

        private IEnumerator FadeToBlack(float duration)
        {
            if (blackout == null)
            {
                yield return new WaitForSecondsRealtime(duration);
                yield break;
            }

            float start = blackout.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                blackout.alpha = Mathf.Lerp(start, 1f, Mathf.Clamp01(t / Mathf.Max(.01f, duration)));
                yield return null;
            }
            blackout.alpha = 1f;
        }

        private void ResolveReferences()
        {
            if (playerMotor == null) playerMotor = FindFirstObjectByType<PlayerMotor>();
            if (cameraMotion == null) cameraMotion = FindFirstObjectByType<CameraMotion>();
            if (viewmodelMotion == null) viewmodelMotion = FindFirstObjectByType<ViewmodelMotionController>();
            if (flashlight == null) flashlight = FindFirstObjectByType<FlashlightController>();
        }

        private void PlayScreamer(int variant)
        {
            if (source == null || locustScreamers == null || locustScreamers.Length == 0) return;
            source.clip = locustScreamers[Mathf.Clamp(variant, 0, locustScreamers.Length - 1)];
            source.pitch = 1f;
            source.Play();
        }
    }
}
