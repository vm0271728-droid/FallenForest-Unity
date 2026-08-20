using UnityEngine;

namespace FallenForest.Monsters
{
    public enum MonsterState { Hidden, Watching, Retreating, Rage, Chase, Death }

    public abstract class MonsterT3BehaviourController : MonoBehaviour
    {
        public MonsterState State { get; protected set; } = MonsterState.Hidden;
        public float playerDistance;

        protected virtual void Update()
        {
            TickBehaviour();
        }

        protected abstract void TickBehaviour();

        protected bool IsBlockedByWorld()
        {
            return Physics.Linecast(transform.position, Camera.main.transform.position);
        }
    }
}
