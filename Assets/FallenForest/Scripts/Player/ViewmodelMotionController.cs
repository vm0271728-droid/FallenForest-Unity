using UnityEngine;

namespace FallenForest.Player
{
    /// <summary>
    /// Mobile-friendly physical first-person presentation for the supplied arms and flashlight.
    /// Camera turns lead the hands, the real flashlight Light lags behind, movement is asymmetric,
    /// and cinematics can temporarily override the pose without teleporting the viewmodel.
    /// </summary>
    public sealed class ViewmodelMotionController : MonoBehaviour
    {
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

            Vector3 armsGameplayPos = armsBasePosition + new Vector3(horizontal, vertical, Mathf.Sin(phase) * bobAmplitude * .12f) + runLower;
            Quaternion armsGameplayRot = armsBaseRotation * armsLag * Quaternion.Euler(speed01 * 1.5f + idleGrip * 1.2f, idleGrip * -1.1f, roll + idleGrip * 2.1f);
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
                Vector3 gameplayPosition = gameplayFlashlightBasePosition + new Vector3(horizontal * .30f, vertical * .35f, 0f) + runLower * .45f;
                Quaternion gameplayRotation = gameplayFlashlightBaseRotation * Quaternion.Euler(
                    -lookLag.x * .46f,
                    -lookLag.y * .82f,
                    roll * .40f + lookLag.y * .20f);
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
    }
}
