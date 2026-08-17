using System;
using UnityEngine;

namespace FallenForest.Monsters
{
    /// <summary>
    /// Skeletal fallback for the supplied Locust rig. It provides five visibly different hiding
    /// silhouettes plus a front-heavy, long-arm support gait for Rage/chase when no authored
    /// RuntimeAnimatorController is available.
    /// </summary>
    public sealed class LocustProceduralAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private LocustAI ai;
        [SerializeField] private float movingThreshold = .08f;
        [SerializeField] private float fullStrideSpeed = 5.5f;
        [SerializeField] private float walkCadence = 5.4f;
        [SerializeField] private float chaseCadence = 8.8f;
        [SerializeField] private float maxArmSwing = 34f;
        [SerializeField] private float maxLegSwing = 20f;
        [SerializeField] private float idleBreathDegrees = 1.3f;

        private Transform shoulderL, shoulderR, upperArmL, upperArmR, forearmL, forearmR;
        private Transform thighL, thighR, shinL, shinR;
        private Quaternion shoulderLBind, shoulderRBind, upperArmLBind, upperArmRBind;
        private Quaternion forearmLBind, forearmRBind, thighLBind, thighRBind, shinLBind, shinRBind;
        private Vector3 lastWorldPosition;
        private float phase;
        private bool ready;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (ai == null) ai = GetComponent<LocustAI>();

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                enabled = false;
                return;
            }

            if (animator != null) animator.enabled = false;
            ResolveBones();
            CaptureBindPose();
            lastWorldPosition = transform.position;
        }

        private void OnEnable() => lastWorldPosition = transform.position;

        private void LateUpdate()
        {
            if (!ready) return;

            float dt = Mathf.Max(Time.deltaTime, .0001f);
            float worldSpeed = Vector3.ProjectOnPlane(transform.position - lastWorldPosition, Vector3.up).magnitude / dt;
            lastWorldPosition = transform.position;

            bool rage = ai != null && ai.IsRaging;
            float move01 = Mathf.InverseLerp(movingThreshold, fullStrideSpeed, worldSpeed);
            if (rage) move01 = Mathf.Max(move01, .42f);
            float cadence = Mathf.Lerp(walkCadence, rage ? chaseCadence * 1.08f : chaseCadence, move01);
            phase += dt * Mathf.Lerp(.9f, cadence, Mathf.Max(.08f, move01));

            float cycle = Mathf.Sin(phase);
            float opposite = -cycle;
            float stride = Mathf.Lerp(2.2f, maxArmSwing * (rage ? 1.12f : 1f), move01);
            float legStride = Mathf.Lerp(1.2f, maxLegSwing, move01);
            float support = Mathf.Abs(Mathf.Sin(phase)) * move01;
            float breath = Mathf.Sin(Time.unscaledTime * 1.2f) * idleBreathDegrees * (1f - move01);
            float forwardBrace = rage ? 13f : 0f;

            Vector3 sl = new(cycle * stride * .25f + breath + forwardBrace * .25f, 0f, -cycle * stride * .13f);
            Vector3 sr = new(opposite * stride * .25f - breath + forwardBrace * .25f, 0f, -opposite * stride * .13f);
            Vector3 ual = new(cycle * stride + forwardBrace, rage ? -5f : 0f, -support * (rage ? 11f : 7f));
            Vector3 uar = new(opposite * stride + forwardBrace, rage ? 5f : 0f, support * (rage ? 11f : 7f));
            Vector3 fal = new(-support * (rage ? 36f : 24f) - cycle * stride * .22f, 0f, rage ? -5f : 0f);
            Vector3 far = new(-support * (rage ? 36f : 24f) - opposite * stride * .22f, 0f, rage ? 5f : 0f);

            if (move01 < .18f && ai != null && !rage)
                ApplyHideOffsets(ai.HideVariant, ref sl, ref sr, ref ual, ref uar, ref fal, ref far);

            Apply(shoulderL, shoulderLBind, Quaternion.Euler(sl));
            Apply(shoulderR, shoulderRBind, Quaternion.Euler(sr));
            Apply(upperArmL, upperArmLBind, Quaternion.Euler(ual));
            Apply(upperArmR, upperArmRBind, Quaternion.Euler(uar));
            Apply(forearmL, forearmLBind, Quaternion.Euler(fal));
            Apply(forearmR, forearmRBind, Quaternion.Euler(far));

            Apply(thighL, thighLBind, Quaternion.Euler(opposite * legStride + forwardBrace * .18f, 0f, rage ? -3f : 0f));
            Apply(thighR, thighRBind, Quaternion.Euler(cycle * legStride + forwardBrace * .18f, 0f, rage ? 3f : 0f));
            Apply(shinL, shinLBind, Quaternion.Euler(Mathf.Max(0f, cycle) * -legStride * .75f, 0f, 0f));
            Apply(shinR, shinRBind, Quaternion.Euler(Mathf.Max(0f, opposite) * -legStride * .75f, 0f, 0f));
        }

        private static void ApplyHideOffsets(
            int variant,
            ref Vector3 sl,
            ref Vector3 sr,
            ref Vector3 ual,
            ref Vector3 uar,
            ref Vector3 fal,
            ref Vector3 far)
        {
            switch (Mathf.Clamp(variant, 0, 4))
            {
                case 0: // Far A: narrow silhouette, one forearm wrapped in.
                    sl += new Vector3(-8f, 8f, -12f); sr += new Vector3(5f, -3f, 7f);
                    ual += new Vector3(-18f, 8f, -18f); uar += new Vector3(9f, -2f, 5f);
                    fal += new Vector3(-28f, 0f, -12f); far += new Vector3(-6f, 0f, 6f);
                    break;
                case 1: // Far B: opposite side and higher exposed shoulder.
                    sl += new Vector3(7f, -4f, -5f); sr += new Vector3(-12f, -10f, 15f);
                    ual += new Vector3(8f, 2f, -5f); uar += new Vector3(-24f, -8f, 21f);
                    fal += new Vector3(-8f, 0f, -4f); far += new Vector3(-32f, 0f, 15f);
                    break;
                case 2: // Medium: low crouched observation with both arms braced.
                    sl += new Vector3(11f, 0f, -9f); sr += new Vector3(11f, 0f, 9f);
                    ual += new Vector3(24f, -5f, -15f); uar += new Vector3(24f, 5f, 15f);
                    fal += new Vector3(-38f, 0f, -6f); far += new Vector3(-38f, 0f, 6f);
                    break;
                case 3: // Close A: one long arm planted, the other tucked back.
                    sl += new Vector3(15f, 10f, -16f); sr += new Vector3(-6f, -8f, 5f);
                    ual += new Vector3(37f, 7f, -25f); uar += new Vector3(-12f, -7f, 9f);
                    fal += new Vector3(-48f, 0f, -12f); far += new Vector3(-12f, 0f, 8f);
                    break;
                default: // Close B: mirrored, taller partial exposure.
                    sl += new Vector3(-4f, 7f, -4f); sr += new Vector3(18f, -12f, 18f);
                    ual += new Vector3(-10f, 6f, -8f); uar += new Vector3(40f, -8f, 27f);
                    fal += new Vector3(-14f, 0f, -6f); far += new Vector3(-50f, 0f, 14f);
                    break;
            }
        }

        private void ResolveBones()
        {
            shoulderL = FindBone("shoulder.L"); shoulderR = FindBone("shoulder.R");
            upperArmL = FindBone("upper_arm.L"); upperArmR = FindBone("upper_arm.R");
            forearmL = FindBone("forearm.L"); forearmR = FindBone("forearm.R");
            thighL = FindBone("thigh.L"); thighR = FindBone("thigh.R");
            shinL = FindBone("shin.L"); shinR = FindBone("shin.R");
        }

        private Transform FindBone(string exactName)
        {
            foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
                if (string.Equals(candidate.name, exactName, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            return null;
        }

        private void CaptureBindPose()
        {
            shoulderLBind = Bind(shoulderL); shoulderRBind = Bind(shoulderR);
            upperArmLBind = Bind(upperArmL); upperArmRBind = Bind(upperArmR);
            forearmLBind = Bind(forearmL); forearmRBind = Bind(forearmR);
            thighLBind = Bind(thighL); thighRBind = Bind(thighR);
            shinLBind = Bind(shinL); shinRBind = Bind(shinR);
            ready = upperArmL != null && upperArmR != null && forearmL != null && forearmR != null;
            if (!ready)
                Debug.LogWarning("Fallen Forest: Locust procedural animation could not resolve the expected arm bones.", this);
        }

        private static Quaternion Bind(Transform t) => t != null ? t.localRotation : Quaternion.identity;
        private static void Apply(Transform bone, Quaternion bind, Quaternion offset)
        {
            if (bone != null) bone.localRotation = bind * offset;
        }
    }
}
