using UnityEngine;

namespace FallenForest.World
{
    /// <summary>
    /// Marks a lightweight trigger volume as visual cover for gaze/line-of-sight systems.
    /// It deliberately does not become a physical wall for the player or monsters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisibilityOccluder : MonoBehaviour
    {
    }
}
