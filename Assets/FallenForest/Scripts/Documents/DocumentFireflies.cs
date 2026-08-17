using UnityEngine;
using UnityEngine.Rendering;

namespace FallenForest.Documents
{
    /// <summary>
    /// Tiny, deliberately dim fireflies used only as an atmospheric detail above some documents.
    /// They are not a quest beacon: the cluster culls at distance and uses only a very weak local light.
    /// </summary>
    public sealed class DocumentFireflies : MonoBehaviour
    {
        [SerializeField, Range(4, 6)] private int count = 5;
        [SerializeField] private float visibleDistance = 15f;
        [SerializeField] private Vector2 heightRange = new(.18f, .48f);
        [SerializeField] private Vector2 radiusRange = new(.10f, .34f);
        [SerializeField] private Vector2 sizeRange = new(.010f, .020f);
        [SerializeField] private float driftSpeed = .48f;
        [SerializeField] private float verticalBob = .035f;
        [SerializeField] private float pointLightIntensity = .025f;
        [SerializeField] private float pointLightRange = .55f;

        private Transform[] flies;
        private MeshRenderer[] renderers;
        private Vector3[] baseOffsets;
        private float[] phases;
        private float[] speeds;
        private Material material;
        private Light clusterLight;
        private Camera cachedCamera;
        private float nextCameraLookup;

        public void Configure(int deterministicSeed, int requestedCount)
        {
            count = Mathf.Clamp(requestedCount, 4, 6);
            Build(deterministicSeed);
        }

        private void Build(int deterministicSeed)
        {
            ClearChildren();

            var rng = new System.Random(deterministicSeed);
            flies = new Transform[count];
            renderers = new MeshRenderer[count];
            baseOffsets = new Vector3[count];
            phases = new float[count];
            speeds = new float[count];

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = shader != null ? new Material(shader) : null;
            if (material != null)
            {
                material.name = "DocumentFirefly_Runtime";
                Color dimWarm = new(0.68f, 0.78f, 0.30f, 0.82f);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", dimWarm);
                if (material.HasProperty("_Color")) material.SetColor("_Color", dimWarm);
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", new Color(.20f, .25f, .06f, 1f));
            }

            for (int i = 0; i < count; i++)
            {
                GameObject fly = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fly.name = $"Firefly_{i + 1:00}";
                fly.transform.SetParent(transform, false);

                Collider col = fly.GetComponent<Collider>();
                if (col != null) Destroy(col);

                float angle = Next(rng, 0f, Mathf.PI * 2f);
                float radius = Next(rng, radiusRange.x, radiusRange.y);
                baseOffsets[i] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Next(rng, heightRange.x, heightRange.y),
                    Mathf.Sin(angle) * radius);
                phases[i] = Next(rng, 0f, Mathf.PI * 2f);
                speeds[i] = Next(rng, .78f, 1.28f);

                float size = Next(rng, sizeRange.x, sizeRange.y);
                fly.transform.localScale = Vector3.one * size;
                fly.transform.localPosition = baseOffsets[i];
                flies[i] = fly.transform;

                MeshRenderer renderer = fly.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                if (material != null) renderer.sharedMaterial = material;
                renderers[i] = renderer;
            }

            GameObject lightObject = new("Very Dim Firefly Glow", typeof(Light));
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = Vector3.up * .26f;
            clusterLight = lightObject.GetComponent<Light>();
            clusterLight.type = LightType.Point;
            clusterLight.color = new Color(.68f, .78f, .30f);
            clusterLight.intensity = pointLightIntensity;
            clusterLight.range = pointLightRange;
            clusterLight.shadows = LightShadows.None;
            clusterLight.renderMode = LightRenderMode.Auto;
        }

        private void Update()
        {
            if (flies == null || flies.Length == 0) return;

            if (cachedCamera == null && Time.unscaledTime >= nextCameraLookup)
            {
                cachedCamera = Camera.main;
                nextCameraLookup = Time.unscaledTime + 1f;
            }

            bool visible = true;
            if (cachedCamera != null)
            {
                float maxSq = visibleDistance * visibleDistance;
                visible = (cachedCamera.transform.position - transform.position).sqrMagnitude <= maxSq;
            }

            SetVisible(visible);
            if (!visible) return;

            float time = Time.unscaledTime * driftSpeed;
            for (int i = 0; i < flies.Length; i++)
            {
                float t = time * speeds[i] + phases[i];
                Vector3 offset = baseOffsets[i];
                offset.x += Mathf.Sin(t * .83f) * .045f + Mathf.Sin(t * 1.91f) * .012f;
                offset.z += Mathf.Cos(t * .71f) * .040f + Mathf.Sin(t * 1.37f) * .014f;
                offset.y += Mathf.Sin(t * 1.43f) * verticalBob;
                flies[i].localPosition = offset;
            }
        }

        private void SetVisible(bool visible)
        {
            if (renderers != null)
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null && renderers[i].enabled != visible)
                        renderers[i].enabled = visible;

            if (clusterLight != null && clusterLight.enabled != visible)
                clusterLight.enabled = visible;
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        private static float Next(System.Random rng, float min, float max) =>
            Mathf.Lerp(min, max, (float)rng.NextDouble());
    }
}
