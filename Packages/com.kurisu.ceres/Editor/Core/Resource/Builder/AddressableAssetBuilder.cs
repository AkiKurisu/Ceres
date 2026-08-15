using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

namespace Ceres.Resource.Editor
{
    public class AddressableAssetBuilder : IResourceBuilder
    {
        private bool _buildRemoteCatalog;

        public void Build(ResourceExportContext context)
        {
            // Force enable remote catalog
            _buildRemoteCatalog = AddressableAssetSettingsDefaultObject.Settings.BuildRemoteCatalog;
            AddressableAssetSettingsDefaultObject.Settings.BuildRemoteCatalog = true;
            AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogBuildPath.SetVariableByName(AddressableAssetSettingsDefaultObject.Settings, AddressableAssetSettings.kRemoteBuildPath);
            AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogLoadPath.SetVariableByName(AddressableAssetSettingsDefaultObject.Settings, AddressableAssetSettings.kRemoteLoadPath);

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.profileSettings.SetValue(settings.activeProfileId, "Remote.LoadPath", ResourceSystem.DynamicLoadPath);
            settings.profileSettings.SetValue(settings.activeProfileId, "Remote.BuildPath", context.BuildPath);
        }

        public void Cleanup(ResourceExportContext context)
        {
            // Reset build setting
            AddressableAssetSettingsDefaultObject.Settings.BuildRemoteCatalog = _buildRemoteCatalog;

            EditorUtility.SetDirty(AddressableAssetSettingsDefaultObject.Settings);
            AssetDatabase.SaveAssetIfDirty(AddressableAssetSettingsDefaultObject.Settings);
            if (context.SkipCatalogPostprocess)
            {
                Debug.LogWarning("<color=#ffd45a>Addressable Asset Builder</color>: Addressables build failed, catalog postprocess skipped.");
                return;
            }

            var result = AddressableCatalogPostprocessor.Postprocess(context.BuildPath);
            Debug.Log($"<color=#3aff48>Addressable Asset Builder</color>: Catalog postprocessed, {result.BundleReferenceCount} bundle locations normalized, {result.CopiedBundleCount} dependency bundles copied.");
        }
    }

    internal sealed class CeresAddressablesDiagnosticBuildScript : BuildScriptPackedMode
    {
        private static readonly FieldInfo IncludeInBuildField = typeof(BundledAssetGroupSchema)
            .GetField("m_IncludeInBuild", BindingFlags.Instance | BindingFlags.NonPublic);

        private Func<AddressableAssetGroup, bool> _assetGroupFilter;

        public static Exception LastException { get; private set; }

        public override string Name => "Ceres Addressables Diagnostics";

        public static void ClearLastException()
        {
            LastException = null;
        }

        public void Configure(Func<AddressableAssetGroup, bool> assetGroupFilter)
        {
            _assetGroupFilter = assetGroupFilter;
        }

        protected override string ProcessBundledAssetSchema(BundledAssetGroupSchema schema,
            AddressableAssetGroup assetGroup, AddressableAssetsBuildContext aaContext)
        {
            if (_assetGroupFilter == null)
            {
                return base.ProcessBundledAssetSchema(schema, assetGroup, aaContext);
            }

            if (!_assetGroupFilter(assetGroup))
            {
                return string.Empty;
            }

            if (schema.IncludeInBuild)
            {
                return base.ProcessBundledAssetSchema(schema, assetGroup, aaContext);
            }

            if (IncludeInBuildField == null)
            {
                throw new MissingFieldException(typeof(BundledAssetGroupSchema).FullName, "m_IncludeInBuild");
            }

            // Addressables has no build-time include override. Change its backing field only while
            // the selected group is processed so the schema asset is never marked dirty or saved.
            IncludeInBuildField.SetValue(schema, true);
            try
            {
                return base.ProcessBundledAssetSchema(schema, assetGroup, aaContext);
            }
            finally
            {
                IncludeInBuildField.SetValue(schema, false);
            }
        }

        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput)
        {
            try
            {
                return base.BuildDataImplementation<TResult>(builderInput);
            }
            catch (Exception exception)
            {
                LastException = exception;
                Debug.LogException(exception);
                throw;
            }
        }
    }

    internal sealed class AddressableCatalogPostprocessResult
    {
        public int BundleReferenceCount { get; set; }

        public int CopiedBundleCount { get; set; }

        public string CatalogHash { get; set; }
    }

    internal static class AddressableCatalogPostprocessor
    {
        private const string BundleExtension = ".bundle";

        public static AddressableCatalogPostprocessResult Postprocess(string buildPath)
        {
            NormalizeCatalogFiles(buildPath);

            string catalogPath = Path.Combine(buildPath, $"catalog{ResourceSystem.GetCatalogExtension()}");

            if (!File.Exists(catalogPath))
            {
                throw new FileNotFoundException($"Addressables content catalog was not found: {catalogPath}");
            }

            var result = new AddressableCatalogPostprocessResult();
            var bundleNames = CopyAddressablesBuildBundles(buildPath, result);

#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
            ProcessBinaryCatalog(catalogPath, bundleNames, result);
#else
            ProcessJsonCatalog(catalogPath, bundleNames, result);
#endif
            RewriteCatalogHash(buildPath, catalogPath, result);

            return result;
        }

#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
        private static void ProcessBinaryCatalog(string catalogPath, IReadOnlyCollection<string> bundleNames, AddressableCatalogPostprocessResult result)
        {
            var data = File.ReadAllBytes(catalogPath);
            var reader = new BinaryStorageBuffer.Reader(data, 1024, 1024, new ContentCatalogData.Serializer().WithInternalIdResolvingDisabled());
            var catalogData = reader.ReadObject<ContentCatalogData>(0, out _, false);
            var locator = catalogData.CreateCustomLocator();

            var locationToKeys = new Dictionary<CatalogLocationKey, (IResourceLocation, HashSet<object>)>();
            foreach (var key in locator.Keys)
            {
                if (!locator.Locate(key, typeof(object), out var locs))
                {
                    continue;
                }

                foreach (var loc in locs)
                {
                    var locationKey = CatalogLocationKey.Create(loc);
                    if (!locationToKeys.TryGetValue(locationKey, out var locKeys))
                    {
                        locationToKeys.Add(locationKey, locKeys = (loc, new HashSet<object>()));
                    }

                    locKeys.Item2.Add(key);
                }
            }

            var modifiedEntries = new List<ContentCatalogDataEntry>();
            foreach (var kvp in locationToKeys)
            {
                var loc = kvp.Value.Item1;
                string modifiedInternalId = RewriteBundleInternalId(loc.InternalId, bundleNames, result);

                List<object> deps = null;
                if (loc.HasDependencies)
                {
                    deps = new List<object>();
                    foreach (var dependency in loc.Dependencies)
                    {
                        deps.Add(dependency.PrimaryKey);
                    }
                }

                modifiedEntries.Add(new ContentCatalogDataEntry(
                    loc.ResourceType,
                    modifiedInternalId,
                    loc.ProviderId,
                    kvp.Value.Item2,
                    deps,
                    loc.Data
                ));
            }

            var newCatalog = new ContentCatalogData(modifiedEntries, catalogData.ProviderId)
            {
                BuildResultHash = catalogData.BuildResultHash,
                InstanceProviderData = catalogData.InstanceProviderData,
                SceneProviderData = catalogData.SceneProviderData,
                ResourceProviderData = catalogData.ResourceProviderData
            };
            newCatalog.SetData(modifiedEntries);

            byte[] modifiedData = newCatalog.SerializeToByteArray();
            File.WriteAllBytes(catalogPath, modifiedData);
        }

        private readonly struct CatalogLocationKey : IEquatable<CatalogLocationKey>
        {
            private readonly string _primaryKey;
            private readonly string _internalId;
            private readonly string _providerId;
            private readonly Type _resourceType;
            private readonly int _dependencyHashCode;

            private CatalogLocationKey(IResourceLocation location)
            {
                _primaryKey = location.PrimaryKey;
                _internalId = location.InternalId;
                _providerId = location.ProviderId;
                _resourceType = location.ResourceType;
                _dependencyHashCode = location.DependencyHashCode;
            }

            public static CatalogLocationKey Create(IResourceLocation location)
            {
                return new CatalogLocationKey(location);
            }

            public bool Equals(CatalogLocationKey other)
            {
                return string.Equals(_primaryKey, other._primaryKey, StringComparison.Ordinal)
                       && string.Equals(_internalId, other._internalId, StringComparison.Ordinal)
                       && string.Equals(_providerId, other._providerId, StringComparison.Ordinal)
                       && Equals(_resourceType, other._resourceType)
                       && _dependencyHashCode == other._dependencyHashCode;
            }

            public override bool Equals(object obj)
            {
                return obj is CatalogLocationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_primaryKey ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_internalId ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_providerId ?? string.Empty);
                    hash = hash * 31 + (_resourceType != null ? _resourceType.GetHashCode() : 0);
                    hash = hash * 31 + _dependencyHashCode;
                    return hash;
                }
            }
        }
#else
        private static void ProcessJsonCatalog(string catalogPath, IReadOnlyCollection<string> bundleNames, AddressableCatalogPostprocessResult result)
        {
            var catalog = JsonUtility.FromJson<ContentCatalogData>(File.ReadAllText(catalogPath));
            if (catalog.InternalIds == null)
            {
                return;
            }

            for (int i = 0; i < catalog.InternalIds.Length; ++i)
            {
                catalog.InternalIds[i] = RewriteBundleInternalId(catalog.InternalIds[i], bundleNames, result);
            }

            string json = JsonUtility.ToJson(catalog);
            File.WriteAllText(catalogPath, json);
        }
#endif

        private static string RewriteBundleInternalId(string internalId, IReadOnlyCollection<string> bundleNames, AddressableCatalogPostprocessResult result)
        {
            if (string.IsNullOrEmpty(internalId))
            {
                return internalId;
            }

            foreach (var bundleName in bundleNames)
            {
                if (internalId.IndexOf(bundleName, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                result.BundleReferenceCount++;
                return $"{ResourceSystem.DynamicLoadPath}/{bundleName}";
            }

            return internalId;
        }

        private static IReadOnlyCollection<string> CopyAddressablesBuildBundles(string buildPath, AddressableCatalogPostprocessResult result)
        {
            var bundleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string bundlePath in Directory.GetFiles(buildPath, $"*{BundleExtension}", SearchOption.AllDirectories))
            {
                bundleNames.Add(Path.GetFileName(bundlePath));
            }

            if (!Directory.Exists(Addressables.BuildPath))
            {
                return bundleNames.ToList();
            }

            foreach (string bundlePath in Directory.GetFiles(Addressables.BuildPath, $"*{BundleExtension}", SearchOption.AllDirectories))
            {
                string bundleName = Path.GetFileName(bundlePath);
                string destination = Path.Combine(buildPath, bundleName);

                if (!File.Exists(destination))
                {
                    File.Copy(bundlePath, destination, true);
                    result.CopiedBundleCount++;
                }

                bundleNames.Add(bundleName);
            }

            return bundleNames.ToList();
        }

        private static void RewriteCatalogHash(string buildPath, string catalogPath, AddressableCatalogPostprocessResult result)
        {
#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
            string catalogHash = CalculateAddressablesHash(File.ReadAllBytes(catalogPath));
#else
            string catalogHash = CalculateAddressablesHash(File.ReadAllText(catalogPath));
#endif
            File.WriteAllText(Path.Combine(buildPath, "catalog.hash"), catalogHash);
            result.CatalogHash = catalogHash;
        }

        private static string CalculateAddressablesHash(object value)
        {
            var hashingMethods = Type.GetType("UnityEditor.Build.Pipeline.Utilities.HashingMethods, Unity.ScriptableBuildPipeline.Editor");
            var calculateMethod = hashingMethods?
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "Calculate")
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();
                    return parameters.Length == 1
                           && parameters[0].ParameterType == typeof(object)
                           && !Attribute.IsDefined(parameters[0], typeof(ParamArrayAttribute));
                });
            if (calculateMethod != null)
            {
                return calculateMethod.Invoke(null, new[] { value }).ToString();
            }

            return value switch
            {
                byte[] bytes => Hash128.Compute(bytes).ToString(),
                string text => Hash128.Compute(text).ToString(),
                _ => Hash128.Compute(value?.ToString() ?? string.Empty).ToString()
            };
        }

        private static void NormalizeCatalogFiles(string buildPath)
        {
            string extension = ResourceSystem.GetCatalogExtension();
            string catalogPath = Path.Combine(buildPath, $"catalog{extension}");
            string hashPath = Path.Combine(buildPath, "catalog.hash");

            if (!File.Exists(catalogPath))
            {
                string versionedCatalog = Directory.GetFiles(buildPath, $"catalog_*{extension}", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (string.IsNullOrEmpty(versionedCatalog))
                {
                    throw new FileNotFoundException($"No Addressables content catalog was generated in {buildPath}.");
                }

                File.Move(versionedCatalog, catalogPath);
            }

            if (!File.Exists(hashPath))
            {
                string versionedHash = Directory.GetFiles(buildPath, "catalog_*.hash", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (string.IsNullOrEmpty(versionedHash))
                {
                    throw new FileNotFoundException($"No Addressables content catalog hash was generated in {buildPath}.");
                }

                File.Move(versionedHash, hashPath);
            }
        }
    }
}
