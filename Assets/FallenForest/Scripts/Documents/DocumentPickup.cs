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
        [SerializeField] private float pickupDuration = 2.35f;
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
            if (SaveSystem.IsDocumentCollected(documentSlot))
            {
                gameObject.SetActive(false);
                return;
            }

            PlayerMotor player = other.GetComponentInParent<PlayerMotor>();
            if (player == null) return;

            collected = true;
            Collider ownCollider = GetComponent<Collider>();
            if (ownCollider != null) ownCollider.enabled = false;
            if (subtleOutline != null) subtleOutline.SetActive(false);
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
            int gripVariant = Mathf.Abs(documentSlot) % 3;
            bool contactSoundPlayed = false;
            float elapsed = 0f;

            Vector3[] holdOffsets =
            {
                new(-.10f, -.18f, .40f),
                new(-.15f, -.15f, .36f),
                new(-.06f, -.22f, .43f)
            };
            Vector3[] holdEuler =
            {
                new(69f, -7f, 5f),
                new(61f, 7f, -10f),
                new(74f, -15f, 12f)
            };

            while (elapsed < pickupDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / Mathf.Max(.05f, pickupDuration));
                if (camera != null)
                {
                    Vector3 holdTarget = camera.transform.TransformPoint(holdOffsets[gripVariant]);
                    Quaternion holdRotation = camera.transform.rotation * Quaternion.Euler(holdEuler[gripVariant]);

                    if (p < .20f)
                    {
                        // Left hand reaches first; the document remains on its real ground anchor.
                        transform.position = startPosition;
                        transform.rotation = startRotation;
                    }
                    else if (p < .38f)
                    {
                        // Catch one edge first. Each variant tips a different side of the folder.
                        float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.20f, .38f, p));
                        float side = gripVariant == 1 ? -1f : 1f;
                        transform.position = startPosition + Vector3.up * (.025f * u);
                        transform.rotation = startRotation * Quaternion.Euler(6f * u, side * 3f * u, side * 13f * u);
                    }
                    else if (p < .72f)
                    {
                        // Regrip, lift the complete object and bring it close enough to read.
                        float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.38f, .72f, p));
                        transform.position = Vector3.Lerp(startPosition + Vector3.up * .025f, holdTarget, u);
                        transform.rotation = Quaternion.Slerp(startRotation, holdRotation, u);
                        transform.localScale = Vector3.Lerp(startScale, startScale * .78f, u);
                    }
                    else
                    {
                        // Brief close presentation, then lower the real object below the camera.
                        float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.72f, 1f, p));
                        Vector3 lowerTarget = holdTarget - camera.transform.up * .78f - camera.transform.forward * .06f;
                        transform.position = Vector3.Lerp(holdTarget, lowerTarget, u);
                        transform.rotation = Quaternion.Slerp(holdRotation, holdRotation * Quaternion.Euler(18f, 0f, gripVariant == 2 ? 7f : -4f), u);
                    }
                }

                if (!contactSoundPlayed && p >= .24f)
                {
                    contactSoundPlayed = true;
                    if (pickupSound != null)
                        AudioSource.PlayClipAtPoint(pickupSound, transform.position, .85f);
                }

                yield return null;
            }

            // Progress/save/HUD update only after the physical pickup has visually completed.
            if (GameProgress.Instance != null && GameProgress.Instance.CollectDocument(documentSlot))
            {
                SaveSystem.SavePlayerPosition(player.transform.position);
                HUDController.Instance?.ShowDocumentCount(GameProgress.Instance.DocumentsCollected);
            }
            gameObject.SetActive(false);
        }
    }
}
