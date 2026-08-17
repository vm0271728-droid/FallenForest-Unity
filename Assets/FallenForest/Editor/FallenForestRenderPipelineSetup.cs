#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Creates and activates a deterministic URP configuration for clean CI checkouts.
    /// The repository intentionally carries minimal ProjectSettings, so the release build
    /// must not depend on machine-local Graphics/Quality settings.
    /// </summary>
    public static class FallenForestRenderPipelineSetup
    {
        private const string SettingsRoot = "Assets/FallenForest/Settings";
        private const string PipelinePath = SettingsRoot + "/FallenForest_URP.asset";
        private const string RendererPath = SettingsRoot + "/FallenForest_UniversalRenderer.asset";
        private const string BuiltinRendererTempPath = "Assets/UniversalRenderer.asset";

        [MenuItem("Fallen Forest/Release/Ensure URP Configuration")]
        public static void EnsureFromMenu() => EnsureConfigured();

        public static UniversalRenderPipelineAsset EnsureConfigured()
        {
            EnsureSettingsFolder();

            UniversalRenderPipelineAsset pipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);

            if (pipeline == null)
            {
                // LoadBuiltinRendererData creates a fully initialized Universal Renderer asset,
                // including the package shader resources Unity expects. Create the pipeline first,
                // let URP initialize the renderer, then move that renderer into our settings folder.
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(BuiltinRendererTempPath) != null)
                    AssetDatabase.DeleteAsset(BuiltinRendererTempPath);

                pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                pipeline.name = "FallenForest_URP";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);

                ScriptableRendererData rendererData =
                    pipeline.LoadBuiltinRendererData(RendererType.UniversalRenderer);
                if (rendererData == null)
                    throw new InvalidOperationException("URP failed to create the Universal Renderer data asset.");

                string currentRendererPath = AssetDatabase.GetAssetPath(rendererData);
                if (!string.Equals(currentRendererPath, RendererPath, StringComparison.Ordinal))
                {
                    if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(RendererPath) != null)
                        AssetDatabase.DeleteAsset(RendererPath);

                    string moveError = AssetDatabase.MoveAsset(currentRendererPath, RendererPath);
                    if (!string.IsNullOrEmpty(moveError))
                        throw new InvalidOperationException("Could not move URP renderer asset: " + moveError);
                }

                EditorUtility.SetDirty(pipeline);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            }

            if (pipeline == null)
                throw new InvalidOperationException("Fallen Forest URP pipeline asset could not be loaded.");

            UniversalRendererData renderer =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
                throw new InvalidOperationException("Fallen Forest Universal Renderer data asset is missing.");

            // Camera stacking requires the Universal Renderer in a non-deferred rendering mode.
            renderer.renderingMode = RenderingMode.Forward;
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);

            // Conservative mobile defaults: keep URP active and camera stacking available without
            // paying for desktop-oriented HDR/MSAA settings the Android build does not need.
            pipeline.renderScale = 1f;
            pipeline.msaaSampleCount = 2;
            pipeline.supportsHDR = false;
            pipeline.useSRPBatcher = true;
            pipeline.shadowDistance = 60f;
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            AssetDatabase.SaveAssets();

            if (GraphicsSettings.currentRenderPipeline != pipeline)
                throw new InvalidOperationException("URP did not become the active render pipeline.");
            if (pipeline.GetRenderer(0) == null)
                throw new InvalidOperationException("URP pipeline has no usable Universal Renderer.");

            Debug.Log("Fallen Forest: URP active with a validated Universal Renderer and camera-stack support.");
            return pipeline;
        }

        private static void EnsureSettingsFolder()
        {
            if (AssetDatabase.IsValidFolder(SettingsRoot)) return;
            if (!AssetDatabase.IsValidFolder("Assets/FallenForest"))
                throw new InvalidOperationException("Assets/FallenForest is missing; cannot create URP settings folder.");
            AssetDatabase.CreateFolder("Assets/FallenForest", "Settings");
        }
    }
}
#endif
