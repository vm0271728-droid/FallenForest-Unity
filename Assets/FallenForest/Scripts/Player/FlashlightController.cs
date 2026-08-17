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

        private bool placedForEnding;

        public bool Acquired { get; private set; }
        public bool PlacedForEnding => placedForEnding;
        public Ray CurrentRay => new Ray(
            rayOrigin != null ? rayOrigin.position : transform.position,
            rayOrigin != null ? rayOrigin.forward : transform.forward);

        private void Awake()
        {
            Acquired = acquiredAtStart;
            ApplyState();
        }

        public void Acquire()
        {
            Acquired = true;
            ApplyState();
        }

        private void ApplyState()
        {
            if (flashlight != null)
                flashlight.enabled = Acquired;
        }

        public bool IsIlluminating(Collider target)
        {
            if (!Acquired || placedForEnding || target == null) return false;
            Ray ray = CurrentRay;
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, detectionMask, QueryTriggerInteraction.Ignore))
                return hit.collider == target || hit.collider.transform.IsChildOf(target.transform);
            return false;
        }

        /// <summary>
        /// Finale-only presentation: detach the acquired lamp from the player, keep it switched on,
        /// place it on the road and aim its beam back into the forest.
        /// </summary>
        public void PlaceForEnding(Vector3 position, Vector3 direction)
        {
            if (!Acquired || placedForEnding) return;

            Transform lampTransform = flashlight != null ? flashlight.transform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            forward.Normalize();

            lampTransform.SetParent(null, true);
            lampTransform.position = position + Vector3.up * .10f;
            lampTransform.rotation = Quaternion.LookRotation((forward + Vector3.up * .035f).normalized, Vector3.up);

            if (flashlight != null)
                flashlight.enabled = true;

            rayOrigin = lampTransform;
            placedForEnding = true;
        }
    }
}
