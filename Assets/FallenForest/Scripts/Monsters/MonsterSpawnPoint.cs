using UnityEngine;
namespace FallenForest.Monsters
{
    public sealed class MonsterSpawnPoint : MonoBehaviour
    {
        public enum SpawnKind { LocustPeek, BoiledOpen }
        [SerializeField] private SpawnKind kind;
        [SerializeField] private Transform coverTransform, hidePoint, peekPoint, retreatPoint;
        [SerializeField] private float coverRadius = .55f, peekExtraOffset = .70f;
        public SpawnKind Kind => kind;
        public Transform CoverTransform => coverTransform != null ? coverTransform : transform;
        public Transform HidePoint => hidePoint != null ? hidePoint : transform;
        public Transform PeekPoint => peekPoint != null ? peekPoint : transform;
        public Transform RetreatPoint => retreatPoint != null ? retreatPoint : HidePoint;
        public Vector3 ReferencePosition => kind == SpawnKind.LocustPeek ? CoverTransform.position : PeekPoint.position;
        public Vector3 GetHiddenPositionFor(Transform player) { if (kind != SpawnKind.LocustPeek || player == null) return HidePoint.position; Vector3 cover = CoverTransform.position; Vector3 to = Vector3.ProjectOnPlane(player.position - cover, Vector3.up).normalized; if (to.sqrMagnitude < .001f) to = Vector3.forward; return cover - to * (coverRadius + .45f); }
        public void PrepareFor(Transform player)
        {
            if (kind != SpawnKind.LocustPeek || player == null) return;
            Vector3 cover = CoverTransform.position; Vector3 to = Vector3.ProjectOnPlane(player.position - cover, Vector3.up).normalized; if (to.sqrMagnitude < .001f) to = Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, to).normalized * (Random.value < .5f ? -1f : 1f);
            SetWorld(hidePoint, cover - to * (coverRadius + .48f), player.position);
            SetWorld(peekPoint, cover - to * .10f + side * (coverRadius + peekExtraOffset), player.position);
            SetWorld(retreatPoint, cover - to * (coverRadius + 1.10f), player.position);
        }
        private static void SetWorld(Transform target, Vector3 position, Vector3 lookAt) { if (target == null) return; target.position = position; Vector3 flat = Vector3.ProjectOnPlane(lookAt - position, Vector3.up); if (flat.sqrMagnitude > .001f) target.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up); }
    }
}
