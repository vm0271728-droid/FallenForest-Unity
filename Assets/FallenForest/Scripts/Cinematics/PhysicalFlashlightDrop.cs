using FallenForest.Player;
using UnityEngine;

namespace FallenForest.Cinematics
{
    /// <summary>
    /// Creates a world-space, physics-driven copy of the exact supplied flashlight visual and beam.
    /// Used by Locust deaths so the flashlight can bounce/roll/sweep the forest while remaining ON.
    /// </summary>
    public static class PhysicalFlashlightDrop
    {
        public static Rigidbody Drop(
            FlashlightController controller,
            Vector3 linearVelocity,
            Vector3 angularVelocity,
            string objectName = "DroppedFlashlight")
        {
            if (controller == null || !controller.Acquired || controller.VisualRoot == null)
                return null;

            // Death choreography always requires a live beam, even if the player had manually
            // toggled the flashlight off immediately before the attack.
            controller.SetOn(true);

            GameObject sourceVisual = controller.VisualRoot;
            Transform sourceTransform = sourceVisual.transform;
            GameObject root = new(objectName);
            root.transform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);

            GameObject visual = Object.Instantiate(sourceVisual);
            visual.name = "Visual";
            visual.SetActive(true);
            visual.transform.SetParent(root.transform, true);
            SetLayerRecursively(visual.transform, 0);

            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                Object.Destroy(collider);
            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
                Object.Destroy(body);

            Bounds bounds = CalculateBounds(visual.GetComponentsInChildren<Renderer>(true));
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = root.transform.InverseTransformVector(bounds.size);
            box.size = new Vector3(
                Mathf.Max(.07f, Mathf.Abs(localSize.x) * .86f),
                Mathf.Max(.07f, Mathf.Abs(localSize.y) * .86f),
                Mathf.Max(.18f, Mathf.Abs(localSize.z) * .86f));

            CopyLight(controller.Light, root.transform);

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = .34f;
            rb.linearDamping = .08f;
            rb.angularDamping = .11f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearVelocity = linearVelocity;
            rb.angularVelocity = angularVelocity;

            controller.HideHeldAfterPhysicalDrop();
            return rb;
        }

        private static void CopyLight(Light source, Transform parent)
        {
            if (source == null) return;
            GameObject beamObject = new("DroppedBeam");
            beamObject.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            beamObject.transform.SetParent(parent, true);
            Light beam = beamObject.AddComponent<Light>();
            beam.type = source.type;
            beam.color = source.color;
            beam.intensity = source.intensity;
            beam.range = source.range;
            beam.spotAngle = source.spotAngle;
            beam.innerSpotAngle = source.innerSpotAngle;
            beam.shadows = source.shadows;
            beam.shadowStrength = source.shadowStrength;
            beam.shadowBias = source.shadowBias;
            beam.shadowNormalBias = source.shadowNormalBias;
            beam.shadowNearPlane = source.shadowNearPlane;
            beam.cookie = source.cookie;
            beam.cookieSize = source.cookieSize;
            beam.renderMode = source.renderMode;
            beam.cullingMask = source.cullingMask;
            beam.useColorTemperature = source.useColorTemperature;
            beam.colorTemperature = source.colorTemperature;
            beam.enabled = true;
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
                return new Bounds(Vector3.zero, new Vector3(.10f, .10f, .30f));
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
                SetLayerRecursively(child, layer);
        }
    }
}
