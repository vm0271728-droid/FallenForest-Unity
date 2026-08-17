using UnityEngine;
using UnityEngine.EventSystems;

namespace FallenForest.Input
{
    public sealed class TouchLookInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private bool active;
        private int activePointer = int.MinValue;
        private Vector2 accumulatedDelta;
        public Vector2 ConsumeDelta() { Vector2 d = accumulatedDelta; accumulatedDelta = Vector2.zero; return d; }
        public void OnPointerDown(PointerEventData e) { if (e.position.x < Screen.width * 0.5f || active) return; active = true; activePointer = e.pointerId; }
        public void OnDrag(PointerEventData e) { if (active && e.pointerId == activePointer) accumulatedDelta += e.delta; }
        public void OnPointerUp(PointerEventData e) { if (!active || e.pointerId != activePointer) return; active = false; activePointer = int.MinValue; accumulatedDelta = Vector2.zero; }
    }
}
