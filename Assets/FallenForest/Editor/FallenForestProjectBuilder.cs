#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FallenForest.EditorTools
{
    public static class FallenForestProjectBuilder
    {
        private const string MainMenu = "Assets/FallenForest/Scenes/MainMenu.unity";
        private const string Forest = "Assets/FallenForest/Scenes/Forest.unity";
        private const string Output = "Builds/Android/Fallen_Forest_1.0.0.apk";

        [MenuItem("Fallen Forest/Build Android APK")]
        public static void BuildAndroidAPK() => BuildRelease();

        public static void CIBuildAndroid() => BuildRelease();

        private static void BuildRelease()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // All import, material and shader validation below must happen in the same Android
            // target context as the final APK. Otherwise CI could validate a desktop shader/import
            // path and only discover Android-specific failures minutes later in BuildPipeline.
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false;

            FallenForestRenderPipelineSetup.EnsureConfigured();
            FallenForestUserContentIntegrator.IntegrateBeforeSceneAssembly();
            FallenForestTreePackIntegrator.BuildAvailable();
            FallenForestLowPolyForestIntegrator.BuildAvailable();

            // Structural prefab passes must finish before the final user-material pass.
            FallenForestGrassMaterialBuilder.ApplyIfAvailable();
            FallenForestCreatureMotionIntegrator.Apply();
            PickupWheelMeshSplitter.BuildIfAvailable();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            FallenForestUserMaterialBuilder.ApplyIfAvailable();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            FallenForestSceneAssembler.EnsureRequiredScenesForCI();
            FallenForestViewmodelCameraUrpGuard.PrepareForestBaseCamera();
            FallenForestUserContentIntegrator.PatchGeneratedForestScene();
            FallenForestTreePackIntegrator.PatchForestScene();
            FallenForestLowPolyForestIntegrator.PatchForestScene();
            FallenForestTerrainVisualIntegrator.Configure();
            FallenForestViewmodelMotionIntegrator.Configure();
            FallenForestStreamingVideoIntegrator.ConfigureBoiledSequence();
            FallenForestFinaleIntegrator.FinalizeForestEnding();
            FallenForestRuntimeSceneIntegrator.FinalizeForestRuntimeSystems();
            FallenForestCanonicalRuntimeIntegrator.Configure();
            FallenForestMenuFinalizer.FinalizeMainMenu();
            FallenForestRuntimeSceneIntegrator.FinalizeMainMenuRuntimeSystems();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            FallenForestShaderReleaseValidator.ValidateOrThrow();
            FallenForestRuntimeReadinessValidator.ValidateOrThrow();
            FallenForestReleaseValidator.ValidateReleaseOrThrow();

            if (!File.Exists(MainMenu) || !File.Exists(Forest))
                throw new UnityEditor.Build.BuildFailedException("Final MainMenu/Forest scenes are missing after scene assembly.");

            Directory.CreateDirectory("Builds/Android");

            PlayerSettings.companyName = "Fallen Forest";
            PlayerSettings.productName = "Fallen Forest";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.fallenforest.horror");
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 10000;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenu, true),
                new EditorBuildSettingsScene(Forest, true)
            };

            BuildPlayerOptions options = new()
            {
                scenes = new[] { MainMenu, Forest },
                locationPathName = Output,
                target = BuildTarget.Android,
                options = BuildOptions.CompressWithLz4HC
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new UnityEditor.Build.BuildFailedException(
                    $"Android build failed: {report.summary.result}, errors={report.summary.totalErrors}");

            Debug.Log($"Fallen Forest APK built: {Output}, {report.summary.totalSize / 1048576f:0.0} MB");
        }
    }
}
#endif
