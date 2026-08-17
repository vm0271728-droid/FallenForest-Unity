#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Keeps high-quality source art while applying sane Android import settings automatically.
    /// Exact creature and canonical user-tree meshes are preserved; compression only affects the
    /// runtime representation and never substitutes geometry.
    /// </summary>
    public sealed class FallenForestAssetImportProcessor : AssetPostprocessor
    {
        private const string Root = "Assets/FallenForest/";
        private const string CreatureRoot = "Assets/FallenForest/Art/Models/DoctorNowhere/";
        private const string TreeRoot = "Assets/FallenForest/Art/Vegetation/UserTrees/";

        private bool IsFallenForestAsset => assetPath.StartsWith(Root, StringComparison.OrdinalIgnoreCase);
        private bool IsCreatureAsset => assetPath.StartsWith(CreatureRoot, StringComparison.OrdinalIgnoreCase);
        private bool IsTreeAsset => assetPath.StartsWith(TreeRoot, StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessModel()
        {
            if (!IsCreatureAsset && !IsTreeAsset) return;
            if (assetImporter is not ModelImporter importer) return;

            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.weldVertices = true;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;

            if (IsCreatureAsset)
            {
                importer.importBlendShapes = true;
                importer.isReadable = false;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.importAnimation = true;
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                importer.animationPositionError = .25f;
                importer.animationRotationError = .25f;
                importer.animationScaleError = .25f;
            }
            else
            {
                importer.importBlendShapes = false;
                importer.isReadable = false;
                importer.meshCompression = assetPath.IndexOf("LOD0", StringComparison.OrdinalIgnoreCase) >= 0
                    ? ModelImporterMeshCompression.Off
                    : ModelImporterMeshCompression.Low;
                importer.importAnimation = false;
            }
        }

        private void OnPreprocessTexture()
        {
            if (!IsFallenForestAsset) return;
            if (assetImporter is not TextureImporter importer) return;

            bool creature = assetPath.StartsWith(CreatureRoot, StringComparison.OrdinalIgnoreCase);
            bool tree = assetPath.StartsWith(TreeRoot, StringComparison.OrdinalIgnoreCase);
            bool normal = assetPath.IndexOf("normal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          assetPath.IndexOf("_n.", StringComparison.OrdinalIgnoreCase) >= 0;
            bool mask = assetPath.IndexOf("rough", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        assetPath.IndexOf("opacity", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        assetPath.IndexOf("transparency", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        assetPath.IndexOf("specular", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        assetPath.IndexOf("ao", StringComparison.OrdinalIgnoreCase) >= 0;
            bool ui = assetPath.IndexOf("/UI/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      assetPath.IndexOf("/Icon/", StringComparison.OrdinalIgnoreCase) >= 0;

            importer.maxTextureSize = ui ? 2048 : 2048;
            importer.mipmapEnabled = !ui;
            importer.streamingMipmaps = !ui;
            importer.streamingMipmapsPriority = creature ? 2 : tree ? 1 : 0;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (mask || normal) importer.sRGBTexture = false;
            if (normal) importer.textureType = TextureImporterType.NormalMap;

            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = 2048;
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
            settings.preloadAudioData = !ambience;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = ambience;
        }
    }
}
#endif
