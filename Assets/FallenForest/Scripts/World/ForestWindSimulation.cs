using UnityEngine;

namespace FallenForest.World
{
    public class ForestWindSimulation : MonoBehaviour
    {
        [Range(0f,1f)] public float windStrength = 0.35f;
        public float windSpeed = 0.8f;
        public Transform[] vegetationRoots;

        void Update()
        {
            float sway = Mathf.Sin(Time.time * windSpeed) * windStrength;
            foreach (var item in vegetationRoots)
            {
                if (item == null) continue;
                item.localRotation = Quaternion.Euler(0f, 0f, sway);
            }
        }
    }
}
