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
        private bool flashlightDropped;
        private DeathStressOverlay deathStress;

        public void KillByLocust(LocustAI locust, Transform player)
        {
            if (!busy) StartCoroutine(KillRoutine(locust));
        }

        private IEnumerator KillRoutine(LocustAI locust)
        {
            busy = true;
            flashlightDropped = false;
            ResolveReferences();
            if (deathStress == null) deathStress = GetComponent<DeathStressOverlay>() ?? gameObject.AddComponent<DeathStressOverlay>();
            deathStress.StopImmediately();
            if (blackout != null) blackout.alpha = 0f;
            playerMotor?.SetControlsEnabled(false);
            cameraMotion?.SetInputEnabled(false);

            int variant = Random.Range(0, 2);
            locust?.PrepareJumpscareVariant(variant);
            PlayScreamer(variant);

            if (variant == 0)
                yield return FrontPierceDeath(locust);
            else
                yield return RearPierceDeath(locust);

            if (blackout != null) blackout.alpha = 1f;
            deathStress?.SetIntensity(0f);
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

        private IEnumerator FrontPierceDeath(LocustAI locust)
        {
            cameraMotion?.SetCinematicFov(61f);
            cameraMotion?.AddShake(.06f, .22f);
            if (locust != null)
                cameraMotion?.SetForcedLookTarget(locust.HeadBone, Vector3.zero, 16f);

            // Initial defensive rise while the creature commits to the frontal lunge.
            viewmodelMotion?.SetCinematicPose(
                new Vector3(0f, .045f, .02f),
                new Vector3(-15f, 1f, -5f),
                new Vector3(-.025f, .035f, -.035f),
                new Vector3(-22f, -8f, 14f),
                15f);

            yield return new WaitForSecondsRealtime(.15f);
            if (locust == null || jumpscareAnchor == null)
            {
                DropFlashlight(false);
                deathStress?.SetIntensity(.85f);
                yield return new WaitForSecondsRealtime(1.25f);
                yield return FadeToBlack(.28f);
                yield break;
            }

            Vector3 start = locust.transform.position;
            Quaternion startRotation = locust.transform.rotation;
            Vector3 piercePoint = jumpscareAnchor.position - jumpscareAnchor.forward * .32f + Vector3.down * .20f;
            float t = 0f;
            const float lungeDuration = .76f;
            while (t < lungeDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / lungeDuration);
                float ease = 1f - Mathf.Pow(1f - p, 3f);
                locust.transform.position = Vector3.Lerp(start, piercePoint, ease);
                locust.transform.rotation = Quaternion.Slerp(startRotation, jumpscareAnchor.rotation, ease);
                if (p > .50f) cameraMotion?.AddShake(.06f + p * .08f, .35f);
                yield return null;
            }

            // Chest impact. The exact supplied flashlight leaves the hand, stays ON and becomes a
            // real Rigidbody so its beam can sweep the forest while the player is forced down.
            DropFlashlight(false);
            deathStress?.SetIntensity(.42f);
            cameraMotion?.SetCinematicFov(52f);
            cameraMotion?.AddShake(.25f, .72f);

            viewmodelMotion?.SetCinematicPose(
                new Vector3(0f, .14f, .085f),
                new Vector3(-38f, 4f, -11f),
                Vector3.zero,
                Vector3.zero,
                24f);

            // Forced collapse: horizon is lost in a controlled roll while both hands panic upward.
            t = 0f;
            const float collapse = .88f;
            while (t < collapse)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / collapse);
                deathStress?.SetIntensity(Mathf.Lerp(.42f, .76f, p));
                cameraMotion?.SetCinematicPositionOffset(new Vector3(
                    Mathf.Sin(p * 21f) * .022f,
                    Mathf.Lerp(-.04f, -.66f, p),
                    Mathf.Lerp(-.05f, -.12f, p)));
                cameraMotion?.SetCinematicRotationOffset(new Vector3(
                    Mathf.Lerp(-4f, 13f, p),
                    Mathf.Sin(p * 11f) * 3f,
                    Mathf.Lerp(7f, 27f, p)));
                yield return null;
            }

            // Strength fades: the hands sink and the creature brings its head into the player's face.
            viewmodelMotion?.SetCinematicPose(
                new Vector3(.015f, -.10f, .05f),
                new Vector3(20f, -5f, 16f),
                Vector3.zero,
                Vector3.zero,
                5.5f);
            cameraMotion?.SetCinematicFov(45f);
            deathStress?.SetIntensity(.92f);

            Vector3 closeStart = locust.transform.position;
            Vector3 closeEnd = jumpscareAnchor.position + jumpscareAnchor.forward * .025f - jumpscareAnchor.up * .03f;
            t = 0f;
            const float closeDuration = .46f;
            while (t < closeDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / closeDuration));
                locust.transform.position = Vector3.Lerp(closeStart, closeEnd, p);
                cameraMotion?.AddShake(.12f + p * .09f, .18f);
                yield return null;
            }

            deathStress?.SetIntensity(1f);
            yield return FadeToBlack(.68f);
        }

        private IEnumerator RearPierceDeath(LocustAI locust)
        {
            cameraMotion?.SetCinematicFov(73f);
            cameraMotion?.AddShake(.16f, .28f);
            deathStress?.SetIntensity(.18f);

            // Hard shock from behind. Flashlight is thrown before the camera has fully turned.
            viewmodelMotion?.SetCinematicPose(
                new Vector3(.015f, -.03f, -.01f),
                new Vector3(8f, 0f, 10f),
                new Vector3(.035f, -.045f, -.025f),
                new Vector3(14f, 16f, -18f),
                18f);
            DropFlashlight(true);

            float t = 0f;
            const float turnDuration = .62f;
            while (t < turnDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / turnDuration);
                float yaw = Mathf.SmoothStep(0f, 1f, p) * 150f;
                cameraMotion?.SetCinematicRotationOffset(new Vector3(5f * p, yaw, -15f * p));
                cameraMotion?.SetCinematicPositionOffset(new Vector3(-.05f * p, -.09f * p, -.06f * p));
                deathStress?.SetIntensity(Mathf.Lerp(.18f, .52f, p));
                if (p > .20f) cameraMotion?.AddShake(.12f + p * .08f, .16f);
                yield return null;
            }

            // Both hands clamp around the piercing arm in front of the chest and pull against it.
            viewmodelMotion?.SetCinematicPose(
                new Vector3(-.015f, .135f, .105f),
                new Vector3(-43f, -9f, 13f),
                Vector3.zero,
                Vector3.zero,
                24f);

            if (locust != null && jumpscareAnchor != null)
            {
                Vector3 start = locust.transform.position;
                Vector3 side = jumpscareAnchor.right * -1.05f;
                Vector3 control = jumpscareAnchor.position + side + jumpscareAnchor.forward * .58f + Vector3.up * .28f;
                Vector3 end = jumpscareAnchor.position + jumpscareAnchor.forward * .02f - Vector3.up * .10f;
                t = 0f;
                const float pullDuration = .86f;
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
                    deathStress?.SetIntensity(Mathf.Lerp(.52f, .78f, p));
                    cameraMotion?.AddShake(.12f + p * .10f, .17f);
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(.86f);
            }

            // Grip weakens: hands sink, first asymmetrically then almost fully out of frame.
            viewmodelMotion?.SetCinematicPose(
                new Vector3(.045f, .015f, .085f),
                new Vector3(-8f, 12f, 24f),
                Vector3.zero,
                Vector3.zero,
                5.5f);
            deathStress?.SetIntensity(.90f);
            yield return new WaitForSecondsRealtime(.38f);
            viewmodelMotion?.SetCinematicPose(
                new Vector3(.08f, -.19f, .04f),
                new Vector3(28f, 15f, 35f),
                Vector3.zero,
                Vector3.zero,
                4.2f);
            cameraMotion?.SetCinematicFov(49f);
            yield return new WaitForSecondsRealtime(.42f);

            deathStress?.SetIntensity(1f);
            yield return FadeToBlack(.76f);
        }

        private void DropFlashlight(bool rearHit)
        {
            if (flashlightDropped || flashlight == null || !flashlight.Acquired) return;
            Camera camera = cameraMotion != null ? cameraMotion.TargetCamera : Camera.main;
            Vector3 forward = camera != null ? camera.transform.forward : transform.forward;
            Vector3 right = camera != null ? camera.transform.right : transform.right;
            Vector3 up = Vector3.up;
            Vector3 velocity = rearHit
                ? right * 1.45f + forward * .65f + up * .30f
                : -right * 1.05f + forward * .45f + up * .22f;
            Vector3 angular = rearHit
                ? new Vector3(8.5f, 5.0f, -10.5f)
                : new Vector3(-6.5f, 8.0f, 9.5f);
            Rigidbody dropped = PhysicalFlashlightDrop.Drop(flashlight, velocity, angular, rearHit ? "DroppedFlashlight_RearDeath" : "DroppedFlashlight_FrontDeath");
            flashlightDropped = dropped != null;
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
                float p = Mathf.Clamp01(t / Mathf.Max(.01f, duration));
                blackout.alpha = Mathf.Lerp(start, 1f, Mathf.SmoothStep(0f, 1f, p));
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
