using FallenForest.Player;
using UnityEngine;

namespace FallenForest.World
{
    /// <summary>
    /// Establishes the required generation order for the final forest:
    /// terrain relief -> ground runtime anchors -> terrain-following trails -> trees/grass.
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

            Terrain terrain = terrainRelief != null
                ? terrainRelief.GetComponent<Terrain>()
                : FindFirstObjectByType<Terrain>(FindObjectsInactive.Include);

            EnsureTerrainCollider(terrain);
            GroundRuntimeAnchors(terrain);

            trailNetwork?.Generate();
            forestScatterer?.Generate();

            GenerationComplete = true;
            Debug.Log("Fallen Forest: terrain, grounded player/pickup, trails and dense forest generation completed in deterministic order.", this);
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

        private static void EnsureTerrainCollider(Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null) return;

            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider == null) collider = terrain.gameObject.AddComponent<TerrainCollider>();
            collider.terrainData = terrain.terrainData;
            collider.enabled = true;
        }

        private static void GroundRuntimeAnchors(Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null) return;

            PlayerMotor player = FindFirstObjectByType<PlayerMotor>(FindObjectsInactive.Include);
            if (player != null)
            {
                Vector3 position = player.transform.position;
                position.y = SampleWorldHeight(terrain, position) + .06f;
                player.Teleport(position);
            }

            GameObject start = GameObject.Find("PlayerStart");
            if (start != null)
            {
                Vector3 position = start.transform.position;
                position.y = SampleWorldHeight(terrain, position) + .02f;
                start.transform.position = position;
            }

            GameObject flashlightPickup = GameObject.Find("FlashlightPickup");
            if (flashlightPickup != null)
            {
                Vector3 position = flashlightPickup.transform.position;
                position.y = SampleWorldHeight(terrain, position) + .14f;
                flashlightPickup.transform.position = position;
            }

            Physics.SyncTransforms();
        }

        private static float SampleWorldHeight(Terrain terrain, Vector3 worldPosition)
        {
            return terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
        }
    }
}
