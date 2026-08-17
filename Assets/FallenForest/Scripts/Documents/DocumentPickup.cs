using System.Collections;
using FallenForest.Core;
using FallenForest.Player;
using FallenForest.UI;
using UnityEngine;

namespace FallenForest.Documents
{
    [RequireComponent(typeof(Collider))]
    public sealed class DocumentPickup : MonoBehaviour
    {
        [SerializeField] private AudioClip pickupSound;
        [SerializeField] private GameObject subtleOutline;
        [SerializeField] private int documentSlot = -1;
        [SerializeField] private float pickupDuration = .82f;
        private bool collected;

        public int DocumentSlot => documentSlot;

        public void Configure(int slot)
        {
            documentSlot = slot;
            if (SaveSystem.IsDocumentCollected(slot)) gameObject.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected || documentSlot < 0 || GameProgress.Instance == null) return;
            PlayerMotor player = other.GetComponentInParent<PlayerMotor>();
            if (player == null) return;
            if (!GameProgress.Instance.CollectDocument(documentSlot)) return;

            collected = true;
            Collider ownCollider = GetComponent<Collider>();
            if (ownCollider != null) ownCollider.enabled = false;
            if (subtleOutline != null) subtleOutline.SetActive(false);

            SaveSystem.SavePlayerPosition(player.transform.position);
            HUDController.Instance?.ShowDocumentCount(GameProgress.Instance.DocumentsCollected);
            StartCoroutine(PickupRoutine(player));
        }

        private IEnumerator PickupRoutine(PlayerMotor player)
        {
            ViewmodelMotionController viewmodel = FindFirstObjectByType<ViewmodelMotionController>();
            if (viewmodel != null)
                StartCoroutine(viewmodel.PlayDocumentPickup(pickupDuration));

            Camera camera = Camera.main;
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            float moveDuration = pickupDuration * .80f;

            if (pickupSound != null)
                AudioSource.PlayClipAtPoint(pickupSound, transform.position, .85f);

            while (elapsed < moveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / Mathf.Max(.05f, moveDuration));
                float eased = Mathf.SmoothStep(0f, 1f, p);
                if (camera != null)
                {
                    Vector3 target = camera.transform.position + camera.transform.forward * .38f - camera.transform.up * .20f - camera.transform.right * .08f;
                    Quaternion targetRotation = camera.transform.rotation * Quaternion.Euler(68f, -6f, 4f);
                    transform.position = Vector3.Lerp(startPosition, target, eased);
                    transform.rotation = Quaternion.Slerp(startRotation, targetRotation, eased);
                    transform.localScale = Vector3.Lerp(startScale, startScale * .72f, eased);
                }
                yield return null;
            }

            gameObject.SetActive(false);
        }
    }
}
