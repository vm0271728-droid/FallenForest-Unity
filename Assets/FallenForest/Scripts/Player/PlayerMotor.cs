using FallenForest.Core;
using FallenForest.Input;
using UnityEngine;

namespace FallenForest.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private FloatingJoystickInput joystick;
        [SerializeField] private Transform movementReference;
        [SerializeField] private float walkSpeed = 3.25f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float deceleration = 22f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float finalRunMultiplier = 2.15f;

        private CharacterController controller;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private bool controlsEnabled = true;
        private bool finalRun;
        private float externalSpeedMultiplier = 1f;

        public float WalkSpeed => walkSpeed;
        public float CurrentMaxSpeed => walkSpeed * (finalRun ? finalRunMultiplier : 1f) * externalSpeedMultiplier;
        public bool IsFinalRun => finalRun;
        public bool IsMoving => planarVelocity.sqrMagnitude > 0.04f;
        public float NormalizedSpeed => Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(CurrentMaxSpeed, 0.01f));
        public Vector3 PlanarVelocity => planarVelocity;
        public float LateralSpeed
        {
            get
            {
                Transform b = movementReference != null ? movementReference : transform;
                return Vector3.Dot(planarVelocity, b.right) / Mathf.Max(CurrentMaxSpeed, 0.01f);
            }
        }

        private void Awake() => controller = GetComponent<CharacterController>();

        private void OnEnable()
        {
            if (GameProgress.Instance != null) GameProgress.Instance.FinalRunStarted += EnableFinalRun;
        }

        private void Start()
        {
            if (GameProgress.Instance != null)
            {
                GameProgress.Instance.FinalRunStarted -= EnableFinalRun;
                GameProgress.Instance.FinalRunStarted += EnableFinalRun;
                if (GameProgress.Instance.FinalRun) EnableFinalRun();
            }
        }

        private void OnDisable()
        {
            if (GameProgress.Instance != null) GameProgress.Instance.FinalRunStarted -= EnableFinalRun;
        }

        private void Update()
        {
            Vector2 input = controlsEnabled && joystick != null ? joystick.Value : Vector2.zero;
            Transform b = movementReference != null ? movementReference : transform;
            Vector3 desired = Vector3.ProjectOnPlane(b.forward, Vector3.up).normalized * input.y +
                              Vector3.ProjectOnPlane(b.right, Vector3.up).normalized * input.x;
            if (desired.sqrMagnitude > 1f) desired.Normalize();
            desired *= CurrentMaxSpeed;

            float rate = desired.sqrMagnitude > planarVelocity.sqrMagnitude ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desired, rate * Time.deltaTime);
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;
            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        public void SetControlsEnabled(bool enabled)
        {
            controlsEnabled = enabled;
            if (!enabled) planarVelocity = Vector3.zero;
        }

        /// <summary>Temporary encounter multiplier. Boiled focus uses 0.33 (67% slowdown).</summary>
        public void SetExternalSpeedMultiplier(float multiplier)
        {
            externalSpeedMultiplier = Mathf.Clamp(multiplier, 0f, 3f);
        }

        public void ClearExternalSpeedMultiplier() => externalSpeedMultiplier = 1f;
        public void EnableFinalRun() => finalRun = true;

        public void Teleport(Vector3 position)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            bool enabled = controller.enabled;
            controller.enabled = false;
            transform.position = position;
            controller.enabled = enabled;
            planarVelocity = Vector3.zero;
            verticalVelocity = -2f;
        }
    }
}
