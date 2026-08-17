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

            // A clean CI checkout only carries the Unity version in ProjectSettings.
            // Build-time setup therefore owns the active URP configuration instead of
            // inheriting whatever graphics settings happen to exist on a machine.
            FallenForestRenderPipelineSetup.EnsureConfigured();

            FallenForestUserContentIntegrator.IntegrateBeforeSceneAssembly();

            // Structural prefab passes must finish before the final user-material pass.
            // PickupWheelMeshSplitter rebuilds Pickup_Final from the merged FBX; applying the
            // PBR material before that split gets overwritten by the rebuilt source renderers.
            FallenForestGrassMaterialBuilder.ApplyIfAvailable();
            FallenForestCreatureMotionIntegrator.Apply();
            PickupWheelMeshSplitter.BuildIfAvailable();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Apply exact user PBR textures only after all structural prefab rebuilds. This makes
            // the material state that readiness validation sees the same state scene instances use.
            FallenForestUserMaterialBuilder.ApplyIfAvailable();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            FallenForestSceneAssembler.EnsureRequiredScenesForCI();
            // A clean Unity 6 CI project can add UniversalAdditionalCameraData before the camera
            // has a concrete renderer index. Prepare the base camera first so cameraStack is valid.
            FallenForestViewmodelCameraUrpGuard.PrepareForestBaseCamera();
            FallenForestUserContentIntegrator.PatchGeneratedForestScene();
            FallenForestTerrainVisualIntegrator.Configure();
            FallenForestViewmodelMotionIntegrator.Configure();
            FallenForestStreamingVideoIntegrator.ConfigureBoiledSequence();
            FallenForestFinaleIntegrator.FinalizeForestEnding();
            FallenForestRuntimeSceneIntegrator.FinalizeForestRuntimeSystems();
            FallenForestMenuFinalizer.FinalizeMainMenu();
            FallenForestRuntimeSceneIntegrator.FinalizeMainMenuRuntimeSystems();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Do not allow another technically green but visually/runtime-incomplete APK.
            FallenForestRuntimeReadinessValidator.ValidateOrThrow();
            FallenForestReleaseValidator.ValidateReleaseOrThrow();

            if (!File.Exists(MainMenu) || !File.Exists(Forest))
                throw new UnityEditor.Build.BuildFailedException("Final MainMenu/Forest scenes are missing after scene assembly.");

            Directory.CreateDirectory("Builds/Android");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false;

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
