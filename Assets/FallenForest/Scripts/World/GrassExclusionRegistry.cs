using System.Collections.Generic;
using UnityEngine;

namespace FallenForest.World
{
    /// <summary>
    /// Feeds a small set of circular grass exclusion zones to the foliage shader.
    /// This works with batched grass because suppression happens in the shader, not by deleting meshes.
    /// </summary>
    public static class GrassExclusionRegistry
    {
        public const int MaxZones = 16;

        private sealed class Entry
        {
            public Object owner;
            public Vector3 position;
            public float radius;
        }

        private static readonly List<Entry> Entries = new(MaxZones);
        private static readonly Vector4[] ShaderZones = new Vector4[MaxZones];
        private static readonly int CountId = Shader.PropertyToID("_FF_GrassExclusionCount");
        private static readonly int ZonesId = Shader.PropertyToID("_FF_GrassExclusions");

        public static void Register(Object owner, Vector3 position, float radius)
        {
            if (owner == null) return;
            radius = Mathf.Max(.05f, radius);

            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].owner != owner) continue;
                Entries[i].position = position;
                Entries[i].radius = radius;
                PushGlobals();
                return;
            }

            if (Entries.Count >= MaxZones)
            {
                Debug.LogWarning($"Fallen Forest: grass exclusion registry reached {MaxZones} zones; extra zone ignored.");
                return;
            }

            Entries.Add(new Entry { owner = owner, position = position, radius = radius });
            PushGlobals();
        }

        public static void Update(Object owner, Vector3 position, float radius)
        {
            Register(owner, position, radius);
        }

        public static void Unregister(Object owner)
        {
            if (owner == null) return;
            for (int i = Entries.Count - 1; i >= 0; i--)
                if (Entries[i].owner == owner)
                    Entries.RemoveAt(i);
            PushGlobals();
        }

        public static void Clear()
        {
            Entries.Clear();
            PushGlobals();
        }

        private static void PushGlobals()
        {
            int count = Mathf.Min(Entries.Count, MaxZones);
            for (int i = 0; i < ShaderZones.Length; i++)
            {
                if (i < count)
                {
                    Entry e = Entries[i];
                    ShaderZones[i] = new Vector4(e.position.x, e.position.y, e.position.z, e.radius);
                }
                else
                {
                    ShaderZones[i] = Vector4.zero;
                }
            }

            Shader.SetGlobalFloat(CountId, count);
            Shader.SetGlobalVectorArray(ZonesId, ShaderZones);
        }
    }

    /// <summary>
    /// Attach to an object that should visually clear dense grass around itself.
    /// </summary>
    public sealed class GrassExclusionEmitter : MonoBehaviour
    {
        [SerializeField] private float radius = .92f;

        public void Configure(float newRadius)
        {
            radius = Mathf.Max(.05f, newRadius);
            if (isActiveAndEnabled)
                GrassExclusionRegistry.Update(this, transform.position, radius);
        }

        private void OnEnable() => GrassExclusionRegistry.Register(this, transform.position, radius);
        private void LateUpdate() => GrassExclusionRegistry.Update(this, transform.position, radius);
        private void OnDisable() => GrassExclusionRegistry.Unregister(this);
        private void OnDestroy() => GrassExclusionRegistry.Unregister(this);
    }
}
