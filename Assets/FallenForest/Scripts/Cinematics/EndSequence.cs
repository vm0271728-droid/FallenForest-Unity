using System.Collections;
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
        [SerializeField] private GameObject car;
        [SerializeField] private AudioSource carAudio;
        [SerializeField] private AudioDirector audioDirector;
        [SerializeField] private CanvasGroup fade;
        [SerializeField] private Text endText;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private float escapeRunTime = 2.35f;
        [SerializeField] private float carPassTime = 1.65f;
        [SerializeField] private float sitTime = 1.8f;

        private bool started;
        private GameObject runtimeRoad;

        public void Begin(PlayerMotor player, Vector3 boundaryPoint, Vector3 outwardDirection)
        {
            if (started || player == null) return;
            StartCoroutine(Run(player, boundaryPoint, outwardDirection));
        }

        public void Begin(PlayerMotor player)
        {
            Vector3 outward = player != null
                ? Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized
                : Vector3.forward;
            Begin(player, player != null ? player.transform.position : transform.position, outward);
        }

        private IEnumerator Run(PlayerMotor player, Vector3 boundaryPoint, Vector3 outward)
        {
            started = true;

            // Once the boundary ending begins, the final chase has done its job. Remove any remaining
            // Locust instances before locking player controls so an attack cannot interrupt the ending.
            LocustAI[] remainingLocusts = new LocustAI[MonsterRegistry.Locusts.Count];
            MonsterRegistry.Locusts.CopyTo(remainingLocusts);
            for (int i = 0; i < remainingLocusts.Length; i++)
                if (remainingLocusts[i] != null)
                    Destroy(remainingLocusts[i].gameObject);

            outward = Vector3.ProjectOnPlane(outward, Vector3.up).normalized;
            if (outward.sqrMagnitude < .001f) outward = Vector3.forward;
            Vector3 tangent = Vector3.Cross(Vector3.up, outward).normalized;

            CameraMotion cam = FindFirstObjectByType<CameraMotion>();
            player.SetControlsEnabled(false);
            cam?.SetInputEnabled(false);
            cam?.AddShake(.055f, 3.2f);
            HUDController.Instance?.ShowMessage("БЕГИ");

            Vector3 edge = Ground(boundaryPoint + outward * 1.2f);
            Vector3 forestExit = Ground(edge + outward * 8.5f);
            Vector3 roadside = Ground(edge + outward * 16f);
            Vector3 sitting = Ground(edge + outward * 18.3f - tangent * .7f);
            BuildRoad(roadside, tangent);

            Transform actor = player.transform;
            yield return MoveActor(actor, forestExit, escapeRunTime, outward);
            yield return MoveActor(actor, roadside, .55f, outward);

            if (car != null)
            {
                Vector3 start = Ground(roadside - tangent * 31f) + Vector3.up * .72f;
                Vector3 end = Ground(roadside + tangent * 31f) + Vector3.up * .72f;
                car.SetActive(true);
                car.transform.SetPositionAndRotation(start, Quaternion.LookRotation(tangent));
                carAudio?.Play();

                float t = 0f;
                while (t < carPassTime)
                {
                    t += Time.deltaTime;
                    car.transform.position = Vector3.Lerp(
                        start,
                        end,
                        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / carPassTime)));
                    yield return null;
                }

                carAudio?.Stop();
                car.SetActive(false);
            }

            // After the vehicle moment, leave the flashlight behind as a final visual: still lit,
            // lying near the player and pointing back into the forest they just escaped.
            FlashlightController flashlight = FindFirstObjectByType<FlashlightController>();
            if (flashlight != null && flashlight.Acquired)
            {
                Vector3 lampPosition = Ground(actor.position + tangent * .38f - outward * .18f);
                flashlight.PlaceForEnding(lampPosition, -outward);
            }

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
                endText.text = "КОНЕЦ";
                endText.gameObject.SetActive(true);
            }

            yield return new WaitForSecondsRealtime(4f);
            SceneFlow.MainMenu();
        }

        private Vector3 Ground(Vector3 p)
        {
            Terrain active = terrain != null ? terrain : Terrain.activeTerrain;
            if (active == null) return p;
            p.y = active.SampleHeight(p) + active.transform.position.y + .03f;
            return p;
        }

        private void BuildRoad(Vector3 centre, Vector3 tangent)
        {
            if (runtimeRoad != null) Destroy(runtimeRoad);
            runtimeRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            runtimeRoad.name = "FinalRoad_Runtime";
            runtimeRoad.transform.position = centre - Vector3.up * .09f;
            runtimeRoad.transform.rotation = Quaternion.LookRotation(tangent);
            runtimeRoad.transform.localScale = new Vector3(8.8f, .16f, 72f);
            Collider c = runtimeRoad.GetComponent<Collider>();
            if (c != null) c.enabled = false;
            Renderer r = runtimeRoad.GetComponent<Renderer>();
            if (r != null && roadMaterial != null) r.sharedMaterial = roadMaterial;
        }

        private IEnumerator MoveActor(Transform actor, Vector3 target, float duration, Vector3 face)
        {
            Vector3 start = actor.position;
            Quaternion startRotation = actor.rotation;
            Quaternion endRotation = face.sqrMagnitude > .001f ? Quaternion.LookRotation(face) : startRotation;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(.01f, duration)));
                actor.position = Vector3.Lerp(start, target, u);
                actor.rotation = Quaternion.Slerp(startRotation, endRotation, u);
                yield return null;
            }
        }

        private IEnumerator RotateActor(Transform actor, Quaternion target, float duration)
        {
            Quaternion start = actor.rotation;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                actor.rotation = Quaternion.Slerp(
                    start,
                    target,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
                yield return null;
            }
        }
    }
}
