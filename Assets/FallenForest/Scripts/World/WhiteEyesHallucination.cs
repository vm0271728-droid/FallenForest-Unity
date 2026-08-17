using System.Collections;
using FallenForest.Player;
using UnityEngine;

namespace FallenForest.World
{
    /// <summary>
    /// Rare non-hostile hallucination: two distant white eyes, no body, collider or gameplay effect.
    /// It only appears after a long interval while the player is actively moving forward.
    /// </summary>
    public sealed class WhiteEyesHallucination : MonoBehaviour
    {
        [SerializeField] private PlayerMotor player;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Vector2 intervalSeconds = new(300f, 480f);
        [SerializeField] private Vector2 distanceRange = new(30f, 44f);
        [SerializeField] private Vector2 visibleDuration = new(.55f, 1.05f);
        [SerializeField] private float minimumForwardSpeed = .55f;
        [SerializeField] private float eyeSeparation = .24f;
        [SerializeField] private Vector2 eyeScale = new(.055f, .085f);
        [SerializeField] private LayerMask groundMask = ~0;

        private float nextEligibleTime;
        private Coroutine activeRoutine;
        private Material eyeMaterial;

        private void Awake()
        {
            ResolveReferences();
            ScheduleNext();
        }

        private void Update()
        {
            ResolveReferences();
            if (activeRoutine != null || player == null || playerCamera == null || Time.unscaledTime < nextEligibleTime)
                return;
            if (!IsMovingForward())
                return;

            activeRoutine = StartCoroutine(ShowEyes());
        }

        private void OnDestroy()
        {
            if (eyeMaterial != null)
                Destroy(eyeMaterial);
        }

        private void ResolveReferences()
        {
            if (player == null) player = FindFirstObjectByType<PlayerMotor>();
            if (playerCamera == null) playerCamera = Camera.main;
        }

        private bool IsMovingForward()
        {
            if (!player.IsMoving) return false;
            Vector3 forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .001f) return false;
            return Vector3.Dot(player.PlanarVelocity, forward) >= minimumForwardSpeed;
        }

        private IEnumerator ShowEyes()
        {
            Vector3 forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
            float distance = Random.Range(distanceRange.x, distanceRange.y);
            Vector3 desired = playerCamera.transform.position + forward * distance;

            float baseY = desired.y - 1.4f;
            Vector3 probe = desired + Vector3.up * 20f;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 50f, groundMask, QueryTriggerInteraction.Ignore))
                baseY = hit.point.y;

            Vector3 eyeCentre = new(desired.x, baseY + Random.Range(1.55f, 2.15f), desired.z);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            GameObject root = new("WhiteEyes_Hallucination");
            root.transform.position = eyeCentre;
            root.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);

            Material material = GetEyeMaterial();
            CreateEye(root.transform, "Eye_L", -right * (eyeSeparation * .5f), material);
            CreateEye(root.transform, "Eye_R", right * (eyeSeparation * .5f), material);

            yield return new WaitForSecondsRealtime(Random.Range(visibleDuration.x, visibleDuration.y));

            if (root != null) Destroy(root);
            activeRoutine = null;
            ScheduleNext();
        }

        private void CreateEye(Transform parent, string name, Vector3 worldOffset, Material material)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = name;
            eye.transform.SetParent(parent, true);
            eye.transform.position = parent.position + worldOffset;
            float scale = Random.Range(eyeScale.x, eyeScale.y);
            eye.transform.localScale = new Vector3(scale * 1.45f, scale, scale * .35f);
            Collider collider = eye.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = eye.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private Material GetEyeMaterial()
        {
            if (eyeMaterial != null) return eyeMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            eyeMaterial = new Material(shader) { name = "WhiteEyes_Runtime" };
            Color white = new(1f, 1f, 1f, 1f);
            if (eyeMaterial.HasProperty("_BaseColor")) eyeMaterial.SetColor("_BaseColor", white);
            if (eyeMaterial.HasProperty("_Color")) eyeMaterial.SetColor("_Color", white);
            return eyeMaterial;
        }

        private void ScheduleNext()
        {
            nextEligibleTime = Time.unscaledTime + Random.Range(intervalSeconds.x, intervalSeconds.y);
        }
    }
}
