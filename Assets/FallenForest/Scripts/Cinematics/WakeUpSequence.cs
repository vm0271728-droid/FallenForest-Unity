using System.Collections;
using FallenForest.Player;
using UnityEngine;

namespace FallenForest.Cinematics
{
    public sealed class WakeUpSequence : MonoBehaviour
    {
        [SerializeField] private CanvasGroup eyelids, blurOverlay;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private CameraMotion cameraMotion;
        [SerializeField] private AudioSource breathing;
        [SerializeField] private float fullDuration = 5.2f;

        private IEnumerator Start()
        {
            yield return PlayWakeUp(true);
        }

        public IEnumerator PlayWakeUp(bool longVersion)
        {
            ResolveReferences();
            playerMotor?.SetControlsEnabled(false);
            cameraMotion?.SetInputEnabled(false);
            if (breathing != null && !breathing.isPlaying) breathing.Play();

            float duration = longVersion ? fullDuration : fullDuration * 0.58f;
            float t = 0f;
            float startBlur = longVersion ? 0.78f : 0.56f;
            if (eyelids != null) eyelids.alpha = 1f;
            if (blurOverlay != null) blurOverlay.alpha = startBlur;

            Vector3 lowOffset = longVersion
                ? new Vector3(0.10f, -1.26f, 0.06f)
                : new Vector3(0.04f, -0.58f, 0.03f);
            Vector3 lowTilt = longVersion
                ? new Vector3(19f, 0f, 8f)
                : new Vector3(9f, 0f, 4f);

            cameraMotion?.SetCinematicPositionOffset(lowOffset);
            cameraMotion?.SetCinematicRotationOffset(lowTilt);

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eye;
                if (p < 0.16f) eye = 1f - Mathf.SmoothStep(0f, 0.24f, p / 0.16f);
                else if (p < 0.28f) eye = Mathf.SmoothStep(0.24f, 1f, (p - 0.16f) / 0.12f);
                else if (p < 0.50f) eye = 1f - Mathf.SmoothStep(0f, 0.56f, (p - 0.28f) / 0.22f);
                else eye = 1f - Mathf.SmoothStep(0f, 1f, (p - 0.50f) / 0.50f);

                float rise = p < 0.28f ? 0f : Mathf.SmoothStep(0f, 1f, (p - 0.28f) / 0.72f);
                cameraMotion?.SetCinematicPositionOffset(Vector3.Lerp(lowOffset, Vector3.zero, rise));
                cameraMotion?.SetCinematicRotationOffset(Vector3.Lerp(lowTilt, Vector3.zero, rise));
                if (eyelids != null) eyelids.alpha = Mathf.Clamp01(eye);
                if (blurOverlay != null) blurOverlay.alpha = Mathf.Lerp(startBlur, 0f, Mathf.SmoothStep(0.18f, 1f, p));
                yield return null;
            }

            cameraMotion?.ClearCinematicTransform();
            if (eyelids != null) eyelids.alpha = 0f;
            if (blurOverlay != null) blurOverlay.alpha = 0f;
            if (breathing != null) breathing.Stop();
            cameraMotion?.SetInputEnabled(true);
            playerMotor?.SetControlsEnabled(true);
        }

        /// <summary>
        /// Used by the Boiled One encounter. The camera collapses while the eyelids close.
        /// The creature is hidden only after this routine reaches fully closed eyes.
        /// </summary>
        public IEnumerator PlayCollapseToBlack(float duration)
        {
            ResolveReferences();
            playerMotor?.SetControlsEnabled(false);
            cameraMotion?.SetInputEnabled(false);

            duration = Mathf.Max(.15f, duration);
            Vector3 endOffset = new(0.05f, -1.18f, 0.08f);
            Vector3 endTilt = new(6f, 0f, 8f);
            float t = 0f;

            if (eyelids != null) eyelids.alpha = 0f;
            if (blurOverlay != null) blurOverlay.alpha = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                float fall = Mathf.SmoothStep(0f, 1f, p);
                cameraMotion?.SetCinematicPositionOffset(Vector3.Lerp(Vector3.zero, endOffset, fall));
                cameraMotion?.SetCinematicRotationOffset(Vector3.Lerp(Vector3.zero, endTilt, fall));

                float close = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - .34f) / .66f));
                if (eyelids != null) eyelids.alpha = close;
                if (blurOverlay != null) blurOverlay.alpha = close * .24f;
                yield return null;
            }

            cameraMotion?.SetCinematicPositionOffset(endOffset);
            cameraMotion?.SetCinematicRotationOffset(endTilt);
            if (eyelids != null) eyelids.alpha = 1f;
            if (blurOverlay != null) blurOverlay.alpha = .24f;
        }

        public void PrepareForScreamerVideo()
        {
            ResolveReferences();
            cameraMotion?.ClearCinematicTransform();
            if (eyelids != null) eyelids.alpha = 0f;
            if (blurOverlay != null) blurOverlay.alpha = 0f;
        }

        private void ResolveReferences()
        {
            if (playerMotor == null) playerMotor = FindFirstObjectByType<PlayerMotor>();
            if (cameraMotion == null) cameraMotion = FindFirstObjectByType<CameraMotion>();
        }
    }
}
