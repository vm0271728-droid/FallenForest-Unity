using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallenForest.Player
{
    /// <summary>
    /// Authored-feeling additive idle layer for the supplied FPS hand rig. Runs after the base
    /// physical viewmodel solver, so breathing/turn lag remain intact while grip corrections,
    /// tension and rare switch checks alter the actual wrist/finger bones and flashlight transform.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class FlashlightIdleAnimator : MonoBehaviour
    {
        private enum Variant { None, GripCorrection, Tension, SwitchCheck }

        [SerializeField] private PlayerMotor player;
        [SerializeField] private FlashlightController flashlight;
        [SerializeField] private ViewmodelMotionController viewmodel;
        [SerializeField] private Vector2 normalInterval = new(5.2f, 10.5f);
        [SerializeField] private Vector2 rareInterval = new(23f, 46f);
        [SerializeField] private float motionNoise = .0025f;

        private Transform rightWrist;
        private Transform rightPalm;
        private Transform rightThumb1;
        private Transform rightThumb2;
        private readonly List<Transform> rightFingers = new();
        private readonly Dictionary<Transform, Quaternion> binds = new();
        private Transform flashlightRoot;
        private Vector3 lastPositionOffset;
        private Quaternion lastRotationOffset = Quaternion.identity;
        private float nextNormalAt;
        private float nextRareAt;
        private float variantStartedAt;
        private float variantDuration;
        private Variant variant;
        private float seed;
        private bool rigResolved;

        private void Awake()
        {
            ResolveReferences();
            seed = UnityEngine.Random.Range(0f, 1000f);
            ScheduleNormal();
            ScheduleRare();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (!rigResolved) ResolveRig();
            RemovePreviousFlashlightOffset();

            if (player == null || flashlight == null || !flashlight.Acquired || viewmodel == null)
                return;

            bool idle = !player.IsMoving && player.NormalizedSpeed < .05f && !viewmodel.IsInCinematicPose;
            if (!idle)
            {
                variant = Variant.None;
                ApplyIrregularMovementNoise();
                return;
            }

            if (variant == Variant.None)
                TryStartVariant();

            float p = variant == Variant.None
                ? 0f
                : Mathf.Clamp01((Time.unscaledTime - variantStartedAt) / Mathf.Max(.05f, variantDuration));

            Vector3 positionOffset = Vector3.zero;
            Vector3 rotationEuler = Vector3.zero;
            Vector3 wristEuler = Vector3.zero;
            float fingerCurl = 0f;
            float thumbCurl = 0f;

            switch (variant)
            {
                case Variant.GripCorrection:
                {
                    float down = SmoothPulse(p, 0f, .28f, .78f, 1f);
                    float twist = SmoothPulse(p, .12f, .38f, .70f, .95f);
                    positionOffset = new Vector3(.002f, -.012f * down, -.006f * down);
                    rotationEuler = new Vector3(3.5f * down, -2.8f * twist, 5.8f * twist);
                    wristEuler = new Vector3(2.5f * twist, -3.0f * twist, 5.2f * twist);
                    // Fingers briefly relax, then re-tighten around the real flashlight body.
                    fingerCurl = Mathf.Lerp(-4.5f, 7.5f, Mathf.SmoothStep(.36f, .82f, p)) * Mathf.Sin(p * Mathf.PI);
                    thumbCurl = fingerCurl * .45f;
                    break;
                }
                case Variant.Tension:
                {
                    float envelope = Mathf.Sin(p * Mathf.PI);
                    float correction = Mathf.Sin(p * Mathf.PI * 2f) * envelope;
                    positionOffset = new Vector3(.003f * correction, -.019f * envelope, -.008f * envelope);
                    rotationEuler = new Vector3(7.2f * envelope, -2.0f * correction, -4.8f * correction);
                    wristEuler = new Vector3(-3.8f * envelope, 2.2f * correction, -5.8f * correction);
                    fingerCurl = 9f * Mathf.SmoothStep(.28f, .72f, p) * (1f - Mathf.SmoothStep(.78f, 1f, p));
                    thumbCurl = 5f * envelope;
                    break;
                }
                case Variant.SwitchCheck:
                {
                    float envelope = Mathf.Sin(p * Mathf.PI);
                    float check = Mathf.SmoothStep(.18f, .54f, p) * (1f - Mathf.SmoothStep(.62f, .92f, p));
                    positionOffset = new Vector3(.001f, -.004f * envelope, .002f * envelope);
                    rotationEuler = new Vector3(-1.5f * envelope, 2.2f * envelope, 1.8f * envelope);
                    wristEuler = new Vector3(-1.2f * envelope, -1.8f * envelope, 2.4f * envelope);
                    thumbCurl = -11f * check;
                    fingerCurl = 2.2f * envelope;
                    break;
                }
            }

            // Non-periodic micro movement prevents the base locomotion from reading as a perfect
            // repeated weapon-bob loop on a phone screen.
            float noiseX = Mathf.PerlinNoise(seed, Time.unscaledTime * .48f) * 2f - 1f;
            float noiseY = Mathf.PerlinNoise(seed + 13.7f, Time.unscaledTime * .41f) * 2f - 1f;
            positionOffset += new Vector3(noiseX, noiseY, 0f) * motionNoise;
            rotationEuler += new Vector3(noiseY * .35f, noiseX * .55f, noiseX * .25f);

            ApplyFlashlightOffset(positionOffset, Quaternion.Euler(rotationEuler));
            ApplyHandPose(wristEuler, fingerCurl, thumbCurl);

            if (variant != Variant.None && p >= 1f)
            {
                variant = Variant.None;
                ScheduleNormal();
                if (Time.unscaledTime >= nextRareAt) ScheduleRare();
            }
        }

        private void TryStartVariant()
        {
            float now = Time.unscaledTime;
            if (now >= nextRareAt)
            {
                StartVariant(Variant.SwitchCheck, UnityEngine.Random.Range(.72f, 1.02f));
                ScheduleRare();
                return;
            }
            if (now < nextNormalAt) return;

            if (UnityEngine.Random.value < .58f)
                StartVariant(Variant.GripCorrection, UnityEngine.Random.Range(1.05f, 1.42f));
            else
                StartVariant(Variant.Tension, UnityEngine.Random.Range(1.35f, 1.92f));
        }

        private void StartVariant(Variant next, float duration)
        {
            variant = next;
            variantStartedAt = Time.unscaledTime;
            variantDuration = duration;
        }

        private void ApplyIrregularMovementNoise()
        {
            if (flashlightRoot == null || viewmodel.IsInCinematicPose) return;
            float speed = Mathf.Clamp01(player.NormalizedSpeed);
            float nx = Mathf.PerlinNoise(seed + 31f, Time.unscaledTime * 1.7f) * 2f - 1f;
            float ny = Mathf.PerlinNoise(seed + 53f, Time.unscaledTime * 1.35f) * 2f - 1f;
            Vector3 pos = new Vector3(nx, ny, 0f) * motionNoise * speed * 1.65f;
            Quaternion rot = Quaternion.Euler(ny * .65f * speed, nx * .9f * speed, nx * .55f * speed);
            ApplyFlashlightOffset(pos, rot);
        }

        private void ApplyFlashlightOffset(Vector3 position, Quaternion rotation)
        {
            if (flashlightRoot == null) return;
            flashlightRoot.localPosition += position;
            flashlightRoot.localRotation *= rotation;
            lastPositionOffset = position;
            lastRotationOffset = rotation;
        }

        private void RemovePreviousFlashlightOffset()
        {
            if (flashlightRoot == null)
            {
                lastPositionOffset = Vector3.zero;
                lastRotationOffset = Quaternion.identity;
                return;
            }

            flashlightRoot.localPosition -= lastPositionOffset;
            flashlightRoot.localRotation *= Quaternion.Inverse(lastRotationOffset);
            lastPositionOffset = Vector3.zero;
            lastRotationOffset = Quaternion.identity;
        }

        private void ApplyHandPose(Vector3 wristEuler, float fingerCurl, float thumbCurl)
        {
            if (!rigResolved) return;

            if (rightWrist != null && binds.TryGetValue(rightWrist, out Quaternion wristBind))
                rightWrist.localRotation = rightWrist.localRotation * Quaternion.Euler(wristEuler);
            if (rightPalm != null && binds.TryGetValue(rightPalm, out Quaternion palmBind))
                rightPalm.localRotation = rightPalm.localRotation * Quaternion.Euler(0f, fingerCurl * -.08f, fingerCurl * .10f);

            foreach (Transform bone in rightFingers)
            {
                if (bone == null) continue;
                bool thumb = bone == rightThumb1 || bone == rightThumb2 || bone.name.IndexOf("thumb", StringComparison.OrdinalIgnoreCase) >= 0;
                float curl = thumb ? thumbCurl : fingerCurl;
                bone.localRotation = bone.localRotation * Quaternion.Euler(curl, 0f, curl * .08f);
            }
        }

        private void ResolveReferences()
        {
            if (player == null) player = FindFirstObjectByType<PlayerMotor>(FindObjectsInactive.Include);
            if (flashlight == null) flashlight = FindFirstObjectByType<FlashlightController>(FindObjectsInactive.Include);
            if (viewmodel == null) viewmodel = FindFirstObjectByType<ViewmodelMotionController>(FindObjectsInactive.Include);
            if (flashlightRoot == null && flashlight != null) flashlightRoot = flashlight.transform;
        }

        private void ResolveRig()
        {
            if (viewmodel == null) return;
            Transform[] transforms = viewmodel.GetComponentsInChildren<Transform>(true);
            rightFingers.Clear();
            binds.Clear();
            foreach (Transform bone in transforms)
            {
                string n = bone.name;
                if (n == "R_wrist") rightWrist = bone;
                else if (n == "R_palm") rightPalm = bone;
                else if (n == "R_thumb1") rightThumb1 = bone;
                else if (n == "R_thumb2") rightThumb2 = bone;

                if (IsRightFinger(n))
                {
                    rightFingers.Add(bone);
                    binds[bone] = bone.localRotation;
                }
            }
            if (rightWrist != null) binds[rightWrist] = rightWrist.localRotation;
            if (rightPalm != null) binds[rightPalm] = rightPalm.localRotation;
            rigResolved = rightWrist != null && rightFingers.Count > 0;
        }

        private static bool IsRightFinger(string name)
        {
            if (!name.StartsWith("R_", StringComparison.Ordinal)) return false;
            return name.IndexOf("thumb", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("point", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("middle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("ring", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("pink", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float SmoothPulse(float p, float inStart, float inEnd, float outStart, float outEnd)
        {
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inStart, inEnd, p));
            float fall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(outStart, outEnd, p));
            return rise * fall;
        }

        private void ScheduleNormal() => nextNormalAt = Time.unscaledTime + UnityEngine.Random.Range(normalInterval.x, normalInterval.y);
        private void ScheduleRare() => nextRareAt = Time.unscaledTime + UnityEngine.Random.Range(rareInterval.x, rareInterval.y);
    }
}
