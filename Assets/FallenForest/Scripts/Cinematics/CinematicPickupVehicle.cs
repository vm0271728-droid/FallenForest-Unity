using System;
using UnityEngine;

namespace FallenForest.Cinematics
{
    /// <summary>
    /// Physics-driven finale pickup used for the road approach. It deliberately uses WheelCollider
    /// instead of transform-only animation so suspension compression, wheel spin and steering remain
    /// physically coherent while the truck drives toward the player.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CinematicPickupVehicle : MonoBehaviour
    {
        [Serializable]
        private sealed class Wheel
        {
            public Transform visual;
            public WheelCollider collider;
            public bool steer;
            public bool driven = true;
            [NonSerialized] public Quaternion visualRotationOffset = Quaternion.identity;
        }

        [Header("Wheels")]
        [SerializeField] private Wheel frontLeft = new();
        [SerializeField] private Wheel frontRight = new();
        [SerializeField] private Wheel rearLeft = new();
        [SerializeField] private Wheel rearRight = new();
        [SerializeField] private float wheelRadius = .39f;
        [SerializeField] private float wheelMass = 28f;
        [SerializeField] private float suspensionDistance = .22f;
        [SerializeField] private float spring = 32000f;
        [SerializeField] private float damper = 4500f;
        [SerializeField, Range(0f, 1f)] private float suspensionTargetPosition = .48f;
        [SerializeField] private float antiRollForce = 6500f;

        [Header("Automatic cinematic driving")]
        [SerializeField] private Transform[] route;
        [SerializeField] private float cruiseSpeed = 11.5f;
        [SerializeField] private float approachSpeed = 5.2f;
        [SerializeField] private float routePointRadius = 2.3f;
        [SerializeField] private float finalStopDistance = 1.25f;
        [SerializeField] private float maxSteerAngle = 28f;
        [SerializeField] private float motorTorque = 1450f;
        [SerializeField] private float brakeTorque = 4200f;
        [SerializeField] private float steeringResponse = 5.5f;
        [SerializeField] private float speedResponse = 3.2f;

        [Header("Body")]
        [SerializeField] private Vector3 centerOfMassOffset = new(0f, -.46f, .08f);
        [SerializeField] private float maxAngularVelocity = 5.5f;

        [Header("Lights")]
        [SerializeField] private Light[] headlights;
        [SerializeField] private Light[] tailLights;
        [SerializeField] private Renderer[] headlampMeshes;
        [SerializeField] private Renderer[] tailLampMeshes;
        [SerializeField] private float headlampEmission = 6.5f;
        [SerializeField] private float tailLampEmission = 2.4f;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private Rigidbody body;
        private Wheel[] wheels;
        private int routeIndex;
        private bool driving;
        private bool stopping;
        private float currentSteer;
        private float currentThrottle;

        public bool IsDriving => driving;
        public bool HasStopped => !driving && stopping;
        public float Speed => body != null ? body.linearVelocity.magnitude : 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.centerOfMass += centerOfMassOffset;
            body.maxAngularVelocity = maxAngularVelocity;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            wheels = new[] { frontLeft, frontRight, rearLeft, rearRight };
            CaptureVisualOffsets();
            ConfigureWheelColliders();
            SetLights(false);
        }

        private void FixedUpdate()
        {
            if (driving)
                DriveRoute();
            else if (stopping)
                ApplyBrakes(brakeTorque);

            ApplyAntiRoll(frontLeft, frontRight);
            ApplyAntiRoll(rearLeft, rearRight);
        }

        private void LateUpdate()
        {
            UpdateWheelVisual(frontLeft);
            UpdateWheelVisual(frontRight);
            UpdateWheelVisual(rearLeft);
            UpdateWheelVisual(rearRight);
        }

        public void StartDrive(Transform[] overrideRoute = null)
        {
            if (overrideRoute != null && overrideRoute.Length > 0)
                route = overrideRoute;

            if (route == null || route.Length == 0)
            {
                Debug.LogWarning("CinematicPickupVehicle cannot start: route is empty.", this);
                return;
            }

            routeIndex = 0;
            stopping = false;
            driving = true;
            SetLights(true);
            ApplyBrakes(0f);
        }

        public void StopNow(bool keepLightsOn = true)
        {
            driving = false;
            stopping = true;
            currentThrottle = 0f;
            ApplyMotorTorque(0f);
            ApplyBrakes(brakeTorque);
            SetLights(keepLightsOn);
        }

        public void SetLights(bool enabled)
        {
            SetLightArray(headlights, enabled);
            SetLightArray(tailLights, enabled);
            SetEmission(headlampMeshes, enabled ? headlampEmission : 0f, Color.white);
            SetEmission(tailLampMeshes, enabled ? tailLampEmission : 0f, new Color(1f, .06f, .025f));
        }

        private void DriveRoute()
        {
            if (route == null || route.Length == 0)
            {
                StopNow();
                return;
            }

            routeIndex = Mathf.Clamp(routeIndex, 0, route.Length - 1);
            Transform target = route[routeIndex];
            if (target == null)
            {
                AdvanceRoute();
                return;
            }

            Vector3 toTargetWorld = target.position - transform.position;
            Vector3 planar = Vector3.ProjectOnPlane(toTargetWorld, transform.up);
            float distance = planar.magnitude;

            bool finalPoint = routeIndex >= route.Length - 1;
            if (!finalPoint && distance <= routePointRadius)
            {
                AdvanceRoute();
                return;
            }

            if (finalPoint && distance <= finalStopDistance)
            {
                StopNow(true);
                return;
            }

            Vector3 localTarget = transform.InverseTransformDirection(planar.normalized);
            float desiredSteer = Mathf.Clamp(localTarget.x * maxSteerAngle * 1.55f, -maxSteerAngle, maxSteerAngle);
            currentSteer = Mathf.Lerp(currentSteer, desiredSteer, 1f - Mathf.Exp(-steeringResponse * Time.fixedDeltaTime));
            ApplySteer(currentSteer);

            float speed = Speed;
            float desiredSpeed = cruiseSpeed;
            if (finalPoint)
            {
                float slowZone = Mathf.Max(7f, cruiseSpeed * 1.2f);
                float t = Mathf.InverseLerp(finalStopDistance, slowZone, distance);
                desiredSpeed = Mathf.Lerp(0f, approachSpeed, Mathf.Clamp01(t));
            }

            float speedError = desiredSpeed - speed;
            float desiredThrottle = Mathf.Clamp(speedError / Mathf.Max(1f, cruiseSpeed), -1f, 1f);
            currentThrottle = Mathf.Lerp(currentThrottle, desiredThrottle, 1f - Mathf.Exp(-speedResponse * Time.fixedDeltaTime));

            if (currentThrottle >= 0f)
            {
                ApplyBrakes(0f);
                ApplyMotorTorque(currentThrottle * motorTorque);
            }
            else
            {
                ApplyMotorTorque(0f);
                ApplyBrakes(Mathf.Abs(currentThrottle) * brakeTorque);
            }
        }

        private void AdvanceRoute()
        {
            if (route == null || route.Length == 0) return;
            routeIndex = Mathf.Min(routeIndex + 1, route.Length - 1);
        }

        private void CaptureVisualOffsets()
        {
            if (wheels == null) return;
            foreach (Wheel wheel in wheels)
            {
                if (wheel?.visual == null) continue;
                wheel.visualRotationOffset = wheel.visual.localRotation;
            }
        }

        private void ConfigureWheelColliders()
        {
            if (wheels == null) return;
            foreach (Wheel wheel in wheels)
            {
                if (wheel == null || wheel.collider == null) continue;
                wheel.collider.radius = wheelRadius;
                wheel.collider.mass = wheelMass;
                wheel.collider.suspensionDistance = suspensionDistance;

                JointSpring suspension = wheel.collider.suspensionSpring;
                suspension.spring = spring;
                suspension.damper = damper;
                suspension.targetPosition = suspensionTargetPosition;
                wheel.collider.suspensionSpring = suspension;

                WheelFrictionCurve forward = wheel.collider.forwardFriction;
                forward.extremumSlip = .32f;
                forward.extremumValue = 1.15f;
                forward.asymptoteSlip = .7f;
                forward.asymptoteValue = .82f;
                forward.stiffness = 1.18f;
                wheel.collider.forwardFriction = forward;

                WheelFrictionCurve sideways = wheel.collider.sidewaysFriction;
                sideways.extremumSlip = .22f;
                sideways.extremumValue = 1.08f;
                sideways.asymptoteSlip = .55f;
                sideways.asymptoteValue = .8f;
                sideways.stiffness = 1.22f;
                wheel.collider.sidewaysFriction = sideways;
            }
        }

        private void ApplySteer(float angle)
        {
            if (frontLeft?.collider != null && frontLeft.steer) frontLeft.collider.steerAngle = angle;
            if (frontRight?.collider != null && frontRight.steer) frontRight.collider.steerAngle = angle;
            if (rearLeft?.collider != null && rearLeft.steer) rearLeft.collider.steerAngle = angle;
            if (rearRight?.collider != null && rearRight.steer) rearRight.collider.steerAngle = angle;
        }

        private void ApplyMotorTorque(float torque)
        {
            if (wheels == null) return;
            int drivenCount = 0;
            foreach (Wheel wheel in wheels)
                if (wheel != null && wheel.driven && wheel.collider != null)
                    drivenCount++;

            float splitTorque = drivenCount > 0 ? torque / drivenCount : 0f;
            foreach (Wheel wheel in wheels)
                if (wheel != null && wheel.driven && wheel.collider != null)
                    wheel.collider.motorTorque = splitTorque;
        }

        private void ApplyBrakes(float torque)
        {
            if (wheels == null) return;
            foreach (Wheel wheel in wheels)
                if (wheel?.collider != null)
                    wheel.collider.brakeTorque = torque;
        }

        private void ApplyAntiRoll(Wheel left, Wheel right)
        {
            if (body == null || left?.collider == null || right?.collider == null) return;

            float leftTravel = 1f;
            float rightTravel = 1f;
            bool leftGrounded = left.collider.GetGroundHit(out WheelHit leftHit);
            bool rightGrounded = right.collider.GetGroundHit(out WheelHit rightHit);

            if (leftGrounded)
            {
                Vector3 localHit = left.collider.transform.InverseTransformPoint(leftHit.point);
                leftTravel = (-localHit.y - left.collider.radius) / Mathf.Max(.001f, left.collider.suspensionDistance);
            }

            if (rightGrounded)
            {
                Vector3 localHit = right.collider.transform.InverseTransformPoint(rightHit.point);
                rightTravel = (-localHit.y - right.collider.radius) / Mathf.Max(.001f, right.collider.suspensionDistance);
            }

            float antiRoll = (leftTravel - rightTravel) * antiRollForce;
            if (leftGrounded)
                body.AddForceAtPosition(left.collider.transform.up * -antiRoll, left.collider.transform.position);
            if (rightGrounded)
                body.AddForceAtPosition(right.collider.transform.up * antiRoll, right.collider.transform.position);
        }

        private static void UpdateWheelVisual(Wheel wheel)
        {
            if (wheel?.collider == null || wheel.visual == null) return;
            wheel.collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            wheel.visual.SetPositionAndRotation(position, rotation * wheel.visualRotationOffset);
        }

        private static void SetLightArray(Light[] lights, bool enabled)
        {
            if (lights == null) return;
            foreach (Light light in lights)
                if (light != null)
                    light.enabled = enabled;
        }

        private static void SetEmission(Renderer[] renderers, float intensity, Color color)
        {
            if (renderers == null) return;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                foreach (Material material in renderer.materials)
                {
                    if (material == null || !material.HasProperty(EmissionColor)) continue;
                    material.EnableKeyword("_EMISSION");
                    material.SetColor(EmissionColor, color * intensity);
                }
            }
        }
    }
}
