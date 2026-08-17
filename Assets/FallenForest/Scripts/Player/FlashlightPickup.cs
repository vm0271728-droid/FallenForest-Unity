using System.Collections;
using UnityEngine;

namespace FallenForest.Player
{
    [RequireComponent(typeof(Collider))]
    public sealed class FlashlightPickup : MonoBehaviour
    {
        [SerializeField] private GameObject outlineVisual;
        [SerializeField] private AudioClip pickupSound;
        [SerializeField] private float pickupDuration = .72f;
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
            float moveDuration = pickupDuration * .76f;

            while (elapsed < moveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / Mathf.Max(.05f, moveDuration));
                float eased = Mathf.SmoothStep(0f, 1f, p);
                Vector3 target = flashlight.transform.position + flashlight.transform.forward * .035f;
                transform.position = Vector3.Lerp(startPosition, target, eased);
                transform.rotation = Quaternion.Slerp(startRotation, flashlight.transform.rotation, eased);
                transform.localScale = Vector3.Lerp(startScale, startScale * .78f, eased);
                yield return null;
            }

            flashlight.Acquire();
            if (pickupSound != null)
                AudioSource.PlayClipAtPoint(pickupSound, flashlight.transform.position, .75f);
            gameObject.SetActive(false);
        }
    }
}
