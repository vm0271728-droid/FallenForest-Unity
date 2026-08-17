using UnityEngine;

namespace FallenForest.Monsters
{
    public sealed class MonsterSpawnPoint : MonoBehaviour
    {
        public enum SpawnKind { LocustPeek, BoiledOpen }

        [SerializeField] private SpawnKind kind;
        [SerializeField] private Transform coverTransform;
        [SerializeField] private Transform hidePoint;
        [SerializeField] private Transform peekPoint;
        [SerializeField] private Transform retreatPoint;
        [SerializeField] private float coverRadius = .55f;
        [SerializeField] private float peekExtraOffset = .70f;

        public SpawnKind Kind => kind;
        public Transform CoverTransform => coverTransform != null ? coverTransform : transform;
        public Transform HidePoint => hidePoint != null ? hidePoint : transform;
        public Transform PeekPoint => peekPoint != null ? peekPoint : transform;
        public Transform RetreatPoint => retreatPoint != null ? retreatPoint : HidePoint;
        public Vector3 ReferencePosition => kind == SpawnKind.LocustPeek ? CoverTransform.position : PeekPoint.position;
        public bool RuntimeGenerated { get; private set; }

        public Vector3 GetHiddenPositionFor(Transform player)
        {
            if (kind != SpawnKind.LocustPeek || player == null)
                return HidePoint.position;
            Vector3 cover = CoverTransform.position;
            Vector3 to = Vector3.ProjectOnPlane(player.position - cover, Vector3.up).normalized;
            if (to.sqrMagnitude < .001f) to = Vector3.forward;
            return cover - to * (coverRadius + .45f);
        }

        public void PrepareFor(Transform player)
        {
            if (kind != SpawnKind.LocustPeek || player == null)
                return;

            Vector3 cover = CoverTransform.position;
            Vector3 to = Vector3.ProjectOnPlane(player.position - cover, Vector3.up).normalized;
            if (to.sqrMagnitude < .001f) to = Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, to).normalized * (Random.value < .5f ? -1f : 1f);

            SetWorld(hidePoint, cover - to * (coverRadius + .48f), player.position);
            SetWorld(peekPoint, cover - to * .10f + side * (coverRadius + peekExtraOffset), player.position);
            SetWorld(retreatPoint, cover - to * (coverRadius + 1.10f), player.position);
        }

        public static MonsterSpawnPoint CreateRuntimeLocust(Vector3 coverPosition, float radius, Transform player)
        {
            GameObject root = new("Runtime_LocustPeek");
            root.transform.position = coverPosition;
            MonsterSpawnPoint point = root.AddComponent<MonsterSpawnPoint>();
            point.RuntimeGenerated = true;
            point.kind = SpawnKind.LocustPeek;
            point.coverTransform = root.transform;
            point.coverRadius = Mathf.Clamp(radius, .35f, 2.6f);
            point.hidePoint = CreateChild(root.transform, "HidePoint");
            point.peekPoint = CreateChild(root.transform, "PeekPoint");
            point.retreatPoint = CreateChild(root.transform, "RetreatPoint");
            point.PrepareFor(player);
            return point;
        }

        public static MonsterSpawnPoint CreateRuntimeBoiled(Vector3 position, Transform player)
        {
            GameObject root = new("Runtime_BoiledOpen");
            root.transform.position = position;
            MonsterSpawnPoint point = root.AddComponent<MonsterSpawnPoint>();
            point.RuntimeGenerated = true;
            point.kind = SpawnKind.BoiledOpen;
            point.peekPoint = CreateChild(root.transform, "StandPoint");
            point.peekPoint.position = position;
            if (player != null)
            {
                Vector3 flat = Vector3.ProjectOnPlane(player.position - position, Vector3.up);
                if (flat.sqrMagnitude > .001f)
                    point.peekPoint.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }
            return point;
        }

        public void Release()
        {
            if (RuntimeGenerated && this != null)
                Destroy(gameObject);
        }

        private static Transform CreateChild(Transform parent, string childName)
        {
            GameObject go = new(childName);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void SetWorld(Transform target, Vector3 position, Vector3 lookAt)
        {
            if (target == null) return;
            target.position = position;
            Vector3 flat = Vector3.ProjectOnPlane(lookAt - position, Vector3.up);
            if (flat.sqrMagnitude > .001f)
                target.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        }
    }
}
