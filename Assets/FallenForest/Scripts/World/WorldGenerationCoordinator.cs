using UnityEngine;

namespace FallenForest.World
{
    /// <summary>
    /// Establishes the required generation order for the final forest:
    /// terrain relief -> terrain-following trails -> trees/grass.
    /// DocumentSpawner runs later at normal script order and therefore samples the finished world.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class WorldGenerationCoordinator : MonoBehaviour
    {
        [SerializeField] private TerrainReliefGenerator terrainRelief;
        [SerializeField] private TrailNetworkGenerator trailNetwork;
        [SerializeField] private ForestScatterer forestScatterer;
        [SerializeField] private bool generateOnStart = true;

        public bool GenerationComplete { get; private set; }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (generateOnStart)
                GenerateWorld();
        }

        [ContextMenu("Generate Complete World")]
        public void GenerateWorld()
        {
            ResolveReferences();
            GenerationComplete = false;

            terrainRelief?.Generate();
            trailNetwork?.Generate();
            forestScatterer?.Generate();

            GenerationComplete = true;
            Debug.Log("Fallen Forest: terrain, trails and dense forest generation completed in deterministic order.", this);
        }

        private void ResolveReferences()
        {
            if (terrainRelief == null)
                terrainRelief = FindFirstObjectByType<TerrainReliefGenerator>(FindObjectsInactive.Include);
            if (trailNetwork == null)
                trailNetwork = FindFirstObjectByType<TrailNetworkGenerator>(FindObjectsInactive.Include);
            if (forestScatterer == null)
                forestScatterer = FindFirstObjectByType<ForestScatterer>(FindObjectsInactive.Include);
        }
    }
}
