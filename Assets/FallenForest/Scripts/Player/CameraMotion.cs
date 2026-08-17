using FallenForest.Core;
using FallenForest.Input;
using UnityEngine;

namespace FallenForest.Player
{
    public sealed class CameraMotion : MonoBehaviour
    {
        [SerializeField] private TouchLookInput lookInput;
        [SerializeField] private PlayerMotor player;
        [SerializeField] private Transform yawRoot;
        [SerializeField] private Transform pitchRoot;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float pixelsToDegrees = 0.095f;
        [SerializeField] private float maxPitch = 82f;
        [SerializeField] private float turnSmoothing = 24f;
        [SerializeField] private float maxTurnRoll = 2.4f;
        [SerializeField] private float strafeRoll = 1.35f;
        [SerializeField] private float rollResponse = 8f;
        [SerializeField] private float walkBobFrequency = 7.6f;
        [SerializeField] private float walkBobVertical = 0.026f;
        [SerializeField] private float walkBobHorizontal = 0.018f;
        [SerializeField] private float runBobMultiplier = 1.72f;
        [SerializeField] private float idleBreathAmount = 0.0035f;
        [SerializeField] private float positionalSmoothing = 14f;

        private float yaw, pitch, targetRoll, roll, bobTime, idleTime, shakeAmount, shakeDecay;
        private Vector3 baseLocalPosition, shakePosition, cinematicPositionOffset, cinematicRotationOffset;
        private bool inputEnabled = true, cinematicFov;
        private float cinematicFovValue = 63f;

        private Transform forcedLookTarget;
        private Vector3 forcedLookWorldOffset;
        private float forcedLookResponse = 12f;

        public Camera TargetCamera => targetCamera;
        public bool HasForcedLookTarget => forcedLookTarget != null;

        private void Awake()
        {
            if (yawRoot == null) yawRoot = transform.parent != null ? transform.parent : transform;
            if (pitchRoot == null) pitchRoot = transform;
            if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>();
            baseLocalPosition = pitchRoot.localPosition;
            yaw = yawRoot.eulerAngles.y;
            pitch = NormalizeAngle(pitchRoot.localEulerAngles.x);
        }

        private void Start()
        {
            if (targetCamera != null) targetCamera.fieldOfView = GameSettings.DefaultFov;
        }

        private void LateUpdate()
        {
            float dt = Mathf.Max(Time.unscaledDeltaTime, .0001f);

            if (forcedLookTarget != null)
            {
                Vector3 origin = targetCamera != null ? targetCamera.transform.position : pitchRoot.position;
                Vector3 toTarget = forcedLookTarget.position + forcedLookWorldOffset - origin;
                if (toTarget.sqrMagnitude > .0001f)
                {
                    Quaternion wanted = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    float blend = 1f - Mathf.Exp(-Mathf.Max(.01f, forcedLookResponse) * dt);
                    yaw = Mathf.LerpAngle(yaw, wanted.eulerAngles.y, blend);
                    pitch = Mathf.Lerp(pitch, Mathf.Clamp(NormalizeAngle(wanted.eulerAngles.x), -maxPitch, maxPitch), blend);
                }
            }
            else
            {
                Vector2 look = inputEnabled && lookInput != null ? lookInput.ConsumeDelta() : Vector2.zero;
                float yawDelta = look.x * pixelsToDegrees * GameSettings.Sensitivity;
                float pitchDelta = look.y * pixelsToDegrees * GameSettings.Sensitivity;
                yaw += yawDelta;
                pitch = Mathf.Clamp(pitch - pitchDelta, -maxPitch, maxPitch);

                float turnRoll = Mathf.Clamp(-yawDelta * .22f, -maxTurnRoll, maxTurnRoll);
                float movementRoll = player != null ? -player.LateralSpeed * strafeRoll : 0f;
                targetRoll = Mathf.Clamp(turnRoll + movementRoll, -maxTurnRoll - strafeRoll, maxTurnRoll + strafeRoll);
                if (Mathf.Abs(yawDelta) < .001f && Mathf.Abs(movementRoll) < .02f)
                    targetRoll = Mathf.Lerp(targetRoll, 0f, 1f - Mathf.Exp(-rollResponse * dt));
            }

            if (forcedLookTarget != null)
                targetRoll = Mathf.Lerp(targetRoll, 0f, 1f - Mathf.Exp(-12f * dt));

            roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-rollResponse * dt));
            yawRoot.rotation = Quaternion.Slerp(yawRoot.rotation, Quaternion.Euler(0f, yaw, 0f), 1f - Mathf.Exp(-turnSmoothing * dt));
            pitchRoot.localRotation = Quaternion.Slerp(
                pitchRoot.localRotation,
                Quaternion.Euler(pitch, 0f, roll) * Quaternion.Euler(cinematicRotationOffset),
                1f - Mathf.Exp(-turnSmoothing * dt));

            float speed = player != null ? player.NormalizedSpeed : 0f;
            float runFactor = player != null && player.IsFinalRun ? runBobMultiplier : 1f;
            Vector3 bob = Vector3.zero;
            if (speed > .03f)
            {
                bobTime += dt * walkBobFrequency * Mathf.Lerp(.65f, 1.15f, speed) * runFactor;
                bob.y = Mathf.Sin(bobTime * 2f) * walkBobVertical * speed * runFactor;
                bob.x = Mathf.Cos(bobTime) * walkBobHorizontal * speed * runFactor;
            }
            else
            {
                idleTime += dt * 1.4f;
                bob.y = Mathf.Sin(idleTime) * idleBreathAmount;
            }

            if (shakeAmount > 0f)
            {
                Vector3 random = Random.insideUnitSphere * shakeAmount * GameSettings.CameraShake;
                shakePosition = Vector3.Lerp(shakePosition, random, 1f - Mathf.Exp(-32f * dt));
                shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, shakeDecay * dt);
            }
            else
            {
                shakePosition = Vector3.Lerp(shakePosition, Vector3.zero, 1f - Mathf.Exp(-20f * dt));
            }

            pitchRoot.localPosition = Vector3.Lerp(
                pitchRoot.localPosition,
                baseLocalPosition + cinematicPositionOffset + bob + shakePosition,
                1f - Mathf.Exp(-positionalSmoothing * dt));

            if (targetCamera != null)
                targetCamera.fieldOfView = Mathf.Lerp(
                    targetCamera.fieldOfView,
                    cinematicFov ? cinematicFovValue : GameSettings.DefaultFov,
                    1f - Mathf.Exp(-10f * dt));
        }

        public void AddShake(float amount, float decay = 2f)
        {
            shakeAmount = Mathf.Max(shakeAmount, amount);
            shakeDecay = Mathf.Max(.01f, decay);
        }

        public void SetInputEnabled(bool enabled) => inputEnabled = enabled;
        public void SetCinematicPositionOffset(Vector3 offset) => cinematicPositionOffset = offset;
        public void SetCinematicRotationOffset(Vector3 euler) => cinematicRotationOffset = euler;

        public void ClearCinematicTransform()
        {
            cinematicPositionOffset = Vector3.zero;
            cinematicRotationOffset = Vector3.zero;
        }

        public void SetCinematicFov(float fov)
        {
            cinematicFov = true;
            cinematicFovValue = Mathf.Clamp(fov, 35f, 110f);
        }

        public void ClearCinematicFov() => cinematicFov = false;

        public void SetForcedLookTarget(Transform target, Vector3 worldOffset, float response = 12f)
        {
            forcedLookTarget = target;
            forcedLookWorldOffset = worldOffset;
            forcedLookResponse = Mathf.Max(.01f, response);
        }

        public void ClearForcedLookTarget()
        {
            forcedLookTarget = null;
            forcedLookWorldOffset = Vector3.zero;
        }

        public void SnapView(Quaternion worldRotation)
        {
            yaw = worldRotation.eulerAngles.y;
            pitch = NormalizeAngle(worldRotation.eulerAngles.x);
            yawRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
            pitchRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            return angle;
        }
    }
}
