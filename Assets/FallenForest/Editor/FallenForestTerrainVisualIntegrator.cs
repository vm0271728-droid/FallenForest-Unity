#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Adds a deterministic forest-floor TerrainLayer and explicitly validates/repairs TerrainCollider
    /// wiring in the generated Forest scene. This avoids shipping the default untextured terrain.
    /// </summary>
    public static class FallenForestTerrainVisualIntegrator
    {
        private const string ForestScene = "Assets/FallenForest/Scenes/Forest.unity";
        private const string GeneratedDir = "Assets/FallenForest/Generated/Terrain";
        private const string GroundTexturePath = GeneratedDir + "/ForestGroundProcedural.asset";
        private const string GroundLayerPath = GeneratedDir + "/ForestGround.terrainlayer";

        public static void Configure()
        {
            if (!File.Exists(ForestScene)) return;

            Directory.CreateDirectory(GeneratedDir);
            AssetDatabase.Refresh();

            Texture2D groundTexture = EnsureGroundTexture();
            TerrainLayer groundLayer = EnsureGroundLayer(groundTexture);

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ForestScene, OpenSceneMode.Single);
            try
            {
                Terrain terrain = Object.FindFirstObjectByType<Terrain>(FindObjectsInactive.Include);
                if (terrain == null || terrain.terrainData == null)
                    throw new InvalidDataException("Forest scene has no valid Terrain/TerrainData.");

                TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
                if (collider == null) collider = terrain.gameObject.AddComponent<TerrainCollider>();
                collider.sharedTerrainData = terrain.terrainData;
                collider.enabled = true;

                terrain.terrainData.terrainLayers = new[] { groundLayer };
                terrain.drawInstanced = true;
                terrain.basemapDistance = 120f;
                terrain.Flush();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                EditorUtility.SetDirty(terrain.terrainData);
                AssetDatabase.SaveAssets();
                Debug.Log("Fallen Forest: textured forest floor and TerrainCollider configured.");
            }
            finally
            {
                if (previous.IsValid() && !string.IsNullOrEmpty(previous.path) && previous.path != scene.path)
                    EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
            }
        }

        private static Texture2D EnsureGroundTexture()
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexturePath);
            if (existing != null) return existing;

            const int size = 256;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, true, false)
            {
                name = "ForestGroundProcedural",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };

            Color32[] pixels = new Color32[size * size];
            Color darkSoil = new(.075f, .048f, .026f, 1f);
            Color dampSoil = new(.115f, .078f, .040f, 1f);
            Color moss = new(.070f, .105f, .047f, 1f);
            Color deadNeedles = new(.145f, .095f, .045f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;
                    float broad = Mathf.PerlinNoise(17.31f + u * 5.2f, 91.77f + v * 5.2f);
                    float fine = Mathf.PerlinNoise(63.12f + u * 19.4f, 12.48f + v * 19.4f);
                    float litter = Mathf.PerlinNoise(4.77f + u * 41.0f, 52.3f + v * 41.0f);

                    Color color = Color.Lerp(darkSoil, dampSoil, broad);
                    color = Color.Lerp(color, moss, Mathf.Clamp01((broad - .58f) * 2.6f));
                    color = Color.Lerp(color, deadNeedles, Mathf.Clamp01((litter - .70f) * 3.4f) * .52f);
                    float variation = Mathf.Lerp(.88f, 1.10f, fine);
                    color *= variation;
                    color.a = 1f;
                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            AssetDatabase.CreateAsset(texture, GroundTexturePath);
            AssetDatabase.SaveAssets();
            return texture;
        }

        private static TerrainLayer EnsureGroundLayer(Texture2D groundTexture)
        {
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GroundLayerPath);
            if (layer == null)
            {
                layer = new TerrainLayer { name = "ForestGround" };
                AssetDatabase.CreateAsset(layer, GroundLayerPath);
            }

            layer.diffuseTexture = groundTexture;
            layer.tileSize = new Vector2(6.5f, 6.5f);
            layer.tileOffset = Vector2.zero;
            layer.metallic = 0f;
            layer.smoothness = .08f;
            EditorUtility.SetDirty(layer);
            AssetDatabase.SaveAssets();
            return layer;
        }
    }
}
#endif
