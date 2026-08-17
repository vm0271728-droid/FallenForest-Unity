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
        public bool Acquired { get; private set; }
        public Ray CurrentRay => new Ray(rayOrigin != null ? rayOrigin.position : transform.position, rayOrigin != null ? rayOrigin.forward : transform.forward);
        private void Awake() { Acquired = acquiredAtStart; ApplyState(); }
        public void Acquire() { Acquired = true; ApplyState(); }
        private void ApplyState() { if (flashlight != null) flashlight.enabled = Acquired; }
        public bool IsIlluminating(Collider target)
        {
            if (!Acquired || target == null) return false;
            Ray ray = CurrentRay;
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, detectionMask, QueryTriggerInteraction.Ignore)) return hit.collider == target || hit.collider.transform.IsChildOf(target.transform);
            return false;
        }
    }
}
