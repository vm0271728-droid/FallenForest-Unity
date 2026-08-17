using FallenForest.World;
using UnityEngine;
namespace FallenForest.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class BoundaryContact : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        private void Awake() { if (motor == null) motor = GetComponent<PlayerMotor>(); }
        private void OnControllerColliderHit(ControllerColliderHit hit) { InvisibleBoundary boundary = hit.collider.GetComponent<InvisibleBoundary>(); if (boundary != null) boundary.Contact(motor, hit.point); }
    }
}
