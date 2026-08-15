using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;

namespace Ceres.Resource.Editor
{
    /// <summary>
    /// Resource exporter for building remote contents.
    /// </summary>
    public sealed class ResourceExporter
    {
        private static readonly LazyDirectory ExportDirectory = new(Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "Export"));

        /// <summary>
        /// Export destination directory.
        /// </summary>
        public static string ExportPath => ExportDirectory.GetPath();
        
        private readonly List<IResourceBuilder> _builders = new();

        private readonly ResourceExportContext _context;

        private readonly ResourceExportOptions _options;

        private ResourceExporter(ResourceExportContext context, ResourceExportOptions options)
        {
            _context = context;
            _options = options ?? new ResourceExportOptions();
        }
        
        /// <summary>
        /// Create resource exporter from custom context and builders
        /// </summary>
        /// <param name="context"></param>
        /// <param name="builders"></param>
        /// <returns></returns>
        public static ResourceExporter CreateFromContext(ResourceExportContext context, IResourceBuilder[] builders)
        {
            return CreateFromContext(context, builders, new ResourceExportOptions());
        }

        /// <summary>
        /// Create resource exporter from custom context, builders, and output options.
        /// </summary>
        public static ResourceExporter CreateFromContext(ResourceExportContext context, IResourceBuilder[] builders, ResourceExportOptions options)
        {
            var exporter = new ResourceExporter(context, options);
            exporter._builders.AddRange(builders);
            return exporter;
        }
        
        private static string CreateBuildPath(string outputRoot, string modName)
        {
            var targetPath = Path.Combine(outputRoot, EditorUserBuildSettings.activeBuildTarget.ToString());
            if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);
            var buildPath = Path.Combine(targetPath, modName.Replace(" ", string.Empty));
            if (Directory.Exists(buildPath)) FileUtil.DeleteFileOrDirectory(buildPath);
            Directory.CreateDirectory(buildPath);
            return buildPath;
        }
        
        public bool Export()
        {
            return ExportWithResult().Succeeded;
        }

        public ResourceExportResult ExportWithResult()
        {
            var result = new ResourceExportResult();
            try
            {
                _context.BuildPath = CreateBuildPath(_options.GetOutputRoot(), _context.Name);
                result.BuildPath = _context.BuildPath;

                BuildPipeline(result);
                if (string.IsNullOrEmpty(result.Error))
                {
                    WriteOutput(result);
                }
            }
            catch (Exception exception)
            {
                result.Error = exception.Message;
                Debug.LogException(exception);
            }

            if (result.Succeeded)
            {
                Log(string.IsNullOrEmpty(result.ZipPath)
                    ? $"Export succeed, export path: {result.BuildPath}"
                    : $"Export succeed, export path: {result.ZipPath}");
            }
            else
            {
                LogError(string.IsNullOrEmpty(result.Error) ? "Build pipeline failed!" : result.Error);
            }

            return result;
        }

        private void WriteOutput(ResourceExportResult result)
        {
            if (!_options.ZipOutput)
            {
                return;
            }

            string zipPath = result.BuildPath + ".zip";
            if (!ZipTogether(result.BuildPath, zipPath))
            {
                result.Error = "Zip failed!";
                return;
            }

            result.ZipPath = zipPath;
            if (_options.DeleteBuildDirectoryAfterZip)
            {
                Directory.Delete(result.BuildPath, true);
            }
        }
        
        private static void LogError(string message)
        {
            Debug.LogError($"<color=#ff2f2f>Resource Exporter</color>: {message}");
        }
        
        private static void Log(string message)
        {
            Debug.Log($"<color=#3aff48>Resource Exporter</color>: {message}");
        }
        
        private static bool ZipTogether(string buildPath, string zipPath)
        {
            return ZipWrapper.Zip(new[] { buildPath }, zipPath);
        }
        
        private void BuildPipeline(ResourceExportResult result)
        {
            var enteredBuilders = new List<IResourceBuilder>();
            bool pipelineStarted = false;
            try
            {
                _context.SkipCatalogPostprocess = true;
                pipelineStarted = true;
                foreach (var builder in _builders)
                {
                    enteredBuilders.Add(builder);
                    builder.Build(_context);
                }

                AddressablesPlayerBuildResult addressablesResult = _options.EnableAddressablesDiagnostics
                    ? BuildPlayerContentWithDiagnostics(_context.AssetGroupFilter)
                    : BuildPlayerContent(_context.AssetGroupFilter);
                result.AddressablesResult = addressablesResult;
                if (addressablesResult == null)
                {
                    result.Error = "Addressables build returned no result.";
                    return;
                }

                if (!string.IsNullOrEmpty(addressablesResult.Error))
                {
                    result.Error = addressablesResult.Error;
                    return;
                }

                _context.SkipCatalogPostprocess = false;
            }
            finally
            {
                if (pipelineStarted)
                {
                    CleanupPipeline(enteredBuilders, result);
                }
            }
        }

        private static AddressablesPlayerBuildResult BuildPlayerContent(Func<AddressableAssetGroup, bool> assetGroupFilter)
        {
            return BuildPlayerContent(assetGroupFilter, false);
        }

        private static AddressablesPlayerBuildResult BuildPlayerContentWithDiagnostics(Func<AddressableAssetGroup, bool> assetGroupFilter)
        {
            return BuildPlayerContent(assetGroupFilter, true);
        }

        private static AddressablesPlayerBuildResult BuildPlayerContent(Func<AddressableAssetGroup, bool> assetGroupFilter, bool enableDiagnostics)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (!settings)
            {
                return new AddressablesPlayerBuildResult
                {
                    Error = "AddressableAssetSettings is missing."
                };
            }

            var builders = settings.DataBuilders;
            int originalBuilderIndex = settings.ActivePlayerDataBuilderIndex;
            var diagnosticBuilder = ScriptableObject.CreateInstance<CeresAddressablesDiagnosticBuildScript>();
            diagnosticBuilder.hideFlags = HideFlags.HideAndDontSave;
            diagnosticBuilder.name = "Ceres Addressables Diagnostics";
            diagnosticBuilder.Configure(assetGroupFilter);
            CeresAddressablesDiagnosticBuildScript.ClearLastException();

            try
            {
                builders.Add(diagnosticBuilder);
                settings.ActivePlayerDataBuilderIndex = builders.Count - 1;

                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult addressablesResult);
                if (enableDiagnostics && CeresAddressablesDiagnosticBuildScript.LastException != null)
                {
                    Debug.LogError($"[Ceres] BuildPlayerContent returned a shallow error result. Full exception was logged above. Error: {addressablesResult?.Error}");
                }

                return addressablesResult;
            }
            finally
            {
                if (originalBuilderIndex >= 0 && originalBuilderIndex < builders.Count)
                {
                    settings.ActivePlayerDataBuilderIndex = originalBuilderIndex;
                }

                builders.Remove(diagnosticBuilder);
                UnityEngine.Object.DestroyImmediate(diagnosticBuilder);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
            }
        }

        private void CleanupPipeline(List<IResourceBuilder> builders, ResourceExportResult result)
        {
            for (int i = builders.Count - 1; i >= 0; i--)
            {
                var builder = builders[i];
                if (builder == null)
                {
                    continue;
                }

                try
                {
                    builder.Cleanup(_context);
                }
                catch (Exception exception)
                {
                    if (string.IsNullOrEmpty(result.Error))
                    {
                        result.Error = exception.Message;
                    }

                    Debug.LogException(exception);
                }
            }
        }

        private class LazyDirectory
        {
            private readonly string _path;
        
            private bool _initialized;
        
            public LazyDirectory(string path)
            {
                _path = path;
            }
        
            public string GetPath()
            {
                if (!_initialized) return _path;
            
                Directory.CreateDirectory(_path);
                _initialized = true;
                return _path;
            }
        }
    }

    public sealed class ResourceExportOptions
    {
        public string OutputRoot { get; set; }

        public bool ZipOutput { get; set; } = true;

        public bool DeleteBuildDirectoryAfterZip { get; set; } = true;

        internal bool EnableAddressablesDiagnostics { get; set; }

        internal string GetOutputRoot()
        {
            return string.IsNullOrWhiteSpace(OutputRoot) ? ResourceExporter.ExportPath : OutputRoot;
        }
    }
}
