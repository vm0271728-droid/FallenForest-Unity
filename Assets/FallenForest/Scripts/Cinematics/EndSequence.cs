using System.Collections;
using System.Collections.Generic;
using FallenForest.Audio;
using FallenForest.Core;
using FallenForest.Monsters;
using FallenForest.Player;
using FallenForest.UI;
using UnityEngine;
using UnityEngine.UI;

namespace FallenForest.Cinematics
{
    public sealed class EndSequence : MonoBehaviour
    {
        [SerializeField] private Terrain terrain;
        [SerializeField] private CinematicPickupVehicle pickup;
        [SerializeField] private AudioSource pickupAudio;
        [SerializeField] private AudioDirector audioDirector;
        [SerializeField] private CanvasGroup fade;
        [SerializeField] private Text endText;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private float escapeRunTime = 2.35f;
        [SerializeField] private float sitTime = 1.8f;
        [SerializeField] private float vehicleTimeout = 12f;

        private bool started;
        private GameObject runtimeRoad;
        private GameObject routeRoot;

        public void Begin(PlayerMotor player, Vector3 boundaryPoint, Vector3 outwardDirection)
        {
            if (started || player == null) return;
            StartCoroutine(Run(player, boundaryPoint, outwardDirection));
        }

        public void Begin(PlayerMotor player)
        {
            Vector3 o = player != null ? Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized : Vector3.forward;
            Begin(player, player != null ? player.transform.position : transform.position, o);
        }

        private IEnumerator Run(PlayerMotor player, Vector3 boundaryPoint, Vector3 outward)
        {
            started = true;
            outward = Vector3.ProjectOnPlane(outward, Vector3.up).normalized;
            if (outward.sqrMagnitude < .001f) outward = Vector3.forward;
            Vector3 tangent = Vector3.Cross(Vector3.up, outward).normalized;

            // Ending is now truly committed: no Locust may kill the player after controls are taken.
            MonsterDirector director = FindFirstObjectByType<MonsterDirector>();
            if (director != null) director.enabled = false;
            foreach (LocustAI locust in new List<LocustAI>(MonsterRegistry.Locusts))
                if (locust != null) Destroy(locust.gameObject);

            CameraMotion cam = FindFirstObjectByType<CameraMotion>();
            player.SetControlsEnabled(false);
            cam?.SetInputEnabled(false);
            cam?.AddShake(.055f, 3.2f);
            HUDController.Instance?.ShowMessage(LocalizationSettings.Text("run"));

            float edgeY = boundaryPoint.y;
            Vector3 edge = boundaryPoint + outward * 1.2f;
            edge.y = edgeY;
            Vector3 forestExit = edge + outward * 8.5f;
            Vector3 roadside = edge + outward * 16f;
            Vector3 sitting = edge + outward * 18.3f - tangent * .7f;
            forestExit.y = roadside.y = sitting.y = edgeY;

            BuildRoad(roadside, tangent, edgeY);

            Transform actor = player.transform;
            yield return MoveActor(actor, forestExit, escapeRunTime, outward);
            yield return MoveActor(actor, roadside, .55f, outward);

            DetachFlashlightTowardForest(actor, outward);
            yield return DrivePhysicalPickup(roadside, tangent, edgeY);

            if (audioDirector == null) audioDirector = FindFirstObjectByType<AudioDirector>();
            audioDirector?.HardSilence();
            yield return new WaitForSecondsRealtime(1.05f);
            yield return RotateActor(actor, Quaternion.LookRotation(-outward), 1.15f);
            yield return new WaitForSecondsRealtime(.65f);
            yield return MoveActor(actor, sitting, sitTime, -outward);

            if (fade != null)
            {
                fade.blocksRaycasts = true;
                float t = 0f;
                while (t < 2.45f)
                {
                    t += Time.unscaledDeltaTime;
                    fade.alpha = Mathf.Clamp01(t / 2.45f);
                    yield return null;
                }
            }

            if (endText != null)
            {
                endText.text = LocalizationSettings.Text("end");
                endText.gameObject.SetActive(true);
            }
            yield return new WaitForSecondsRealtime(4f);
            SceneFlow.MainMenu();
        }

        private IEnumerator DrivePhysicalPickup(Vector3 roadside, Vector3 tangent, float roadY)
        {
            if (pickup == null) yield break;

            Vector3 start = roadside - tangent * 33f + Vector3.up * .75f;
            pickup.gameObject.SetActive(true);
            Rigidbody rb = pickup.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = start;
                rb.rotation = Quaternion.LookRotation(tangent, Vector3.up);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                pickup.transform.SetPositionAndRotation(start, Quaternion.LookRotation(tangent, Vector3.up));
            }

            Transform[] route = BuildVehicleRoute(roadside, tangent, roadY + .75f);
            pickupAudio?.Play();
            pickup.StartDrive(route);

            float t = 0f;
            while (t < vehicleTimeout && !pickup.HasStopped)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            pickup.StopNow(true);
            pickupAudio?.Stop();
        }

        private Transform[] BuildVehicleRoute(Vector3 centre, Vector3 tangent, float y)
        {
            if (routeRoot != null) Destroy(routeRoot);
            routeRoot = new GameObject("FinalPickupRoute_Runtime");
            float[] offsets = { -18f, -3f, 16f, 31f };
            Transform[] route = new Transform[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject waypoint = new($"Route_{i + 1:00}");
                waypoint.transform.SetParent(routeRoot.transform, false);
                Vector3 p = centre + tangent * offsets[i];
                p.y = y;
                waypoint.transform.position = p;
                route[i] = waypoint.transform;
            }
            return route;
        }

        private void BuildRoad(Vector3 centre, Vector3 tangent, float y)
        {
            if (runtimeRoad != null) Destroy(runtimeRoad);
            runtimeRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            runtimeRoad.name = "FinalRoad_Runtime";
            runtimeRoad.transform.position = new Vector3(centre.x, y - .13f, centre.z);
            runtimeRoad.transform.rotation = Quaternion.LookRotation(tangent);
            runtimeRoad.transform.localScale = new Vector3(8.8f, .22f, 74f);
            Renderer renderer = runtimeRoad.GetComponent<Renderer>();
            if (renderer != null && roadMaterial != null) renderer.sharedMaterial = roadMaterial;
        }

        private static void DetachFlashlightTowardForest(Transform player, Vector3 outward)
        {
            GameObject flashlight = GameObject.Find("Flashlight");
            if (flashlight == null) return;

            flashlight.transform.SetParent(null, true);
            flashlight.transform.position = player.position + Vector3.up * .55f - outward * .35f + player.right * .22f;
            flashlight.transform.rotation = Quaternion.LookRotation(-outward + Vector3.down * .12f, Vector3.up);

            if (flashlight.GetComponent<Collider>() == null)
            {
                BoxCollider box = flashlight.AddComponent<BoxCollider>();
                box.size = new Vector3(.11f, .11f, .32f);
            }
            Rigidbody body = flashlight.GetComponent<Rigidbody>();
            if (body == null) body = flashlight.AddComponent<Rigidbody>();
            body.mass = .32f;
            body.linearDamping = .12f;
            body.angularDamping = .2f;
            body.AddTorque(Random.onUnitSphere * .18f, ForceMode.Impulse);
        }

        private static IEnumerator MoveActor(Transform actor, Vector3 target, float duration, Vector3 face)
        {
            Vector3 start = actor.position;
            Quaternion startRotation = actor.rotation;
            Quaternion endRotation = face.sqrMagnitude > .001f ? Quaternion.LookRotation(face) : startRotation;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(.01f, duration)));
                actor.position = Vector3.Lerp(start, target, u);
                actor.rotation = Quaternion.Slerp(startRotation, endRotation, u);
                yield return null;
            }
        }

        private static IEnumerator RotateActor(Transform actor, Quaternion target, float duration)
        {
            Quaternion start = actor.rotation;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                actor.rotation = Quaternion.Slerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
                yield return null;
            }
        }
    }
}
