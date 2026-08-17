using System.Collections;
using UnityEngine;
namespace FallenForest.Player
{
    [RequireComponent(typeof(Collider))]
    public sealed class FlashlightPickup : MonoBehaviour
    {
        [SerializeField] private GameObject outlineVisual;
        [SerializeField] private AudioClip pickupSound;
        [SerializeField] private float pickupDelay = .2f;
        private bool consumed;
        private void OnTriggerEnter(Collider other)
        {
            if (consumed) return;
            FlashlightController flashlight = other.GetComponentInChildren<FlashlightController>(); if (flashlight == null) flashlight = other.GetComponent<FlashlightController>(); if (flashlight == null) return;
            consumed = true; StartCoroutine(Pickup(flashlight));
        }
        private IEnumerator Pickup(FlashlightController flashlight) { if (outlineVisual != null) outlineVisual.SetActive(false); yield return new WaitForSeconds(pickupDelay); flashlight.Acquire(); if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, transform.position, .75f); gameObject.SetActive(false); }
    }
}
