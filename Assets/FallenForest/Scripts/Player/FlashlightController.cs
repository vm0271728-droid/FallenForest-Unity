using UnityEngine;

namespace FallenForest.Player
{
    public sealed class FlashlightController : MonoBehaviour
    {
        [SerializeField] private Light flashlight;
        [SerializeField] private float maxDistance = 55f;
        [SerializeField] private LayerMask detectionMask = ~0;
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private bool acquiredAtStart;
        [SerializeField] private bool turnOnWhenAcquired = true;

        public bool Acquired { get; private set; }
        public bool IsOn { get; private set; }
        public Light Light => flashlight;
        public Ray CurrentRay => new(
            rayOrigin != null ? rayOrigin.position : transform.position,
            rayOrigin != null ? rayOrigin.forward : transform.forward);

        private void Awake()
        {
            Acquired = acquiredAtStart;
            IsOn = acquiredAtStart && turnOnWhenAcquired;
            ApplyState();
        }

        public void Acquire()
        {
            Acquired = true;
            IsOn = turnOnWhenAcquired;
            ApplyState();
        }

        public void SetOn(bool on)
        {
            if (!Acquired)
            {
                IsOn = false;
                ApplyState();
                return;
            }

            IsOn = on;
            ApplyState();
        }

        public void Toggle()
        {
            if (!Acquired) return;
            SetOn(!IsOn);
        }

        public void SetPhysicalRayOrigin(Transform origin)
        {
            rayOrigin = origin != null ? origin : transform;
        }

        private void ApplyState()
        {
            if (flashlight != null)
                flashlight.enabled = Acquired && IsOn;
        }

        public bool IsIlluminating(Collider target)
        {
            if (!Acquired || !IsOn || target == null) return false;
            Ray ray = CurrentRay;
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, detectionMask, QueryTriggerInteraction.Ignore))
                return hit.collider == target || hit.collider.transform.IsChildOf(target.transform);
            return false;
        }
    }
}
