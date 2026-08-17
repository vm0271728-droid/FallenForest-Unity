using UnityEngine;

namespace FallenForest.Player
{
    /// <summary>
    /// Lightweight mobile-friendly first-person motion for the exact user arms/flashlight models.
    /// It adds breathing, walking/final-run bob and camera-turn inertia without depending on
    /// imported animation clips or a heavyweight AnimatorController.
    /// </summary>
    public sealed class ViewmodelMotionController : MonoBehaviour
    {
        [SerializeField] private PlayerMotor player;
        [SerializeField] private Transform armsRoot;
        [SerializeField] private Transform flashlightVisualRoot;

        [Header("Motion")]
        [SerializeField] private float idleBreathHeight = .006f;
        [SerializeField] private float walkBobHeight = .016f;
        [SerializeField] private float runBobHeight = .026f;
        [SerializeField] private float walkBobFrequency = 7.4f;
        [SerializeField] private float runBobFrequency = 10.6f;
        [SerializeField] private float maxLookLagDegrees = 4.2f;
        [SerializeField] private float lookLagResponse = 12f;

        private Vector3 armsBasePosition;
        private Quaternion armsBaseRotation;
        private Vector3 flashlightBasePosition;
        private Quaternion flashlightBaseRotation;
        private Quaternion previousCameraRotation;
        private Vector2 lookLag;
        private float phase;
        private bool captured;

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
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (!captured) CaptureBindPose();
            if (player == null) return;

            float dt = Mathf.Max(Time.deltaTime, .0001f);
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

            Quaternion lagRotation = Quaternion.Euler(-lookLag.x * .34f, -lookLag.y * .50f, lookLag.y * .16f);

            if (armsRoot != null)
            {
                Vector3 targetPosition = armsBasePosition + new Vector3(horizontal, vertical, Mathf.Sin(phase) * bobAmplitude * .12f);
                Quaternion targetRotation = armsBaseRotation * lagRotation * Quaternion.Euler(speed01 * 1.5f, 0f, roll);
                armsRoot.localPosition = Vector3.Lerp(armsRoot.localPosition, targetPosition, 1f - Mathf.Exp(-15f * dt));
                armsRoot.localRotation = Quaternion.Slerp(armsRoot.localRotation, targetRotation, 1f - Mathf.Exp(-15f * dt));
            }

            if (flashlightVisualRoot != null)
            {
                Vector3 targetPosition = flashlightBasePosition + new Vector3(horizontal * .45f, vertical * .62f, 0f);
                Quaternion targetRotation = flashlightBaseRotation * Quaternion.Euler(-lookLag.x * .20f, -lookLag.y * .31f, roll * .58f);
                flashlightVisualRoot.localPosition = Vector3.Lerp(flashlightVisualRoot.localPosition, targetPosition, 1f - Mathf.Exp(-18f * dt));
                flashlightVisualRoot.localRotation = Quaternion.Slerp(flashlightVisualRoot.localRotation, targetRotation, 1f - Mathf.Exp(-18f * dt));
            }
        }

        private void ResolveReferences()
        {
            if (player == null)
                player = FindFirstObjectByType<PlayerMotor>(FindObjectsInactive.Include);

            if (armsRoot == null)
                armsRoot = FindNamedChild("FPSArms_Final");

            if (flashlightVisualRoot == null)
                flashlightVisualRoot = FindNamedChild("FlashlightVisual_Final");
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
                flashlightBasePosition = flashlightVisualRoot.localPosition;
                flashlightBaseRotation = flashlightVisualRoot.localRotation;
            }

            captured = armsRoot != null || flashlightVisualRoot != null;
        }
    }
}
