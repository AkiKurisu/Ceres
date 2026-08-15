using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

namespace Ceres.ContentPipeline
{
    /// <summary>
    /// Defines the stable physical layout of materialized dynamic content packages.
    /// Complete build identity remains stored in the package manifest.
    /// </summary>
    public static class DynamicContentPackageLayout
    {
        public const string BaselineContainerName = "baselines";
        public const string UpdateContainerName = "updates";
        public const int PackageDirectoryIdLength = 32;

        public static string GetContainerName(bool isUpdate)
        {
            return isUpdate ? UpdateContainerName : BaselineContainerName;
        }

        public static string GetPackageDirectoryName(string buildId)
        {
            if (string.IsNullOrWhiteSpace(buildId))
                throw new InvalidDataException("Dynamic package build id is empty.");
            return buildId.Length <= PackageDirectoryIdLength
                ? buildId
                : buildId[..PackageDirectoryIdLength];
        }
    }

    [Serializable]
    public sealed class DynamicContentPackageManifest
    {
        public int schemaVersion = 1;
        public string buildKind;
        public string buildId;
        public string baselineId;
        public string artifactManifestPath;
        public string dynamicLoadPath;
        public string catalogRelativePath;
        public string[] referencedBundles = Array.Empty<string>();
        public ContentArtifactRecord[] files = Array.Empty<ContentArtifactRecord>();

        public static DynamicContentPackageManifest Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !ContentPipelineFileSystem.FileExists(path))
                throw new FileNotFoundException("Dynamic content package manifest was not found.", path);
            var manifest = JsonUtility.FromJson<DynamicContentPackageManifest>(
                ContentPipelineFileSystem.ReadAllText(path));
            if (manifest == null || manifest.schemaVersion != 1)
                throw new InvalidDataException($"Unsupported dynamic content package manifest: {path}");
            manifest.dynamicLoadPath = NormalizeDynamicLoadPath(manifest.dynamicLoadPath);
            manifest.referencedBundles ??= Array.Empty<string>();
            manifest.files ??= Array.Empty<ContentArtifactRecord>();
            return manifest;
        }

        public void Save(string path)
        {
            dynamicLoadPath = NormalizeDynamicLoadPath(dynamicLoadPath);
            ContentPipelineFileSystem.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            ContentPipelineFileSystem.WriteAllText(path, JsonUtility.ToJson(this, true));
        }

        internal static string NormalizeDynamicLoadPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException("Dynamic content package load path is empty.");
            var normalized = value.Trim().TrimEnd('/', '\\');
            if (normalized.Length == 0)
                throw new InvalidDataException("Dynamic content package load path is empty.");
            return normalized;
        }
    }

    public sealed class DynamicContentPackageRequest
    {
        public string ArtifactManifestPath { get; set; }

        public string OutputRoot { get; set; }

        public string DynamicLoadPath { get; set; }

        public string BaselinePackageManifestPath { get; set; }
    }

    public sealed class DynamicContentPackageResult
    {
        public string OutputPath { get; internal set; }

        public string PackagePath { get; internal set; }

        public string ManifestPath { get; internal set; }

        public DynamicContentPackageManifest Manifest { get; internal set; }
    }

    /// <summary>
    /// Materializes immutable Addressables artifacts as a flat, relocatable content package.
    /// The package is committed only after every catalog bundle reference is accounted for.
    /// </summary>
    public sealed class DynamicContentPackageBuilder
    {
        private const string PackageManifestName = "package-manifest.json";
        private const string PackageDirectoryName = "abdata";
        private const string CatalogHashName = "catalog.hash";
        private const string BundleExtension = ".bundle";
        private static readonly object BuildGate = new();

        public DynamicContentPackageResult Build(DynamicContentPackageRequest request)
        {
            lock (BuildGate)
            {
                return BuildInternal(request);
            }
        }

        private static DynamicContentPackageResult BuildInternal(DynamicContentPackageRequest request)
        {
            ValidateRequest(request);
            var dynamicLoadPath =
                DynamicContentPackageManifest.NormalizeDynamicLoadPath(request.DynamicLoadPath);
            var artifactManifestPath = Path.GetFullPath(request.ArtifactManifestPath);
            var artifactManifest = ContentArtifactManifest.Load(artifactManifestPath);
            var isUpdate = string.Equals(
                artifactManifest.buildKind,
                ContentPipelineBuildKind.Update.ToString().ToLowerInvariant(),
                StringComparison.Ordinal);
            DynamicContentPackageManifest baselinePackage = null;
            string baselinePackageRoot = null;
            if (isUpdate)
            {
                baselinePackage = DynamicContentPackageManifest.Load(request.BaselinePackageManifestPath);
                baselinePackageRoot = Path.GetDirectoryName(Path.GetFullPath(request.BaselinePackageManifestPath))!;
                if (!string.Equals(baselinePackage.buildId, artifactManifest.baselineId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Update baseline '{artifactManifest.baselineId}' does not match dynamic package " +
                        $"baseline '{baselinePackage.buildId}'.");
                }
            }

            var outputRoot = Path.GetFullPath(request.OutputRoot);
            using var processLock = ContentBuildProcessLock.Acquire(outputRoot);
            var collectionName = DynamicContentPackageLayout.GetContainerName(isUpdate);
            var finalRoot = Path.Combine(
                outputRoot,
                collectionName,
                DynamicContentPackageLayout.GetPackageDirectoryName(artifactManifest.buildId));
            var finalManifestPath = Path.Combine(finalRoot, PackageManifestName);
            if (ContentPipelineFileSystem.FileExists(finalManifestPath))
            {
                var existing = DynamicContentPackageManifest.Load(finalManifestPath);
                ValidateExistingPackage(
                    existing,
                    finalRoot,
                    artifactManifest,
                    baselinePackage,
                    baselinePackageRoot,
                    dynamicLoadPath);
                return CreateResult(finalRoot, existing);
            }

            var stagingParent = Path.Combine(outputRoot, ".staging");
            ContentPipelineFileSystem.CreateDirectory(stagingParent);
            var stagingRoot = Path.Combine(stagingParent, Guid.NewGuid().ToString("N"));
            ContentPipelineFileSystem.CreateDirectory(stagingRoot);
            try
            {
                var packageRoot = Path.Combine(stagingRoot, PackageDirectoryName);
                ContentPipelineFileSystem.CreateDirectory(packageRoot);
                var catalogArtifact = SelectRemoteCatalog(artifactManifest);
                var catalogSource = ResolveArtifactPath(artifactManifestPath, catalogArtifact.relativePath);
                var catalogExtension = Path.GetExtension(catalogSource);
                var catalogDestination = Path.Combine(packageRoot, "catalog" + catalogExtension);
                ValidateFile(catalogSource, catalogArtifact);
                ContentPipelineFileSystem.CopyFile(catalogSource, catalogDestination, true);

                var candidateBundles = BuildCandidateBundleMap(artifactManifest, artifactManifestPath);
                var baselineBundles = BuildPackageBundleMap(
                    baselinePackage,
                    baselinePackageRoot,
                    true);
                var availableBundles = new Dictionary<string, BundleSource>(baselineBundles, StringComparer.OrdinalIgnoreCase);
                foreach (var pair in candidateBundles)
                    availableBundles[pair.Key] = pair.Value;

                var referencedBundles = RewriteCatalog(
                    catalogDestination,
                    dynamicLoadPath,
                    availableBundles);
                var copiedFiles = new List<ContentArtifactRecord>();
                foreach (var bundleName in referencedBundles)
                {
                    if (!candidateBundles.TryGetValue(bundleName, out var source))
                        continue;
                    var destination = Path.Combine(packageRoot, bundleName);
                    ValidateFile(source.Path, source.Record);
                    ContentPipelineFileSystem.CopyFile(source.Path, destination, true);
                    copiedFiles.Add(CreateCopiedBundleRecord(
                        source,
                        PackageDirectoryName + "/" + bundleName));
                }

                var catalogRelativePath = PackageDirectoryName + "/" + Path.GetFileName(catalogDestination);
                copiedFiles.Add(CreateFileRecord(catalogDestination, catalogRelativePath, "catalog", Array.Empty<string>()));
                var catalogHashPath = Path.Combine(packageRoot, CatalogHashName);
                ContentPipelineFileSystem.WriteAllText(
                    catalogHashPath,
                    CalculateAddressablesHash(catalogDestination));
                copiedFiles.Add(CreateFileRecord(
                    catalogHashPath,
                    PackageDirectoryName + "/" + CatalogHashName,
                    "catalog-hash",
                    Array.Empty<string>()));

                ValidatePackageFiles(stagingRoot, copiedFiles);
                var packageManifest = new DynamicContentPackageManifest
                {
                    buildKind = artifactManifest.buildKind,
                    buildId = artifactManifest.buildId,
                    baselineId = artifactManifest.baselineId,
                    artifactManifestPath = artifactManifestPath,
                    dynamicLoadPath = dynamicLoadPath,
                    catalogRelativePath = catalogRelativePath,
                    referencedBundles = referencedBundles.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    files = copiedFiles.OrderBy(value => value.relativePath, StringComparer.Ordinal).ToArray()
                };
                packageManifest.Save(Path.Combine(stagingRoot, PackageManifestName));

                ContentPipelineFileSystem.CreateDirectory(Path.GetDirectoryName(finalRoot)!);
                if (ContentPipelineFileSystem.DirectoryExists(finalRoot))
                    throw new IOException($"Dynamic package output already exists without a valid manifest: {finalRoot}");
                ContentPipelineFileSystem.MoveDirectory(stagingRoot, finalRoot);
                return CreateResult(finalRoot, packageManifest);
            }
            catch
            {
                if (ContentPipelineFileSystem.DirectoryExists(stagingRoot))
                    ContentPipelineFileSystem.DeleteDirectory(stagingRoot, true);
                throw;
            }
        }

        private static void ValidateRequest(DynamicContentPackageRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.ArtifactManifestPath))
                throw new ArgumentException("Artifact manifest path is empty.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.OutputRoot))
                throw new ArgumentException("Dynamic package output root is empty.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.DynamicLoadPath))
                throw new ArgumentException("Dynamic load path is empty.", nameof(request));
        }

        private static DynamicContentPackageResult CreateResult(
            string outputPath,
            DynamicContentPackageManifest manifest)
        {
            return new DynamicContentPackageResult
            {
                OutputPath = outputPath,
                PackagePath = Path.Combine(outputPath, PackageDirectoryName),
                ManifestPath = Path.Combine(outputPath, PackageManifestName),
                Manifest = manifest
            };
        }

        private static ContentArtifactRecord SelectRemoteCatalog(ContentArtifactManifest manifest)
        {
            var candidates = manifest.artifacts
                .Where(artifact => artifact.kind == "catalog" &&
                                   artifact.relativePath.StartsWith("remote/", StringComparison.Ordinal))
                .ToArray();
            if (candidates.Length != 1)
                throw new InvalidDataException(
                    $"Expected one remote catalog for build '{manifest.buildId}', found {candidates.Length}.");
            return candidates[0];
        }

        private static Dictionary<string, BundleSource> BuildCandidateBundleMap(
            ContentArtifactManifest manifest,
            string manifestPath)
        {
            var result = new Dictionary<string, BundleSource>(StringComparer.OrdinalIgnoreCase);
            foreach (var artifact in manifest.artifacts.Where(value => value.kind == "bundle"))
            {
                var name = Path.GetFileName(artifact.relativePath);
                var path = ResolveArtifactPath(manifestPath, artifact.relativePath);
                AddBundle(result, name, new BundleSource(path, artifact), "artifact manifest");
            }

            return result;
        }

        private static Dictionary<string, BundleSource> BuildPackageBundleMap(
            DynamicContentPackageManifest manifest,
            string manifestRoot,
            bool validateFiles)
        {
            var result = new Dictionary<string, BundleSource>(StringComparer.OrdinalIgnoreCase);
            if (manifest == null) return result;
            foreach (var file in manifest.files.Where(value => value.kind == "bundle"))
            {
                var path = ResolveWithin(manifestRoot, file.relativePath);
                if (validateFiles) ValidateFile(path, file);
                AddBundle(
                    result,
                    Path.GetFileName(file.relativePath),
                    new BundleSource(path, file),
                    "baseline package");
            }

            return result;
        }

        private static void ValidateExistingPackage(
            DynamicContentPackageManifest package,
            string packageRoot,
            ContentArtifactManifest artifact,
            DynamicContentPackageManifest baselinePackage,
            string baselinePackageRoot,
            string expectedDynamicLoadPath)
        {
            if (!string.Equals(package.buildId, artifact.buildId, StringComparison.Ordinal) ||
                !string.Equals(package.buildKind, artifact.buildKind, StringComparison.Ordinal) ||
                !string.Equals(package.baselineId, artifact.baselineId, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Dynamic package path contains a different build: {packageRoot}");
            }
            if (!string.Equals(
                    package.dynamicLoadPath,
                    expectedDynamicLoadPath,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Dynamic package '{packageRoot}' uses load path '{package.dynamicLoadPath}', " +
                    $"but the request uses '{expectedDynamicLoadPath}'. Remove the stale package and rebuild it.");
            }

            if (package.files.Count(file =>
                    file.kind == "catalog" &&
                    string.Equals(file.relativePath, package.catalogRelativePath, StringComparison.Ordinal)) != 1 ||
                package.files.Count(file =>
                    file.kind == "catalog-hash" &&
                    string.Equals(
                        file.relativePath,
                        PackageDirectoryName + "/" + CatalogHashName,
                        StringComparison.Ordinal)) != 1)
            {
                throw new InvalidDataException($"Dynamic package catalog records are invalid: {packageRoot}");
            }

            ValidatePackageFiles(packageRoot, package.files);
            var availableBundles = BuildPackageBundleMap(package, packageRoot, false);
            foreach (var pair in BuildPackageBundleMap(
                         baselinePackage,
                         baselinePackageRoot,
                         true))
                availableBundles[pair.Key] = pair.Value;
            var missing = package.referencedBundles
                .Where(bundle => !availableBundles.ContainsKey(bundle))
                .OrderBy(bundle => bundle, StringComparer.Ordinal)
                .ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidDataException(
                    $"Dynamic package is missing {missing.Length} referenced bundles:" +
                    Environment.NewLine + string.Join(Environment.NewLine, missing));
            }
        }

        private static void AddBundle(
            IDictionary<string, BundleSource> bundles,
            string name,
            BundleSource source,
            string context)
        {
            if (string.IsNullOrEmpty(name) || !name.EndsWith(BundleExtension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Invalid bundle name in {context}: {name}");
            if (bundles.TryGetValue(name, out var existing) &&
                !string.Equals(existing.Path, source.Path, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Duplicate bundle file name '{name}' in {context}.");
            bundles[name] = source;
        }

        private static string[] RewriteCatalog(
            string catalogPath,
            string dynamicLoadPath,
            IReadOnlyDictionary<string, BundleSource> bundles)
        {
#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
            return RewriteBinaryCatalog(catalogPath, dynamicLoadPath, bundles);
#else
            return RewriteJsonCatalog(catalogPath, dynamicLoadPath, bundles);
#endif
        }

#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
        private static string[] RewriteBinaryCatalog(
            string catalogPath,
            string dynamicLoadPath,
            IReadOnlyDictionary<string, BundleSource> bundles)
        {
            var data = ContentPipelineFileSystem.ReadAllBytes(catalogPath);
            var reader = new BinaryStorageBuffer.Reader(
                data,
                1024,
                1024,
                new ContentCatalogData.Serializer().WithInternalIdResolvingDisabled());
            var catalogData = reader.ReadObject<ContentCatalogData>(0, out _, false);
            var locator = catalogData.CreateCustomLocator();
            var locations = new Dictionary<CatalogLocationKey, (IResourceLocation Location, HashSet<object> Keys)>();
            foreach (var key in locator.Keys)
            {
                if (!locator.Locate(key, typeof(object), out var found)) continue;
                foreach (var location in found)
                {
                    var locationKey = new CatalogLocationKey(location);
                    if (!locations.TryGetValue(locationKey, out var value))
                    {
                        value = (location, new HashSet<object>());
                        locations.Add(locationKey, value);
                    }
                    value.Keys.Add(key);
                }
            }

            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<ContentCatalogDataEntry>(locations.Count);
            foreach (var value in locations.Values)
            {
                var location = value.Location;
                var internalId = RewriteInternalId(location.InternalId, dynamicLoadPath, bundles, referenced);
                List<object> dependencies = null;
                if (location.HasDependencies)
                {
                    dependencies = location.Dependencies.Select(dependency => (object)dependency.PrimaryKey).ToList();
                }
                entries.Add(new ContentCatalogDataEntry(
                    location.ResourceType,
                    internalId,
                    location.ProviderId,
                    value.Keys,
                    dependencies,
                    location.Data));
            }

            var rewritten = new ContentCatalogData(entries, catalogData.ProviderId)
            {
                BuildResultHash = catalogData.BuildResultHash,
                InstanceProviderData = catalogData.InstanceProviderData,
                SceneProviderData = catalogData.SceneProviderData,
                ResourceProviderData = catalogData.ResourceProviderData
            };
            rewritten.SetData(entries);
            ContentPipelineFileSystem.WriteAllBytes(
                catalogPath,
                rewritten.SerializeToByteArray());
            return referenced.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
#else
        private static string[] RewriteJsonCatalog(
            string catalogPath,
            string dynamicLoadPath,
            IReadOnlyDictionary<string, BundleSource> bundles)
        {
            var catalog = JsonUtility.FromJson<ContentCatalogData>(
                ContentPipelineFileSystem.ReadAllText(catalogPath));
            if (catalog?.InternalIds == null)
                throw new InvalidDataException($"JSON content catalog has no internal IDs: {catalogPath}");
            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < catalog.InternalIds.Length; i++)
                catalog.InternalIds[i] = RewriteInternalId(catalog.InternalIds[i], dynamicLoadPath, bundles, referenced);
            ContentPipelineFileSystem.WriteAllText(catalogPath, JsonUtility.ToJson(catalog));
            return referenced.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
#endif

        private static string RewriteInternalId(
            string internalId,
            string dynamicLoadPath,
            IReadOnlyDictionary<string, BundleSource> bundles,
            ISet<string> referenced)
        {
            if (!TryGetBundleName(internalId, out var bundleName)) return internalId;
            if (!bundles.ContainsKey(bundleName))
                throw new FileNotFoundException(
                    $"Catalog references bundle '{bundleName}' that is absent from the candidate and baseline packages.");
            referenced.Add(bundleName);
            return $"{dynamicLoadPath}/{bundleName}";
        }

        private static bool TryGetBundleName(string internalId, out string bundleName)
        {
            bundleName = string.Empty;
            if (string.IsNullOrWhiteSpace(internalId)) return false;
            var value = internalId.Replace('\\', '/');
            var queryIndex = value.IndexOfAny(new[] { '?', '#' });
            if (queryIndex >= 0) value = value[..queryIndex];
            var slashIndex = value.LastIndexOf('/');
            bundleName = slashIndex >= 0 ? value[(slashIndex + 1)..] : value;
            return bundleName.EndsWith(BundleExtension, StringComparison.OrdinalIgnoreCase);
        }

        private static ContentArtifactRecord CreateFileRecord(
            string path,
            string relativePath,
            string kind,
            string[] scopes)
        {
            return new ContentArtifactRecord
            {
                relativePath = relativePath.Replace('\\', '/'),
                kind = kind,
                size = ContentPipelineFileSystem.GetFileLength(path),
                sha256 = ContentPipelineHash.Sha256File(path),
                sourceScopes = scopes ?? Array.Empty<string>()
            };
        }

        private static ContentArtifactRecord CreateCopiedBundleRecord(
            BundleSource source,
            string relativePath)
        {
            return new ContentArtifactRecord
            {
                relativePath = relativePath.Replace('\\', '/'),
                kind = "bundle",
                size = source.Record.size,
                sha256 = source.Record.sha256,
                partitionId = source.Record.partitionId,
                sourceScopes = source.Scopes
            };
        }

        private static string CalculateAddressablesHash(string catalogPath)
        {
            object value =
#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
                ContentPipelineFileSystem.ReadAllBytes(catalogPath);
#else
                ContentPipelineFileSystem.ReadAllText(catalogPath);
#endif
            var hashingMethods = Type.GetType(
                "UnityEditor.Build.Pipeline.Utilities.HashingMethods, Unity.ScriptableBuildPipeline.Editor");
            var calculateMethod = hashingMethods?
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "Calculate") return false;
                    var parameters = method.GetParameters();
                    return parameters.Length == 1 &&
                           parameters[0].ParameterType == typeof(object) &&
                           !Attribute.IsDefined(parameters[0], typeof(ParamArrayAttribute));
                });
            return calculateMethod != null
                ? calculateMethod.Invoke(null, new[] { value }).ToString()
                : value switch
                {
                    byte[] bytes => Hash128.Compute(bytes).ToString(),
                    string text => Hash128.Compute(text).ToString(),
                    _ => Hash128.Compute(value.ToString()).ToString()
                };
        }

        private static void ValidatePackageFiles(
            string root,
            IEnumerable<ContentArtifactRecord> files)
        {
            foreach (var file in files)
            {
                var path = ResolveWithin(root, file.relativePath);
                ValidateFile(path, file);
            }
        }

        private static void ValidateFile(string path, ContentArtifactRecord file)
        {
            if (!ContentPipelineFileSystem.FileExists(path))
                throw new FileNotFoundException("Dynamic package file is missing.", path);
            if (ContentPipelineFileSystem.GetFileLength(path) != file.size ||
                !string.Equals(ContentPipelineHash.Sha256File(path), file.sha256, StringComparison.Ordinal))
                throw new InvalidDataException($"Dynamic package file failed integrity validation: {path}");
        }

        private static string ResolveArtifactPath(string manifestPath, string relativePath)
        {
            return ResolveWithin(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, relativePath);
        }

        private static string ResolveWithin(string root, string relativePath)
        {
            var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!ContentPipelineFileSystem.IsWithin(path, root))
                throw new InvalidDataException($"Unsafe relative package path: {relativePath}");
            return path;
        }

        private readonly struct BundleSource
        {
            public BundleSource(string path, ContentArtifactRecord record)
            {
                Path = path;
                Record = record ?? throw new ArgumentNullException(nameof(record));
            }

            public string Path { get; }

            public ContentArtifactRecord Record { get; }

            public string[] Scopes => Record.sourceScopes ?? Array.Empty<string>();
        }

#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
        private readonly struct CatalogLocationKey : IEquatable<CatalogLocationKey>
        {
            private readonly string _primaryKey;
            private readonly string _internalId;
            private readonly string _providerId;
            private readonly Type _resourceType;
            private readonly int _dependencyHashCode;

            public CatalogLocationKey(IResourceLocation location)
            {
                _primaryKey = location.PrimaryKey;
                _internalId = location.InternalId;
                _providerId = location.ProviderId;
                _resourceType = location.ResourceType;
                _dependencyHashCode = location.DependencyHashCode;
            }

            public bool Equals(CatalogLocationKey other)
            {
                return string.Equals(_primaryKey, other._primaryKey, StringComparison.Ordinal) &&
                       string.Equals(_internalId, other._internalId, StringComparison.Ordinal) &&
                       string.Equals(_providerId, other._providerId, StringComparison.Ordinal) &&
                       Equals(_resourceType, other._resourceType) &&
                       _dependencyHashCode == other._dependencyHashCode;
            }

            public override bool Equals(object obj)
            {
                return obj is CatalogLocationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_primaryKey ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_internalId ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_providerId ?? string.Empty);
                    hash = hash * 31 + (_resourceType?.GetHashCode() ?? 0);
                    hash = hash * 31 + _dependencyHashCode;
                    return hash;
                }
            }
        }
#endif
    }
}
