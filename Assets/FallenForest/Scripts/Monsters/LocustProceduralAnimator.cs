using System;
using UnityEngine;

namespace FallenForest.Monsters
{
    /// <summary>
    /// Procedural fallback for the supplied Locust FBX. The source file contains an idle take but
    /// no complete gameplay controller matching LocustAI's Peek/Run/Retreat/Jumpscare triggers.
    /// This driver animates the known arm/leg bones from measured movement and automatically gets
    /// out of the way if a real RuntimeAnimatorController is assigned later.
    /// </summary>
    public sealed class LocustProceduralAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float movingThreshold = .08f;
        [SerializeField] private float fullStrideSpeed = 5.5f;
        [SerializeField] private float walkCadence = 5.4f;
        [SerializeField] private float chaseCadence = 8.2f;
        [SerializeField] private float maxArmSwing = 31f;
        [SerializeField] private float maxLegSwing = 20f;
        [SerializeField] private float idleBreathDegrees = 1.3f;

        private Transform shoulderL;
        private Transform shoulderR;
        private Transform upperArmL;
        private Transform upperArmR;
        private Transform forearmL;
        private Transform forearmR;
        private Transform thighL;
        private Transform thighR;
        private Transform shinL;
        private Transform shinR;

        private Quaternion shoulderLBind;
        private Quaternion shoulderRBind;
        private Quaternion upperArmLBind;
        private Quaternion upperArmRBind;
        private Quaternion forearmLBind;
        private Quaternion forearmRBind;
        private Quaternion thighLBind;
        private Quaternion thighRBind;
        private Quaternion shinLBind;
        private Quaternion shinRBind;

        private Vector3 lastWorldPosition;
        private float phase;
        private bool ready;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);

            // A future authored controller always wins over this fallback.
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                enabled = false;
                return;
            }

            if (animator != null)
                animator.enabled = false;

            ResolveBones();
            CaptureBindPose();
            lastWorldPosition = transform.position;
        }

        private void OnEnable()
        {
            lastWorldPosition = transform.position;
        }

        private void LateUpdate()
        {
            if (!ready) return;

            float dt = Mathf.Max(Time.deltaTime, .0001f);
            float worldSpeed = Vector3.ProjectOnPlane(transform.position - lastWorldPosition, Vector3.up).magnitude / dt;
            lastWorldPosition = transform.position;

            float move01 = Mathf.InverseLerp(movingThreshold, fullStrideSpeed, worldSpeed);
            float cadence = Mathf.Lerp(walkCadence, chaseCadence, move01);
            phase += dt * Mathf.Lerp(.9f, cadence, Mathf.Max(.08f, move01));

            float cycle = Mathf.Sin(phase);
            float opposite = -cycle;
            float stride = Mathf.Lerp(2.2f, maxArmSwing, move01);
            float legStride = Mathf.Lerp(1.2f, maxLegSwing, move01);
            float support = Mathf.Abs(Mathf.Sin(phase)) * move01;
            float breath = Mathf.Sin(Time.unscaledTime * 1.2f) * idleBreathDegrees * (1f - move01);

            Apply(shoulderL, shoulderLBind, Quaternion.Euler(cycle * stride * .25f + breath, 0f, -cycle * stride * .13f));
            Apply(shoulderR, shoulderRBind, Quaternion.Euler(opposite * stride * .25f - breath, 0f, -opposite * stride * .13f));

            // Long arms lead the movement so the creature reads as arm-supported rather than humanoid.
            Apply(upperArmL, upperArmLBind, Quaternion.Euler(cycle * stride, 0f, -support * 7f));
            Apply(upperArmR, upperArmRBind, Quaternion.Euler(opposite * stride, 0f, support * 7f));
            Apply(forearmL, forearmLBind, Quaternion.Euler(-support * 24f - cycle * stride * .22f, 0f, 0f));
            Apply(forearmR, forearmRBind, Quaternion.Euler(-support * 24f - opposite * stride * .22f, 0f, 0f));

            // Legs move with smaller amplitude so the silhouette stays front-heavy and unnatural.
            Apply(thighL, thighLBind, Quaternion.Euler(opposite * legStride, 0f, 0f));
            Apply(thighR, thighRBind, Quaternion.Euler(cycle * legStride, 0f, 0f));
            Apply(shinL, shinLBind, Quaternion.Euler(Mathf.Max(0f, cycle) * -legStride * .75f, 0f, 0f));
            Apply(shinR, shinRBind, Quaternion.Euler(Mathf.Max(0f, opposite) * -legStride * .75f, 0f, 0f));
        }

        private void ResolveBones()
        {
            shoulderL = FindBone("shoulder.L");
            shoulderR = FindBone("shoulder.R");
            upperArmL = FindBone("upper_arm.L");
            upperArmR = FindBone("upper_arm.R");
            forearmL = FindBone("forearm.L");
            forearmR = FindBone("forearm.R");
            thighL = FindBone("thigh.L");
            thighR = FindBone("thigh.R");
            shinL = FindBone("shin.L");
            shinR = FindBone("shin.R");
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
            shoulderLBind = Bind(shoulderL);
            shoulderRBind = Bind(shoulderR);
            upperArmLBind = Bind(upperArmL);
            upperArmRBind = Bind(upperArmR);
            forearmLBind = Bind(forearmL);
            forearmRBind = Bind(forearmR);
            thighLBind = Bind(thighL);
            thighRBind = Bind(thighR);
            shinLBind = Bind(shinL);
            shinRBind = Bind(shinR);

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
