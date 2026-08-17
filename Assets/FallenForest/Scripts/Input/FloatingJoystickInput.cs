using UnityEngine;
using UnityEngine.EventSystems;

namespace FallenForest.Input
{
    public sealed class FloatingJoystickInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform baseRing;
        [SerializeField] private RectTransform knob;
        [SerializeField] private CanvasGroup visualGroup;
        [SerializeField] private float radius = 90f;
        [SerializeField] private float fadeSpeed = 16f;
        private RectTransform rect;
        private Vector2 origin;
        private bool active;
        private int activePointer = int.MinValue;
        public Vector2 Value { get; private set; }

        private void Awake()
        {
            rect = transform as RectTransform;
            if (visualGroup != null) visualGroup.alpha = 0f;
            if (baseRing != null) baseRing.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (visualGroup == null) return;
            visualGroup.alpha = Mathf.MoveTowards(visualGroup.alpha, active ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
            if (!active && visualGroup.alpha <= 0.01f && baseRing != null) baseRing.gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.position.x > Screen.width * 0.5f || active) return;
            active = true; activePointer = e.pointerId;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out origin);
            if (baseRing != null) { baseRing.gameObject.SetActive(true); baseRing.anchoredPosition = origin; }
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            Value = Vector2.zero;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!active || e.pointerId != activePointer) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out Vector2 current);
            Value = Vector2.ClampMagnitude((current - origin) / Mathf.Max(radius, 1f), 1f);
            if (knob != null) knob.anchoredPosition = Value * radius;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!active || e.pointerId != activePointer) return;
            active = false; activePointer = int.MinValue; Value = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
        }
    }
}
