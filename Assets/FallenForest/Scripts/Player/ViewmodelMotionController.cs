using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FallenForest.Player
{
    /// <summary>
    /// Physical first-person presentation for the supplied rigged arms and flashlight.
    /// Camera turns lead the hands, the real Light lags behind, named wrist/finger bones are posed,
    /// and pickups/death cinematics can temporarily override the pose without teleporting meshes.
    /// </summary>
    public sealed class ViewmodelMotionController : MonoBehaviour
    {
        private enum InteractionMode { None, FlashlightPickup, DocumentPickup }

        [SerializeField] private PlayerMotor player;
        [SerializeField] private Transform armsRoot;
        [SerializeField] private Transform flashlightVisualRoot;
        [SerializeField] private Transform gameplayFlashlightRoot;

        [Header("Motion")]
        [SerializeField] private float idleBreathHeight = .006f;
        [SerializeField] private float walkBobHeight = .016f;
        [SerializeField] private float runBobHeight = .026f;
        [SerializeField] private float walkBobFrequency = 7.4f;
        [SerializeField] private float runBobFrequency = 10.6f;
        [SerializeField] private float maxLookLagDegrees = 5.2f;
        [SerializeField] private float lookLagResponse = 10.5f;
        [SerializeField] private float flashlightCatchup = 9.2f;

        private Vector3 armsBasePosition;
        private Quaternion armsBaseRotation;
        private Vector3 flashlightVisualBasePosition;
        private Quaternion flashlightVisualBaseRotation;
        private Vector3 gameplayFlashlightBasePosition;
        private Quaternion gameplayFlashlightBaseRotation;
        private Quaternion previousCameraRotation;
        private Vector2 lookLag;
        private float phase;
        private float idleVariantPhase;
        private float nextIdleVariantAt;
        private bool captured;

        private bool cinematicPose;
        private Vector3 cinematicArmsPosition;
        private Vector3 cinematicArmsEuler;
        private Vector3 cinematicFlashlightPosition;
        private Vector3 cinematicFlashlightEuler;
        private float cinematicBlend;
        private float cinematicBlendTarget;
        private float cinematicBlendSpeed = 12f;

        private InteractionMode interactionMode;
        private float interactionProgress;
        private readonly Dictionary<Transform, Quaternion> handBindRotations = new();
        private Transform leftWrist, rightWrist, leftPalm, rightPalm;
        private readonly List<Transform> leftFingerBones = new();
        private readonly List<Transform> rightFingerBones = new();

        private void Awake()
        {
            ResolveReferences();
            CaptureBindPose();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CaptureBindPose();
            previousCameraRotation = transform.parent != null ? transform.parent.rotation : transform.rotation;
            ScheduleIdleVariant();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (!captured) CaptureBindPose();
            if (player == null) return;

            float dt = Mathf.Max(Time.unscaledDeltaTime, .0001f);
            float speed01 = player.NormalizedSpeed;
            float bobAmplitude = Mathf.Lerp(0f, player.IsFinalRun ? runBobHeight : walkBobHeight, speed01);
            float frequency = Mathf.Lerp(1.2f, player.IsFinalRun ? runBobFrequency : walkBobFrequency, speed01);
            phase += dt * frequency;

            Quaternion cameraRotation = transform.parent != null ? transform.parent.rotation : transform.rotation;
            Vector3 previousEuler = previousCameraRotation.eulerAngles;
            Vector3 currentEuler = cameraRotation.eulerAngles;
            Vector2 rawLag = new(
                Mathf.Clamp(Mathf.DeltaAngle(previousEuler.x, currentEuler.x), -maxLookLagDegrees, maxLookLagDegrees),
                Mathf.Clamp(Mathf.DeltaAngle(previousEuler.y, currentEuler.y), -maxLookLagDegrees, maxLookLagDegrees));
            previousCameraRotation = cameraRotation;
            lookLag = Vector2.Lerp(lookLag, rawLag, 1f - Mathf.Exp(-lookLagResponse * dt));

            float breath = Mathf.Sin(Time.unscaledTime * 1.45f) * idleBreathHeight;
            float vertical = Mathf.Abs(Mathf.Sin(phase)) * -bobAmplitude + breath;
            float horizontal = Mathf.Cos(phase * .5f) * bobAmplitude * .45f * speed01;
            float roll = Mathf.Sin(phase * .5f) * 1.15f * speed01 + player.LateralSpeed * -1.6f;

            float idleGrip = EvaluateIdleVariant(dt, speed01);
            Vector3 runLower = player.IsFinalRun ? new Vector3(.006f, -.028f, -.018f) * speed01 : Vector3.zero;
            Quaternion armsLag = Quaternion.Euler(-lookLag.x * .34f, -lookLag.y * .50f, lookLag.y * .16f);

            cinematicBlend = Mathf.MoveTowards(
                cinematicBlend,
                cinematicBlendTarget,
                Mathf.Max(.01f, cinematicBlendSpeed) * dt);

            GetInteractionRootPose(out Vector3 interactionPos, out Vector3 interactionEuler, out Vector3 interactionFlashPos, out Vector3 interactionFlashEuler);

            Vector3 armsGameplayPos = armsBasePosition + new Vector3(horizontal, vertical, Mathf.Sin(phase) * bobAmplitude * .12f) + runLower + interactionPos;
            Quaternion armsGameplayRot = armsBaseRotation * armsLag * Quaternion.Euler(
                speed01 * 1.5f + idleGrip * 1.2f,
                idleGrip * -1.1f,
                roll + idleGrip * 2.1f) * Quaternion.Euler(interactionEuler);
            Vector3 armsCinematicPos = armsBasePosition + cinematicArmsPosition;
            Quaternion armsCinematicRot = armsBaseRotation * Quaternion.Euler(cinematicArmsEuler);

            if (armsRoot != null)
            {
                Vector3 targetPosition = Vector3.Lerp(armsGameplayPos, armsCinematicPos, cinematicBlend);
                Quaternion targetRotation = Quaternion.Slerp(armsGameplayRot, armsCinematicRot, cinematicBlend);
                armsRoot.localPosition = Vector3.Lerp(armsRoot.localPosition, targetPosition, 1f - Mathf.Exp(-15f * dt));
                armsRoot.localRotation = Quaternion.Slerp(armsRoot.localRotation, targetRotation, 1f - Mathf.Exp(-15f * dt));
            }

            if (gameplayFlashlightRoot != null)
            {
                Vector3 gameplayPosition = gameplayFlashlightBasePosition + new Vector3(horizontal * .30f, vertical * .35f, 0f) + runLower * .45f + interactionFlashPos;
                Quaternion gameplayRotation = gameplayFlashlightBaseRotation * Quaternion.Euler(
                    -lookLag.x * .46f,
                    -lookLag.y * .82f,
                    roll * .40f + lookLag.y * .20f) * Quaternion.Euler(interactionFlashEuler);
                Vector3 cinematicPosition = gameplayFlashlightBasePosition + cinematicFlashlightPosition;
                Quaternion cinematicRotation = gameplayFlashlightBaseRotation * Quaternion.Euler(cinematicFlashlightEuler);
                gameplayFlashlightRoot.localPosition = Vector3.Lerp(
                    gameplayFlashlightRoot.localPosition,
                    Vector3.Lerp(gameplayPosition, cinematicPosition, cinematicBlend),
                    1f - Mathf.Exp(-flashlightCatchup * dt));
                gameplayFlashlightRoot.localRotation = Quaternion.Slerp(
                    gameplayFlashlightRoot.localRotation,
                    Quaternion.Slerp(gameplayRotation, cinematicRotation, cinematicBlend),
                    1f - Mathf.Exp(-flashlightCatchup * dt));
            }

            if (flashlightVisualRoot != null)
            {
                float micro = Mathf.Sin(Time.unscaledTime * 2.1f + .6f) * .003f * (1f - speed01);
                Vector3 targetPosition = flashlightVisualBasePosition + new Vector3(0f, micro, 0f);
                Quaternion targetRotation = flashlightVisualBaseRotation * Quaternion.Euler(idleGrip * .6f, 0f, idleGrip * -.8f);
                flashlightVisualRoot.localPosition = Vector3.Lerp(flashlightVisualRoot.localPosition, targetPosition, 1f - Mathf.Exp(-18f * dt));
                flashlightVisualRoot.localRotation = Quaternion.Slerp(flashlightVisualRoot.localRotation, targetRotation, 1f - Mathf.Exp(-18f * dt));
            }

            ApplyHandSkeleton(speed01, idleGrip);
        }

        public IEnumerator PlayFlashlightPickup(float duration = .72f)
        {
            if (interactionMode != InteractionMode.None) yield break;
            interactionMode = InteractionMode.FlashlightPickup;
            interactionProgress = 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                interactionProgress = Mathf.Clamp01(elapsed / Mathf.Max(.05f, duration));
                yield return null;
            }
            interactionProgress = 1f;
            interactionMode = InteractionMode.None;
            interactionProgress = 0f;
        }

        public IEnumerator PlayDocumentPickup(float duration = .82f)
        {
            if (interactionMode != InteractionMode.None) yield break;
            interactionMode = InteractionMode.DocumentPickup;
            interactionProgress = 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                interactionProgress = Mathf.Clamp01(elapsed / Mathf.Max(.05f, duration));
                yield return null;
            }
            interactionProgress = 1f;
            interactionMode = InteractionMode.None;
            interactionProgress = 0f;
        }

        public void SetCinematicPose(
            Vector3 armsPosition,
            Vector3 armsEuler,
            Vector3 flashlightPosition,
            Vector3 flashlightEuler,
            float blendSpeed = 12f)
        {
            cinematicPose = true;
            cinematicArmsPosition = armsPosition;
            cinematicArmsEuler = armsEuler;
            cinematicFlashlightPosition = flashlightPosition;
            cinematicFlashlightEuler = flashlightEuler;
            cinematicBlendSpeed = Mathf.Max(.01f, blendSpeed);
            cinematicBlendTarget = 1f;
        }

        public void ClearCinematicPose(float blendSpeed = 10f)
        {
            cinematicPose = false;
            cinematicBlendSpeed = Mathf.Max(.01f, blendSpeed);
            cinematicBlendTarget = 0f;
        }

        public bool IsInCinematicPose => cinematicPose || cinematicBlend > .001f;

        private void GetInteractionRootPose(out Vector3 armsPos, out Vector3 armsEuler, out Vector3 flashlightPos, out Vector3 flashlightEuler)
        {
            armsPos = Vector3.zero;
            armsEuler = Vector3.zero;
            flashlightPos = Vector3.zero;
            flashlightEuler = Vector3.zero;
            if (interactionMode == InteractionMode.None) return;

            float p = Mathf.Clamp01(interactionProgress);
            float reach = Mathf.Sin(p * Mathf.PI);
            float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.58f, 1f, p));

            if (interactionMode == InteractionMode.FlashlightPickup)
            {
                armsPos = new Vector3(.035f, -.055f, .17f) * reach + new Vector3(.018f, -.01f, .012f) * settle;
                armsEuler = new Vector3(15f, -9f, 13f) * reach + new Vector3(-3f, 2f, -4f) * settle;
                flashlightPos = new Vector3(.04f, -.02f, .035f) * settle;
                flashlightEuler = new Vector3(-7f, 6f, -9f) * settle;
            }
            else
            {
                armsPos = new Vector3(-.055f, -.025f, .205f) * reach;
                armsEuler = new Vector3(9f, 13f, -17f) * reach;
                flashlightPos = new Vector3(.028f, -.045f, -.035f) * reach;
                flashlightEuler = new Vector3(10f, 17f, -13f) * reach;
            }
        }

        private void ApplyHandSkeleton(float speed01, float idleGrip)
        {
            if (handBindRotations.Count == 0) return;
            foreach (KeyValuePair<Transform, Quaternion> pair in handBindRotations)
                if (pair.Key != null) pair.Key.localRotation = pair.Value;

            FlashlightController controller = gameplayFlashlightRoot != null
                ? gameplayFlashlightRoot.GetComponent<FlashlightController>()
                : null;
            bool holdingFlashlight = controller != null && controller.Acquired;

            if (rightWrist != null && handBindRotations.TryGetValue(rightWrist, out Quaternion rightWristBind))
            {
                Vector3 grip = holdingFlashlight ? new Vector3(-4.5f, 5f, 7f) : Vector3.zero;
                if (interactionMode == InteractionMode.FlashlightPickup)
                    grip += new Vector3(14f, -12f, 18f) * Mathf.Sin(interactionProgress * Mathf.PI);
                rightWrist.localRotation = rightWristBind * Quaternion.Euler(grip);
            }

            if (leftWrist != null && handBindRotations.TryGetValue(leftWrist, out Quaternion leftWristBind))
            {
                Vector3 pose = new(idleGrip * 1.3f, 0f, idleGrip * -1.8f);
                if (interactionMode == InteractionMode.DocumentPickup)
                    pose += new Vector3(19f, 10f, -24f) * Mathf.Sin(interactionProgress * Mathf.PI);
                leftWrist.localRotation = leftWristBind * Quaternion.Euler(pose);
            }

            float cinematicBrace = cinematicBlend * -7f;
            float rightCurl = holdingFlashlight ? 12f : 2f;
            float leftCurl = 1.5f + idleGrip * 1.5f;

            if (interactionMode == InteractionMode.FlashlightPickup)
            {
                float grip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, .72f, interactionProgress));
                rightCurl = Mathf.Lerp(-8f, 18f, grip);
            }
            if (interactionMode == InteractionMode.DocumentPickup)
            {
                float grip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.40f, .72f, interactionProgress));
                leftCurl = Mathf.Lerp(-9f, 14f, grip);
            }

            ApplyFingerCurl(rightFingerBones, rightCurl + cinematicBrace, true);
            ApplyFingerCurl(leftFingerBones, leftCurl + cinematicBrace, false);

            if (rightPalm != null && handBindRotations.TryGetValue(rightPalm, out Quaternion rp))
                rightPalm.localRotation = rp * Quaternion.Euler(holdingFlashlight ? new Vector3(0f, -2f, 3f) : Vector3.zero);
            if (leftPalm != null && handBindRotations.TryGetValue(leftPalm, out Quaternion lp))
                leftPalm.localRotation = lp * Quaternion.Euler(new Vector3(0f, idleGrip, -idleGrip));
        }

        private void ApplyFingerCurl(List<Transform> bones, float curl, bool right)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                Transform bone = bones[i];
                if (bone == null || !handBindRotations.TryGetValue(bone, out Quaternion bind)) continue;
                string lower = bone.name.ToLowerInvariant();
                float multiplier = lower.Contains("thumb") ? .55f : 1f;
                float side = right ? 1f : -1f;
                bone.localRotation = bind * Quaternion.Euler(curl * multiplier, 0f, side * curl * .10f);
            }
        }

        private float EvaluateIdleVariant(float dt, float speed01)
        {
            if (speed01 > .08f)
            {
                idleVariantPhase = 0f;
                return 0f;
            }

            if (Time.unscaledTime >= nextIdleVariantAt && idleVariantPhase <= 0f)
                idleVariantPhase = .001f;

            if (idleVariantPhase <= 0f) return 0f;
            idleVariantPhase += dt / 1.15f;
            float p = Mathf.Clamp01(idleVariantPhase);
            float value = Mathf.Sin(p * Mathf.PI) * Mathf.Sin(p * Mathf.PI * 2f) * .75f;
            if (p >= 1f)
            {
                idleVariantPhase = 0f;
                ScheduleIdleVariant();
            }
            return value;
        }

        private void ScheduleIdleVariant()
        {
            nextIdleVariantAt = Time.unscaledTime + Random.Range(5.5f, 11f);
        }

        private void ResolveReferences()
        {
            if (player == null)
                player = FindFirstObjectByType<PlayerMotor>(FindObjectsInactive.Include);
            if (armsRoot == null)
                armsRoot = FindNamedChild("FPSArms_Final");
            if (flashlightVisualRoot == null)
                flashlightVisualRoot = FindNamedChild("FlashlightVisual_Final");
            if (gameplayFlashlightRoot == null)
            {
                FlashlightController controller = FindFirstObjectByType<FlashlightController>(FindObjectsInactive.Include);
                if (controller != null) gameplayFlashlightRoot = controller.transform;
            }
        }

        private Transform FindNamedChild(string objectName)
        {
            foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.name == objectName)
                    return candidate;
            return null;
        }

        private void CaptureBindPose()
        {
            if (armsRoot != null)
            {
                armsBasePosition = armsRoot.localPosition;
                armsBaseRotation = armsRoot.localRotation;
                CaptureHandRig();
            }
            if (flashlightVisualRoot != null)
            {
                flashlightVisualBasePosition = flashlightVisualRoot.localPosition;
                flashlightVisualBaseRotation = flashlightVisualRoot.localRotation;
            }
            if (gameplayFlashlightRoot != null)
            {
                gameplayFlashlightBasePosition = gameplayFlashlightRoot.localPosition;
                gameplayFlashlightBaseRotation = gameplayFlashlightRoot.localRotation;
            }
            captured = armsRoot != null || gameplayFlashlightRoot != null || flashlightVisualRoot != null;
        }

        private void CaptureHandRig()
        {
            handBindRotations.Clear();
            leftFingerBones.Clear();
            rightFingerBones.Clear();
            leftWrist = rightWrist = leftPalm = rightPalm = null;

            foreach (Transform bone in armsRoot.GetComponentsInChildren<Transform>(true))
            {
                string name = bone.name;
                if (name == "L_wrist") leftWrist = bone;
                else if (name == "R_wrist") rightWrist = bone;
                else if (name == "L_palm") leftPalm = bone;
                else if (name == "R_palm") rightPalm = bone;

                bool leftFinger = IsFingerBone(name, "L_");
                bool rightFinger = IsFingerBone(name, "R_");
                if (leftFinger) leftFingerBones.Add(bone);
                if (rightFinger) rightFingerBones.Add(bone);
                if (leftFinger || rightFinger || name == "L_wrist" || name == "R_wrist" || name == "L_palm" || name == "R_palm")
                    handBindRotations[bone] = bone.localRotation;
            }
        }

        private static bool IsFingerBone(string name, string side)
        {
            if (!name.StartsWith(side, System.StringComparison.Ordinal)) return false;
            return name.IndexOf("thumb", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("point", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("middle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("ring", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("pink", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
