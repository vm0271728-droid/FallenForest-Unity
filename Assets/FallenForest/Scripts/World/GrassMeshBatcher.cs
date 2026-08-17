using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FallenForest.World
{
    /// <summary>
    /// Builds thousands of 3D grass clumps into spatial mesh chunks. Grass keeps its wind shader,
    /// but the Android renderer sees a small number of chunk renderers rather than ~9000 GameObjects.
    /// </summary>
    public sealed class GrassMeshBatcher
    {
        private readonly struct BatchKey
        {
            public readonly int x;
            public readonly int z;
            public readonly Material material;

            public BatchKey(int x, int z, Material material)
            {
                this.x = x;
                this.z = z;
                this.material = material;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = x * 73856093 ^ z * 19349663;
                    return h ^ (material != null ? material.GetInstanceID() : 0);
                }
            }

            public override bool Equals(object obj) =>
                obj is BatchKey other && x == other.x && z == other.z && material == other.material;
        }

        private readonly Dictionary<BatchKey, List<CombineInstance>> batches = new();
        private readonly float chunkSize;
        private readonly Transform parent;

        public GrassMeshBatcher(float chunkSize, Transform parent)
        {
            this.chunkSize = Mathf.Max(8f, chunkSize);
            this.parent = parent;
        }

        public bool Add(GameObject prefab, Vector3 worldPosition, Quaternion rotation, float scale)
        {
            if (prefab == null) return false;
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) return false;

            Matrix4x4 rootMatrix = Matrix4x4.TRS(worldPosition, rotation, Vector3.one * scale);
            bool added = false;
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter.sharedMesh == null) continue;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || renderer.sharedMaterials.Length == 0) continue;

                // Most foliage prefabs use one material. For multi-material meshes, split submeshes.
                Material[] materials = renderer.sharedMaterials;
                int subCount = Mathf.Min(filter.sharedMesh.subMeshCount, materials.Length);
                Matrix4x4 local = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Matrix4x4 matrix = rootMatrix * local;
                int cx = Mathf.FloorToInt(worldPosition.x / chunkSize);
                int cz = Mathf.FloorToInt(worldPosition.z / chunkSize);

                for (int sub = 0; sub < subCount; sub++)
                {
                    Material material = materials[sub];
                    if (material == null) continue;
                    BatchKey key = new(cx, cz, material);
                    if (!batches.TryGetValue(key, out List<CombineInstance> list))
                    {
                        list = new List<CombineInstance>(128);
                        batches.Add(key, list);
                    }
                    list.Add(new CombineInstance
                    {
                        mesh = filter.sharedMesh,
                        subMeshIndex = sub,
                        transform = matrix
                    });
                    added = true;
                }
            }
            return added;
        }

        public int Build()
        {
            int built = 0;
            foreach (KeyValuePair<BatchKey, List<CombineInstance>> pair in batches)
            {
                if (pair.Value.Count == 0 || pair.Key.material == null) continue;

                GameObject go = new($"GrassChunk_{pair.Key.x}_{pair.Key.z}_{built:000}");
                go.transform.SetParent(parent, false);
                MeshFilter filter = go.AddComponent<MeshFilter>();
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();

                Mesh mesh = new()
                {
                    name = go.name + "_Mesh",
                    indexFormat = IndexFormat.UInt32
                };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true, false);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = pair.Key.material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.allowOcclusionWhenDynamic = true;
                built++;
            }
            batches.Clear();
            return built;
        }
    }
}
