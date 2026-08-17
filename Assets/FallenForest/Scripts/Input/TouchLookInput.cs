using FallenForest.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FallenForest.Input
{
    public sealed class TouchLookInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private FlashlightController flashlight;
        [SerializeField] private float tapMaxDuration = .22f;
        [SerializeField] private float tapMaxTravel = 34f;
        [SerializeField] private float doubleTapWindow = .32f;

        private bool active;
        private int activePointer = int.MinValue;
        private Vector2 accumulatedDelta;
        private Vector2 pointerDownPosition;
        private float pointerDownTime;
        private float lastTapTime = -10f;

        public Vector2 ConsumeDelta()
        {
            Vector2 d = accumulatedDelta;
            accumulatedDelta = Vector2.zero;
            return d;
        }

        public void SetFlashlight(FlashlightController controller) => flashlight = controller;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.position.x < Screen.width * .5f || active) return;
            active = true;
            activePointer = e.pointerId;
            pointerDownPosition = e.position;
            pointerDownTime = Time.unscaledTime;
        }

        public void OnDrag(PointerEventData e)
        {
            if (active && e.pointerId == activePointer)
                accumulatedDelta += e.delta;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!active || e.pointerId != activePointer) return;

            float duration = Time.unscaledTime - pointerDownTime;
            float travel = Vector2.Distance(pointerDownPosition, e.position);
            bool tap = duration <= tapMaxDuration && travel <= tapMaxTravel;
            if (tap)
            {
                float now = Time.unscaledTime;
                if (now - lastTapTime <= doubleTapWindow)
                {
                    flashlight?.Toggle();
                    lastTapTime = -10f;
                }
                else
                {
                    lastTapTime = now;
                }
            }

            active = false;
            activePointer = int.MinValue;
            accumulatedDelta = Vector2.zero;
        }
    }
}
