using System.Collections;
using UnityEngine;

namespace FallenForest.Player
{
    [RequireComponent(typeof(Collider))]
    public sealed class FlashlightPickup : MonoBehaviour
    {
        [SerializeField] private GameObject outlineVisual;
        [SerializeField] private AudioClip pickupSound;
        [SerializeField] private float pickupDuration = 2.55f;
        [SerializeField] private float powerOnDelay = .075f;
        private bool consumed;

        private void OnTriggerEnter(Collider other)
        {
            if (consumed) return;
            FlashlightController flashlight = other.GetComponentInChildren<FlashlightController>();
            if (flashlight == null) flashlight = other.GetComponent<FlashlightController>();
            if (flashlight == null) flashlight = FindFirstObjectByType<FlashlightController>();
            if (flashlight == null || flashlight.Acquired) return;
            consumed = true;
            StartCoroutine(Pickup(flashlight));
        }

        private IEnumerator Pickup(FlashlightController flashlight)
        {
            if (outlineVisual != null) outlineVisual.SetActive(false);
            Collider ownCollider = GetComponent<Collider>();
            if (ownCollider != null) ownCollider.enabled = false;

            ViewmodelMotionController viewmodel = FindFirstObjectByType<ViewmodelMotionController>();
            if (viewmodel != null)
                StartCoroutine(viewmodel.PlayFlashlightPickup(pickupDuration));

            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            bool clicked = false;
            bool acquired = false;

            while (elapsed < pickupDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / Mathf.Max(.05f, pickupDuration));
                Vector3 handTarget = flashlight.transform.position + flashlight.transform.forward * .025f;
                Quaternion handRotation = flashlight.transform.rotation;

                // 0.00-0.22: hand reaches; the real world object stays where it is.
                // 0.22-0.36: first contact gives the flashlight a small believable nudge.
                // 0.36-0.76: fingers close and the object is lifted into the hand.
                // 0.76-0.90: wrist settles and grip is corrected.
                // 0.90+: thumb clicks; light comes on after a tiny electrical delay.
                if (p < .22f)
                {
                    transform.position = startPosition;
                    transform.rotation = startRotation;
                }
                else if (p < .36f)
                {
                    float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.22f, .36f, p));
                    Vector3 nudge = new(.035f, .014f, -.025f);
                    transform.position = startPosition + transform.TransformDirection(nudge) * u;
                    transform.rotation = startRotation * Quaternion.Euler(0f, 0f, 7f * u);
                }
                else
                {
                    float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.36f, .82f, p));
                    Vector3 nudgedStart = startPosition + transform.TransformDirection(new Vector3(.035f, .014f, -.025f));
                    transform.position = Vector3.Lerp(nudgedStart, handTarget, u);
                    transform.rotation = Quaternion.Slerp(startRotation * Quaternion.Euler(0f, 0f, 7f), handRotation, u);
                    transform.localScale = Vector3.Lerp(startScale, startScale * .82f, u);
                }

                if (!clicked && p >= .90f)
                {
                    clicked = true;
                    if (pickupSound != null)
                        AudioSource.PlayClipAtPoint(pickupSound, flashlight.transform.position, .82f);
                    StartCoroutine(PowerOnAfterDelay(flashlight));
                }

                if (!acquired && p >= .90f + powerOnDelay / Mathf.Max(.05f, pickupDuration))
                {
                    acquired = true;
                    flashlight.Acquire();
                }

                yield return null;
            }

            if (!acquired) flashlight.Acquire();
            gameObject.SetActive(false);
        }

        private static IEnumerator PowerOnAfterDelay(FlashlightController flashlight)
        {
            // Acquire is intentionally performed by the main pickup timeline so the held visual and
            // Light appear together. This short yield preserves a perceptible click->light beat.
            yield return null;
            if (flashlight != null && flashlight.Acquired)
                flashlight.SetOn(true);
        }
    }
}
