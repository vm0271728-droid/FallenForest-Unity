using System.Collections.Generic;
using UnityEngine;

namespace FallenForest.World
{
    /// <summary>
    /// Marks trail/path volumes. Documents stay off these zones and procedural grass uses the
    /// collider boundary as a smooth density gradient: almost none on the trail, sparse beside it,
    /// dense again farther into the forest.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class TrailZone : MonoBehaviour
    {
        private static readonly List<TrailZone> Active = new();

        [SerializeField, Min(0f)] private float extraDocumentClearance = .35f;
        [SerializeField, Min(.1f)] private float grassTransitionWidth = 2.8f;
        private Collider zoneCollider;

        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable() => Active.Remove(this);
        private void OnDestroy() => Active.Remove(this);

        public static bool IsNearAnyTrail(Vector3 worldPosition, float requestedClearance)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                TrailZone zone = Active[i];
                if (!zone.TryGetHorizontalDistance(worldPosition, out float distance))
                    continue;

                float clearance = Mathf.Max(0f, requestedClearance) + zone.extraDocumentClearance;
                if (distance <= clearance)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns 0 on the path volume and smoothly approaches 1 as the point moves away.
        /// Multiple overlapping trail zones use the lowest density multiplier.
        /// </summary>
        public static float GrassDensityMultiplier(Vector3 worldPosition)
        {
            float multiplier = 1f;
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                TrailZone zone = Active[i];
                if (!zone.TryGetHorizontalDistance(worldPosition, out float distance))
                    continue;

                float local = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / Mathf.Max(.1f, zone.grassTransitionWidth)));
                multiplier = Mathf.Min(multiplier, local);
                if (multiplier <= .001f)
                    return 0f;
            }
            return multiplier;
        }

        private bool TryGetHorizontalDistance(Vector3 worldPosition, out float distance)
        {
            if (zoneCollider == null)
                zoneCollider = GetComponent<Collider>();
            if (zoneCollider == null || !zoneCollider.enabled)
            {
                distance = float.PositiveInfinity;
                return false;
            }

            Vector3 closest = zoneCollider.ClosestPoint(worldPosition);
            Vector2 a = new(worldPosition.x, worldPosition.z);
            Vector2 b = new(closest.x, closest.z);
            distance = Vector2.Distance(a, b);
            return true;
        }
    }
}
