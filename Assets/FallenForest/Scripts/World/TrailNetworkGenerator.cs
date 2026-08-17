using System.Collections.Generic;
using UnityEngine;

namespace FallenForest.World
{
    /// <summary>
    /// Builds a small deterministic network of narrow trails that follows the Terrain surface.
    /// Every segment also gets a TrailZone trigger so documents stay off the walked line, trees do
    /// not block it and grass smoothly changes from almost absent on dirt to dense forest growth.
    /// </summary>
    public sealed class TrailNetworkGenerator : MonoBehaviour
    {
        [SerializeField] private Terrain terrain;
        [SerializeField] private Material trailMaterial;
        [SerializeField] private int seed = 228117;
        [SerializeField, Range(1, 4)] private int trailCount = 3;
        [SerializeField, Min(.7f)] private float trailWidth = 1.65f;
        [SerializeField, Min(1f)] private float sampleSpacing = 4.25f;
        [SerializeField, Range(.05f, .35f)] private float waypointJitter = .16f;
        [SerializeField, Min(.001f)] private float surfaceOffset = .025f;
        [SerializeField, Min(1f)] private float zoneHeight = 5f;
        [SerializeField] private bool generateOnAwake = true;
        [SerializeField] private Transform generatedRoot;

        private readonly List<Mesh> generatedMeshes = new();
        private Material runtimeMaterial;

        private void Awake()
        {
            if (terrain == null) terrain = Terrain.activeTerrain;
            if (generateOnAwake) Generate();
        }

        [ContextMenu("Generate Trail Network")]
        public void Generate()
        {
            EnsureRoot();
            ClearGeneratedChildren();

            if (terrain == null) terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null) return;

            Material material = trailMaterial != null ? trailMaterial : BuildRuntimeMaterial();
            var rng = new System.Random(seed);

            for (int i = 0; i < trailCount; i++)
            {
                List<Vector3> control = BuildControlPoints(i, rng);
                List<Vector3> samples = SampleCatmullRom(control);
                if (samples.Count < 2) continue;
                BuildRibbon(samples, material, i);
                BuildZones(samples, i);
            }
        }

        private void EnsureRoot()
        {
            if (generatedRoot != null) return;

            Transform existing = transform.Find("GeneratedTrails");
            if (existing != null)
            {
                generatedRoot = existing;
                return;
            }

            GameObject root = new("GeneratedTrails");
            root.transform.SetParent(transform, false);
            generatedRoot = root.transform;
        }

        private List<Vector3> BuildControlPoints(int trailIndex, System.Random rng)
        {
            Vector2 startNorm;
            Vector2 endNorm;

            switch (trailIndex % 3)
            {
                case 0:
                    startNorm = new Vector2(.06f, .20f);
                    endNorm = new Vector2(.94f, .77f);
                    break;
                case 1:
                    startNorm = new Vector2(.13f, .91f);
                    endNorm = new Vector2(.88f, .09f);
                    break;
                default:
                    startNorm = new Vector2(.02f, .56f);
                    endNorm = new Vector2(.98f, .47f);
                    break;
            }

            const int controls = 7;
            List<Vector3> result = new(controls);
            for (int i = 0; i < controls; i++)
            {
                float t = i / (float)(controls - 1);
                Vector2 p = Vector2.Lerp(startNorm, endNorm, t);

                if (i > 0 && i < controls - 1)
                {
                    float taper = Mathf.Sin(t * Mathf.PI);
                    p.x += Next(rng, -waypointJitter, waypointJitter) * taper;
                    p.y += Next(rng, -waypointJitter, waypointJitter) * taper;
                }

                p.x = Mathf.Clamp(p.x, .025f, .975f);
                p.y = Mathf.Clamp(p.y, .025f, .975f);
                result.Add(NormalizedToGround(p));
            }
            return result;
        }

        private List<Vector3> SampleCatmullRom(List<Vector3> controls)
        {
            List<Vector3> result = new();
            if (controls == null || controls.Count < 2) return result;

            for (int segment = 0; segment < controls.Count - 1; segment++)
            {
                Vector3 p0 = controls[Mathf.Max(0, segment - 1)];
                Vector3 p1 = controls[segment];
                Vector3 p2 = controls[segment + 1];
                Vector3 p3 = controls[Mathf.Min(controls.Count - 1, segment + 2)];
                float approximateLength = Vector3.Distance(p1, p2);
                int steps = Mathf.Max(2, Mathf.CeilToInt(approximateLength / Mathf.Max(1f, sampleSpacing)));

                for (int s = 0; s < steps; s++)
                {
                    if (segment > 0 && s == 0) continue;
                    float t = s / (float)steps;
                    Vector3 p = CatmullRom(p0, p1, p2, p3, t);
                    p.y = GroundY(p) + surfaceOffset;
                    result.Add(p);
                }
            }

            Vector3 last = controls[controls.Count - 1];
            last.y = GroundY(last) + surfaceOffset;
            result.Add(last);
            return result;
        }

        private void BuildRibbon(List<Vector3> points, Material material, int index)
        {
            int count = points.Count;
            Vector3[] vertices = new Vector3[count * 2];
            Vector2[] uv = new Vector2[count * 2];
            int[] triangles = new int[(count - 1) * 6];

            float distance = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 forward;
                if (i == 0) forward = points[1] - points[0];
                else if (i == count - 1) forward = points[count - 1] - points[count - 2];
                else forward = points[i + 1] - points[i - 1];

                forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < .001f) forward = Vector3.forward;

                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                Vector3 localCentre = generatedRoot.InverseTransformPoint(points[i]);
                Vector3 localRight = generatedRoot.InverseTransformDirection(right).normalized;
                float half = trailWidth * .5f;
                vertices[i * 2] = localCentre - localRight * half;
                vertices[i * 2 + 1] = localCentre + localRight * half;

                if (i > 0) distance += Vector3.Distance(points[i - 1], points[i]);
                float v = distance / Mathf.Max(.5f, trailWidth * 2f);
                uv[i * 2] = new Vector2(0f, v);
                uv[i * 2 + 1] = new Vector2(1f, v);
            }

            int ti = 0;
            for (int i = 0; i < count - 1; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                triangles[ti++] = a;
                triangles[ti++] = c;
                triangles[ti++] = b;
                triangles[ti++] = b;
                triangles[ti++] = c;
                triangles[ti++] = d;
            }

            Mesh mesh = new()
            {
                name = $"Trail_{index + 1:00}_Mesh",
                vertices = vertices,
                uv = uv,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            generatedMeshes.Add(mesh);

            GameObject go = new($"Trail_{index + 1:00}_Ribbon", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(generatedRoot, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private void BuildZones(List<Vector3> points, int index)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[i + 1];
                Vector3 flat = Vector3.ProjectOnPlane(b - a, Vector3.up);
                float length = flat.magnitude;
                if (length < .1f) continue;

                GameObject zone = new($"Trail_{index + 1:00}_Zone_{i + 1:000}");
                zone.transform.SetParent(generatedRoot, true);
                zone.transform.position = (a + b) * .5f + Vector3.up * (zoneHeight * .5f - .2f);
                zone.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

                BoxCollider collider = zone.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new Vector3(trailWidth, zoneHeight, length + .3f);
                zone.AddComponent<TrailZone>();
            }
        }

        private Vector3 NormalizedToGround(Vector2 normalized)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            Vector3 world = new(
                origin.x + normalized.x * size.x,
                0f,
                origin.z + normalized.y * size.z);
            world.y = GroundY(world) + surfaceOffset;
            return world;
        }

        private float GroundY(Vector3 world)
        {
            return terrain.SampleHeight(world) + terrain.transform.position.y;
        }

        private Material BuildRuntimeMaterial()
        {
            if (runtimeMaterial != null) return runtimeMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            runtimeMaterial = shader != null ? new Material(shader) : null;

            if (runtimeMaterial != null)
            {
                runtimeMaterial.name = "Trail_Runtime_Material";
                Color dirt = new(.105f, .082f, .055f, 1f);
                if (runtimeMaterial.HasProperty("_BaseColor")) runtimeMaterial.SetColor("_BaseColor", dirt);
                if (runtimeMaterial.HasProperty("_Color")) runtimeMaterial.SetColor("_Color", dirt);
                if (runtimeMaterial.HasProperty("_Smoothness")) runtimeMaterial.SetFloat("_Smoothness", .08f);
                if (runtimeMaterial.HasProperty("_Metallic")) runtimeMaterial.SetFloat("_Metallic", 0f);
            }
            return runtimeMaterial;
        }

        [ContextMenu("Clear Trail Network")]
        public void Clear()
        {
            EnsureRoot();
            ClearGeneratedChildren();
        }

        private void ClearGeneratedChildren()
        {
            if (generatedRoot != null)
            {
                for (int i = generatedRoot.childCount - 1; i >= 0; i--)
                    DestroyUnityObject(generatedRoot.GetChild(i).gameObject);
            }

            for (int i = generatedMeshes.Count - 1; i >= 0; i--)
                if (generatedMeshes[i] != null)
                    DestroyUnityObject(generatedMeshes[i]);
            generatedMeshes.Clear();
        }

        private void OnDestroy()
        {
            ClearGeneratedChildren();
            if (runtimeMaterial != null)
                DestroyUnityObject(runtimeMaterial);
        }

        private static void DestroyUnityObject(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return .5f * ((2f * p1) +
                         (-p0 + p2) * t +
                         (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                         (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static float Next(System.Random rng, float min, float max) =>
            Mathf.Lerp(min, max, (float)rng.NextDouble());
    }
}
