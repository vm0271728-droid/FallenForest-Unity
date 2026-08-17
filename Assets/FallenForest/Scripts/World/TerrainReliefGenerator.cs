using UnityEngine;

namespace FallenForest.World
{
    /// <summary>
    /// Generates a natural uneven forest floor from layered deterministic noise. Intended to be
    /// baked/generated before release rather than recalculated every frame. Relief is deliberately
    /// moderate: enough hills, depressions and shallow ravines to break the flat-plane look without
    /// turning normal first-person movement into mountain traversal.
    /// </summary>
    [RequireComponent(typeof(Terrain))]
    public sealed class TerrainReliefGenerator : MonoBehaviour
    {
        [SerializeField] private Terrain terrain;
        [SerializeField] private int seed = 228117;
        [SerializeField, Min(32)] private int targetHeightmapResolution = 513;

        [Header("Relief")]
        [SerializeField, Min(1f)] private float terrainHeight = 34f;
        [SerializeField] private float baseElevation = .22f;
        [SerializeField] private float broadHillAmplitude = .24f;
        [SerializeField] private float broadHillFrequency = 1.65f;
        [SerializeField] private float mediumAmplitude = .105f;
        [SerializeField] private float mediumFrequency = 4.8f;
        [SerializeField] private float fineAmplitude = .028f;
        [SerializeField] private float fineFrequency = 13.5f;
        [SerializeField] private float ridgeAmplitude = .055f;
        [SerializeField] private float ridgeFrequency = 3.2f;

        [Header("Natural depressions")]
        [SerializeField, Range(0f, .25f)] private float depressionStrength = .075f;
        [SerializeField] private float depressionFrequency = 2.35f;
        [SerializeField, Range(.1f, .9f)] private float depressionThreshold = .58f;

        [Header("Playable opening area")]
        [SerializeField] private Transform startPoint;
        [SerializeField, Min(0f)] private float startFlattenRadius = 8f;
        [SerializeField, Min(.1f)] private float startBlendWidth = 6f;

        [Header("Generation")]
        [SerializeField] private bool generateOnAwake;

        private void Reset()
        {
            terrain = GetComponent<Terrain>();
        }

        private void Awake()
        {
            if (terrain == null) terrain = GetComponent<Terrain>();
            if (generateOnAwake) Generate();
        }

        [ContextMenu("Generate Uneven Terrain")]
        public void Generate()
        {
            if (terrain == null) terrain = GetComponent<Terrain>();
            if (terrain == null || terrain.terrainData == null) return;

            TerrainData data = terrain.terrainData;
            int resolution = ClosestValidHeightmapResolution(targetHeightmapResolution);
            if (data.heightmapResolution != resolution)
                data.heightmapResolution = resolution;

            Vector3 size = data.size;
            if (size.y < terrainHeight)
                size.y = terrainHeight;
            data.size = size;

            float[,] heights = new float[resolution, resolution];
            var rng = new System.Random(seed);
            Vector2 broadOffset = Offset(rng);
            Vector2 mediumOffset = Offset(rng);
            Vector2 fineOffset = Offset(rng);
            Vector2 ridgeOffset = Offset(rng);
            Vector2 depressionOffset = Offset(rng);

            float centerStartHeight = -1f;
            Vector2 normalizedStart = Vector2.zero;
            bool flattenStart = startPoint != null && startFlattenRadius > .01f;
            if (flattenStart)
            {
                normalizedStart = new Vector2(
                    Mathf.InverseLerp(terrain.transform.position.x, terrain.transform.position.x + size.x, startPoint.position.x),
                    Mathf.InverseLerp(terrain.transform.position.z, terrain.transform.position.z + size.z, startPoint.position.z));
            }

            for (int z = 0; z < resolution; z++)
            {
                float nz = z / (float)(resolution - 1);
                for (int x = 0; x < resolution; x++)
                {
                    float nx = x / (float)(resolution - 1);

                    float broad = Fractal(nx, nz, broadOffset, broadHillFrequency, 3, .50f);
                    float medium = Fractal(nx, nz, mediumOffset, mediumFrequency, 2, .48f);
                    float fine = Fractal(nx, nz, fineOffset, fineFrequency, 2, .46f);

                    float ridgeNoise = Mathf.PerlinNoise(
                        ridgeOffset.x + nx * ridgeFrequency,
                        ridgeOffset.y + nz * ridgeFrequency);
                    float ridge = 1f - Mathf.Abs(ridgeNoise * 2f - 1f);
                    ridge *= ridge;

                    float depressionNoise = Mathf.PerlinNoise(
                        depressionOffset.x + nx * depressionFrequency,
                        depressionOffset.y + nz * depressionFrequency);
                    float depression = Mathf.InverseLerp(depressionThreshold, 1f, depressionNoise);
                    depression = depression * depression * depressionStrength;

                    float h = baseElevation;
                    h += (broad - .5f) * broadHillAmplitude;
                    h += (medium - .5f) * mediumAmplitude;
                    h += (fine - .5f) * fineAmplitude;
                    h += (ridge - .5f) * ridgeAmplitude;
                    h -= depression;
                    h = Mathf.Clamp01(h);
                    heights[z, x] = h;
                }
            }

            if (flattenStart)
            {
                int sx = Mathf.Clamp(Mathf.RoundToInt(normalizedStart.x * (resolution - 1)), 0, resolution - 1);
                int sz = Mathf.Clamp(Mathf.RoundToInt(normalizedStart.y * (resolution - 1)), 0, resolution - 1);
                centerStartHeight = heights[sz, sx];
                ApplyStartFlatten(heights, resolution, size, normalizedStart, centerStartHeight);
            }

            data.SetHeights(0, 0, heights);
            terrain.Flush();
        }

        private void ApplyStartFlatten(float[,] heights, int resolution, Vector3 terrainSize, Vector2 normalizedStart, float targetHeight)
        {
            float outerRadius = startFlattenRadius + startBlendWidth;
            for (int z = 0; z < resolution; z++)
            {
                float nz = z / (float)(resolution - 1);
                float dz = (nz - normalizedStart.y) * terrainSize.z;
                for (int x = 0; x < resolution; x++)
                {
                    float nx = x / (float)(resolution - 1);
                    float dx = (nx - normalizedStart.x) * terrainSize.x;
                    float distance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distance >= outerRadius) continue;

                    float keepRelief = distance <= startFlattenRadius
                        ? 0f
                        : Mathf.SmoothStep(0f, 1f, (distance - startFlattenRadius) / Mathf.Max(.01f, startBlendWidth));
                    heights[z, x] = Mathf.Lerp(targetHeight, heights[z, x], keepRelief);
                }
            }
        }

        private static float Fractal(float x, float z, Vector2 offset, float frequency, int octaves, float persistence)
        {
            float amplitude = 1f;
            float total = 0f;
            float normalization = 0f;
            float f = frequency;
            for (int i = 0; i < octaves; i++)
            {
                total += Mathf.PerlinNoise(offset.x + x * f, offset.y + z * f) * amplitude;
                normalization += amplitude;
                amplitude *= persistence;
                f *= 2.03f;
            }
            return normalization > 0f ? total / normalization : .5f;
        }

        private static Vector2 Offset(System.Random rng) => new(
            Mathf.Lerp(-8000f, 8000f, (float)rng.NextDouble()),
            Mathf.Lerp(-8000f, 8000f, (float)rng.NextDouble()));

        private static int ClosestValidHeightmapResolution(int requested)
        {
            // Unity heightmaps use 2^n + 1. Keep this bounded for Android-oriented world baking.
            int[] valid = { 33, 65, 129, 257, 513, 1025, 2049 };
            int best = valid[0];
            int bestDelta = Mathf.Abs(requested - best);
            for (int i = 1; i < valid.Length; i++)
            {
                int delta = Mathf.Abs(requested - valid[i]);
                if (delta >= bestDelta) continue;
                best = valid[i];
                bestDelta = delta;
            }
            return best;
        }
    }
}
