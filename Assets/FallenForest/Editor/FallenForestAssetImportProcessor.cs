#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Keeps high-quality source art while applying sane Android import settings automatically.
    /// Exact creature meshes are preserved; compression here only affects runtime representation.
    /// </summary>
    public sealed class FallenForestAssetImportProcessor : AssetPostprocessor
    {
        private const string Root = "Assets/FallenForest/";
        private const string CreatureRoot = "Assets/FallenForest/Art/Models/DoctorNowhere/";

        private bool IsFallenForestAsset => assetPath.StartsWith(Root, StringComparison.OrdinalIgnoreCase);
        private bool IsCreatureAsset => assetPath.StartsWith(CreatureRoot, StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessModel()
        {
            if (!IsCreatureAsset) return;
            if (assetImporter is not ModelImporter importer) return;

            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = true;
            importer.importVisibility = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.weldVertices = true;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.animationPositionError = .25f;
            importer.animationRotationError = .25f;
            importer.animationScaleError = .25f;
        }

        private void OnPreprocessTexture()
        {
            if (!IsFallenForestAsset) return;
            if (assetImporter is not TextureImporter importer) return;

            bool creature = assetPath.StartsWith(CreatureRoot, StringComparison.OrdinalIgnoreCase);
            bool normal = assetPath.IndexOf("normal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          assetPath.IndexOf("_n.", StringComparison.OrdinalIgnoreCase) >= 0;
            bool ui = assetPath.IndexOf("/UI/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      assetPath.IndexOf("/Icon/", StringComparison.OrdinalIgnoreCase) >= 0;

            importer.maxTextureSize = ui ? 2048 : 2048;
            importer.mipmapEnabled = !ui;
            importer.streamingMipmaps = !ui;
            importer.streamingMipmapsPriority = creature ? 2 : 0;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = 2048;
            // Creature faces and normal maps keep ASTC 4x4. Environment albedo can use 6x6.
            android.format = creature || normal || ui ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 100;
            importer.SetPlatformTextureSettings(android);
        }

        private void OnPreprocessAudio()
        {
            if (!IsFallenForestAsset) return;
            if (assetImporter is not AudioImporter importer) return;

            bool screamer = assetPath.IndexOf("/Screamers/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            assetPath.IndexOf("/Monster/", StringComparison.OrdinalIgnoreCase) >= 0;
            bool ambience = assetPath.IndexOf("/Ambience/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            assetPath.IndexOf("/Menu/", StringComparison.OrdinalIgnoreCase) >= 0;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = ambience ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = screamer ? .92f : .82f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = ambience;
            importer.preloadAudioData = !ambience;
        }
    }
}
#endif
