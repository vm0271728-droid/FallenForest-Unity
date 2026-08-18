#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// Hard release gate for the project's custom shaders. A material merely referencing a shader
    /// is not enough: the Unity compiler must report no shader errors on the CI editor/platform.
    /// </summary>
    public static class FallenForestShaderReleaseValidator
    {
        private static readonly string[] RequiredShaders =
        {
            "Assets/FallenForest/Materials/ForestWindURP.shader",
            "Assets/FallenForest/Materials/TreeFoliageURP.shader",
            "Assets/FallenForest/Materials/PickupOutline.shader"
        };

        public static void ValidateOrThrow()
        {
            var errors = new List<string>();
            foreach (string path in RequiredShaders)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null)
                {
                    errors.Add("Missing or unimportable shader: " + path);
                    continue;
                }

                ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
                foreach (ShaderMessage message in messages)
                {
                    if (message.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) continue;
                    string platform = message.platform.ToString();
                    if (string.IsNullOrEmpty(platform)) platform = "unknown-platform";
                    errors.Add($"{path} [{platform}] line {message.line}: {message.message}");
                }

                if (!shader.isSupported)
                    errors.Add($"Shader is unsupported on the current release target/editor: {path}");
            }

            if (errors.Count == 0)
            {
                Debug.Log("Fallen Forest: custom shader release validation PASSED.");
                return;
            }

            string report = "Fallen Forest shader validation failed:\n - " + string.Join("\n - ", errors.Distinct());
            Debug.LogError(report);
            throw new BuildFailedException(report);
        }
    }
}
#endif
