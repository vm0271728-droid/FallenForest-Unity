using System.Collections.Generic;
using UnityEngine;

namespace FallenForest.World
{
    /// <summary>
    /// Marks trail/path volumes so gameplay systems can keep document pickups off the walked path.
    /// Use trigger colliders following the path; DocumentSpawner queries the nearest registered zone.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class TrailZone : MonoBehaviour
    {
        private static readonly List<TrailZone> Active = new();

        [SerializeField, Min(0f)] private float extraClearance = .35f;
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
                if (zone == null)
                {
                    Active.RemoveAt(i);
                    continue;
                }

                if (zone.zoneCollider == null)
                    zone.zoneCollider = zone.GetComponent<Collider>();
                if (zone.zoneCollider == null || !zone.zoneCollider.enabled)
                    continue;

                Vector3 closest = zone.zoneCollider.ClosestPoint(worldPosition);
                Vector2 a = new(worldPosition.x, worldPosition.z);
                Vector2 b = new(closest.x, closest.z);
                float clearance = Mathf.Max(0f, requestedClearance) + zone.extraClearance;

                // ClosestPoint returns the input point when it is inside the collider.
                if ((a - b).sqrMagnitude <= clearance * clearance)
                    return true;
            }
            return false;
        }
    }
}
