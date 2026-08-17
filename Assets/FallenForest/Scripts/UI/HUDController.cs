using System.Collections;
using FallenForest.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FallenForest.UI
{
    public sealed class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }
        [SerializeField] private CanvasGroup messageGroup;
        [SerializeField] private Text messageText;
        [SerializeField] private float displayTime = 1.8f;
        private Coroutine routine;

        private void Awake() => Instance = this;

        public void ShowDocumentCount(int count) =>
            ShowMessage($"{LocalizationSettings.Text("documents")}\n{count} / {GameProgress.RequiredDocuments}");

        public void ShowMessage(string text)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(ShowRoutine(text));
        }

        private IEnumerator ShowRoutine(string text)
        {
            if (messageText != null) messageText.text = text;
            if (messageGroup != null) messageGroup.alpha = 0f;
            float t = 0f;
            while (t < .2f)
            {
                t += Time.unscaledDeltaTime;
                if (messageGroup != null) messageGroup.alpha = Mathf.Clamp01(t / .2f);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(displayTime);
            t = 0f;
            while (t < .35f)
            {
                t += Time.unscaledDeltaTime;
                if (messageGroup != null) messageGroup.alpha = 1f - Mathf.Clamp01(t / .35f);
                yield return null;
            }
        }
    }
}
