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
    /// Defines the stable physical layout of dynamic content release indexes.
    /// Complete build identity remains stored in the release manifest.
    /// </summary>
    public static class DynamicContentReleaseLayout
    {
        public const string BaselineContainerName = "baselines";
        public const string UpdateContainerName = "updates";
        public const int ReleaseDirectoryIdLength = 32;

        public static string GetContainerName(bool isUpdate)
        {
            return isUpdate ? UpdateContainerName : BaselineContainerName;
        }

        public static string GetReleaseDirectoryName(string buildId)
        {
            if (string.IsNullOrWhiteSpace(buildId))
                throw new InvalidDataException("Dynamic release build id is empty.");
            return buildId.Length <= ReleaseDirectoryIdLength
                ? buildId
                : buildId[..ReleaseDirectoryIdLength];
        }
    }

    [Serializable]
    public sealed class DynamicContentBundleSourceRecord
    {
        public string bundleName;
        public long size;
        public string sha256;
        public string artifactBuildId;
        public string artifactManifestRelativePath;
        public string artifactFileRelativePath;
    }

    [Serializable]
    public sealed class DynamicContentReleaseManifest
    {
        public int schemaVersion = 1;
        public string buildKind;
        public string buildId;
        public string baselineId;
        public string dynamicLoadPath;
        public string catalogRelativePath;
        public string catalogHashRelativePath;
        public string[] referencedBundles = Array.Empty<string>();
        public DynamicContentBundleSourceRecord[] bundleSources = Array.Empty<DynamicContentBundleSourceRecord>();
        public ContentArtifactRecord[] files = Array.Empty<ContentArtifactRecord>();

        public static DynamicContentReleaseManifest Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !ContentPipelineFileSystem.FileExists(path))
                throw new FileNotFoundException("Dynamic content release manifest was not found.", path);
            var manifest = JsonUtility.FromJson<DynamicContentReleaseManifest>(
                ContentPipelineFileSystem.ReadAllText(path));
            if (manifest == null || manifest.schemaVersion != 1)
                throw new InvalidDataException($"Unsupported dynamic content release manifest: {path}");
            manifest.dynamicLoadPath = NormalizeDynamicLoadPath(manifest.dynamicLoadPath);
            manifest.Validate(path);
            return manifest;
        }

        public void Save(string path)
        {
            dynamicLoadPath = NormalizeDynamicLoadPath(dynamicLoadPath);
            Validate(path);
            ContentPipelineFileSystem.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            ContentPipelineFileSystem.WriteAllText(path, JsonUtility.ToJson(this, true));
        }

        private void Validate(string path)
        {
            if (schemaVersion != 1 ||
                string.IsNullOrWhiteSpace(buildKind) ||
                string.IsNullOrWhiteSpace(buildId) ||
                string.IsNullOrWhiteSpace(baselineId) ||
                string.IsNullOrWhiteSpace(catalogRelativePath) ||
                string.IsNullOrWhiteSpace(catalogHashRelativePath) ||
                referencedBundles == null ||
                bundleSources == null ||
                files == null)
                throw new InvalidDataException($"Invalid dynamic content release manifest: {path}");
            var referenced = new HashSet<string>(referencedBundles, StringComparer.OrdinalIgnoreCase);
            if (referenced.Count != referencedBundles.Length)
                throw new InvalidDataException($"Dynamic release contains duplicate Bundle references: {path}");
            var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in bundleSources)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.bundleName) ||
                    source.size < 0 || source.sha256?.Length != 64 ||
                    source.artifactBuildId?.Length != 64 ||
                    string.IsNullOrWhiteSpace(source.artifactManifestRelativePath) ||
                    string.IsNullOrWhiteSpace(source.artifactFileRelativePath) ||
                    !sources.Add(source.bundleName))
                    throw new InvalidDataException($"Invalid dynamic bundle source mapping: {path}");
            }
            if (!referenced.SetEquals(sources))
                throw new InvalidDataException($"Dynamic release Bundle source mapping is incomplete: {path}");
        }

        internal static string NormalizeDynamicLoadPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException("Dynamic content release load path is empty.");
            var normalized = value.Trim().TrimEnd('/', '\\');
            if (normalized.Length == 0)
                throw new InvalidDataException("Dynamic content release load path is empty.");
            return normalized;
        }
    }

    public sealed class DynamicContentReleaseRequest
    {
        public string ArtifactManifestPath { get; set; }

        public string StorageRoot { get; set; }

        public string OutputRoot { get; set; }

        public string DynamicLoadPath { get; set; }

        public string BaselineReleaseManifestPath { get; set; }

        public string PreviousReleaseManifestPath { get; set; }
    }

    public sealed class DynamicContentReleaseResult
    {
        public string OutputPath { get; internal set; }

        public string ManifestPath { get; internal set; }

        public DynamicContentReleaseManifest Manifest { get; internal set; }
    }

    /// <summary>
    /// Indexes immutable Addressables Artifacts without duplicating Bundle payloads.
    /// The Release is committed only after every Catalog Bundle reference is accounted for.
    /// </summary>
    public sealed class DynamicContentReleaseBuilder
    {
        public const string ReleaseManifestName = "release-manifest.json";
        private const string CatalogHashName = "catalog.hash";
        private const string BundleExtension = ".bundle";
        private static readonly object BuildGate = new();

        public DynamicContentReleaseResult Build(DynamicContentReleaseRequest request)
        {
            lock (BuildGate)
            {
                return BuildInternal(request);
            }
        }

        private static DynamicContentReleaseResult BuildInternal(DynamicContentReleaseRequest request)
        {
            ValidateRequest(request);
            var dynamicLoadPath =
                DynamicContentReleaseManifest.NormalizeDynamicLoadPath(request.DynamicLoadPath);
            var storageRoot = Path.GetFullPath(request.StorageRoot);
            var artifactManifestPath = ResolveWithinStorage(storageRoot, request.ArtifactManifestPath);
            var artifactManifest = ContentArtifactManifest.Load(artifactManifestPath);
            var isUpdate = string.Equals(
                artifactManifest.buildKind,
                ContentPipelineBuildKind.Update.ToString().ToLowerInvariant(),
                StringComparison.Ordinal);
            DynamicContentReleaseManifest baselineRelease = null;
            DynamicContentReleaseManifest previousRelease = null;
            if (isUpdate)
            {
                if (string.IsNullOrWhiteSpace(request.BaselineReleaseManifestPath) ||
                    string.IsNullOrWhiteSpace(request.PreviousReleaseManifestPath))
                {
                    throw new ArgumentException(
                        "An update Release requires both Baseline and previous Release Manifests.",
                        nameof(request));
                }
                baselineRelease = DynamicContentReleaseManifest.Load(request.BaselineReleaseManifestPath);
                previousRelease = DynamicContentReleaseManifest.Load(request.PreviousReleaseManifestPath);
                if (!string.Equals(baselineRelease.buildId, artifactManifest.baselineId, StringComparison.Ordinal) ||
                    !string.Equals(previousRelease.baselineId, artifactManifest.baselineId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Update Baseline '{artifactManifest.baselineId}' does not match Release " +
                        $"lineage '{previousRelease.baselineId}'.");
                }
            }

            var outputRoot = Path.GetFullPath(request.OutputRoot);
            if (!ContentPipelineFileSystem.IsWithin(outputRoot, storageRoot))
                throw new InvalidDataException($"Release output must be inside its storage root: {outputRoot}");
            using var processLock = ContentBuildProcessLock.Acquire(storageRoot);
            var collectionName = DynamicContentReleaseLayout.GetContainerName(isUpdate);
            var finalRoot = Path.Combine(
                outputRoot,
                collectionName,
                DynamicContentReleaseLayout.GetReleaseDirectoryName(artifactManifest.buildId));
            var finalManifestPath = Path.Combine(finalRoot, ReleaseManifestName);
            if (ContentPipelineFileSystem.FileExists(finalManifestPath))
            {
                var existing = DynamicContentReleaseManifest.Load(finalManifestPath);
                ValidateExistingRelease(existing, finalRoot, artifactManifest, storageRoot, dynamicLoadPath);
                return CreateResult(finalRoot, existing);
            }

            var stagingParent = Path.Combine(outputRoot, ".staging");
            ContentPipelineFileSystem.CreateDirectory(stagingParent);
            var stagingRoot = Path.Combine(stagingParent, Guid.NewGuid().ToString("N"));
            ContentPipelineFileSystem.CreateDirectory(stagingRoot);
            try
            {
                var catalogArtifact = SelectRemoteCatalog(artifactManifest);
                var catalogSource = ResolveArtifactPath(artifactManifestPath, catalogArtifact.relativePath);
                var catalogDestination = Path.Combine(stagingRoot, "catalog" + Path.GetExtension(catalogSource));
                ValidateFile(catalogSource, catalogArtifact);
                ContentPipelineFileSystem.CopyFile(catalogSource, catalogDestination, true);

                var candidateBundles = BuildCandidateBundleMap(artifactManifest, artifactManifestPath);
                var availableBundles = BuildReleaseBundleMap(baselineRelease, storageRoot, true);
                foreach (var pair in BuildReleaseBundleMap(previousRelease, storageRoot, true))
                    availableBundles[pair.Key] = pair.Value;
                foreach (var pair in candidateBundles)
                    availableBundles[pair.Key] = pair.Value;

                var referencedBundles = RewriteCatalog(
                    catalogDestination,
                    bundleName => $"{dynamicLoadPath}/{bundleName}",
                    new HashSet<string>(availableBundles.Keys, StringComparer.OrdinalIgnoreCase));
                var bundleSources = new Dictionary<string, DynamicContentBundleSourceRecord>(
                    StringComparer.OrdinalIgnoreCase);
                var artifactManifestRelativePath = GetStorageRelativePath(storageRoot, artifactManifestPath);
                foreach (var bundleName in referencedBundles)
                {
                    if (!candidateBundles.TryGetValue(bundleName, out var source))
                    {
                        var reusableSource = FindSource(previousRelease, bundleName) ??
                                             FindSource(baselineRelease, bundleName);
                        if (reusableSource == null)
                            throw new FileNotFoundException($"No Artifact source exists for Bundle '{bundleName}'.");
                        bundleSources[bundleName] = CloneSource(reusableSource);
                        continue;
                    }

                    var previousSource = FindSource(previousRelease, bundleName) ??
                                         FindSource(baselineRelease, bundleName);
                    if (previousSource != null &&
                        previousSource.size == source.Record.size &&
                        string.Equals(previousSource.sha256, source.Record.sha256, StringComparison.Ordinal))
                    {
                        bundleSources[bundleName] = CloneSource(previousSource);
                        continue;
                    }

                    ValidateFile(source.Path, source.Record);
                    bundleSources[bundleName] = new DynamicContentBundleSourceRecord
                    {
                        bundleName = bundleName,
                        size = source.Record.size,
                        sha256 = source.Record.sha256,
                        artifactBuildId = artifactManifest.buildId,
                        artifactManifestRelativePath = artifactManifestRelativePath,
                        artifactFileRelativePath = source.Record.relativePath
                    };
                }

                var catalogRelativePath = Path.GetFileName(catalogDestination);
                var files = new List<ContentArtifactRecord>
                {
                    CreateFileRecord(catalogDestination, catalogRelativePath, "catalog", Array.Empty<string>())
                };
                var catalogHashPath = Path.Combine(stagingRoot, CatalogHashName);
                ContentPipelineFileSystem.WriteAllText(catalogHashPath, CalculateAddressablesHash(catalogDestination));
                files.Add(CreateFileRecord(
                    catalogHashPath,
                    CatalogHashName,
                    "catalog-hash",
                    Array.Empty<string>()));

                ValidateReleaseFiles(stagingRoot, files);
                var releaseManifest = new DynamicContentReleaseManifest
                {
                    buildKind = artifactManifest.buildKind,
                    buildId = artifactManifest.buildId,
                    baselineId = artifactManifest.baselineId,
                    dynamicLoadPath = dynamicLoadPath,
                    catalogRelativePath = catalogRelativePath,
                    catalogHashRelativePath = CatalogHashName,
                    referencedBundles = referencedBundles.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    bundleSources = bundleSources.Values
                        .OrderBy(value => value.bundleName, StringComparer.Ordinal)
                        .ToArray(),
                    files = files.OrderBy(value => value.relativePath, StringComparer.Ordinal).ToArray()
                };
                releaseManifest.Save(Path.Combine(stagingRoot, ReleaseManifestName));

                ContentPipelineFileSystem.CreateDirectory(Path.GetDirectoryName(finalRoot)!);
                if (ContentPipelineFileSystem.DirectoryExists(finalRoot))
                    throw new IOException($"Dynamic Release output already exists without a valid Manifest: {finalRoot}");
                ContentPipelineFileSystem.MoveDirectory(stagingRoot, finalRoot);
                return CreateResult(finalRoot, releaseManifest);
            }
            catch
            {
                if (ContentPipelineFileSystem.DirectoryExists(stagingRoot))
                    ContentPipelineFileSystem.DeleteDirectory(stagingRoot, true);
                throw;
            }
        }

        private static void ValidateRequest(DynamicContentReleaseRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.ArtifactManifestPath))
                throw new ArgumentException("Artifact manifest path is empty.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.StorageRoot))
                throw new ArgumentException("Content storage root is empty.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.OutputRoot))
                throw new ArgumentException("Dynamic Release output root is empty.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.DynamicLoadPath))
                throw new ArgumentException("Dynamic load path is empty.", nameof(request));
        }

        private static DynamicContentReleaseResult CreateResult(
            string outputPath,
            DynamicContentReleaseManifest manifest)
        {
            return new DynamicContentReleaseResult
            {
                OutputPath = outputPath,
                ManifestPath = Path.Combine(outputPath, ReleaseManifestName),
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

        private static Dictionary<string, BundleSource> BuildReleaseBundleMap(
            DynamicContentReleaseManifest manifest,
            string storageRoot,
            bool validateFiles)
        {
            var result = new Dictionary<string, BundleSource>(StringComparer.OrdinalIgnoreCase);
            if (manifest == null) return result;
            foreach (var source in manifest.bundleSources)
            {
                var sourceManifest = ResolveWithin(storageRoot, source.artifactManifestRelativePath);
                var artifact = ContentArtifactManifest.Load(sourceManifest);
                if (!string.Equals(artifact.buildId, source.artifactBuildId, StringComparison.Ordinal))
                    throw new InvalidDataException(
                        $"Bundle Artifact Build ID does not match its Manifest: {sourceManifest}");
                var sourceRoot = Path.GetDirectoryName(sourceManifest)!;
                var path = ResolveWithin(sourceRoot, source.artifactFileRelativePath);
                var file = new ContentArtifactRecord
                {
                    relativePath = source.artifactFileRelativePath,
                    kind = "bundle",
                    size = source.size,
                    sha256 = source.sha256,
                    sourceScopes = Array.Empty<string>()
                };
                ValidateFileMetadata(path, file, validateFiles);
                AddBundle(
                    result,
                    source.bundleName,
                    new BundleSource(path, file),
                    "previous Release");
            }

            return result;
        }

        internal static IReadOnlyDictionary<string, string> ResolveBundlePaths(
            DynamicContentReleaseManifest manifest,
            string storageRoot,
            bool validateFiles)
        {
            return BuildReleaseBundleMap(manifest, Path.GetFullPath(storageRoot), validateFiles)
                .ToDictionary(pair => pair.Key, pair => pair.Value.Path, StringComparer.OrdinalIgnoreCase);
        }

        private static void ValidateExistingRelease(
            DynamicContentReleaseManifest release,
            string releaseRoot,
            ContentArtifactManifest artifact,
            string storageRoot,
            string expectedDynamicLoadPath)
        {
            if (!string.Equals(release.buildId, artifact.buildId, StringComparison.Ordinal) ||
                !string.Equals(release.buildKind, artifact.buildKind, StringComparison.Ordinal) ||
                !string.Equals(release.baselineId, artifact.baselineId, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Dynamic Release path contains a different build: {releaseRoot}");
            }
            if (!string.Equals(
                    release.dynamicLoadPath,
                    expectedDynamicLoadPath,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Dynamic Release '{releaseRoot}' uses load path '{release.dynamicLoadPath}', " +
                    $"but the request uses '{expectedDynamicLoadPath}'. Remove the stale Release and rebuild it.");
            }

            if (release.files.Count(file =>
                    file.kind == "catalog" &&
                    string.Equals(file.relativePath, release.catalogRelativePath, StringComparison.Ordinal)) != 1 ||
                release.files.Count(file =>
                    file.kind == "catalog-hash" &&
                    string.Equals(file.relativePath, release.catalogHashRelativePath, StringComparison.Ordinal)) != 1)
            {
                throw new InvalidDataException($"Dynamic Release Catalog records are invalid: {releaseRoot}");
            }

            ValidateReleaseFiles(releaseRoot, release.files);
            var availableBundles = BuildReleaseBundleMap(release, storageRoot, true);
            var missing = release.referencedBundles
                .Where(bundle => !availableBundles.ContainsKey(bundle))
                .OrderBy(bundle => bundle, StringComparer.Ordinal)
                .ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidDataException(
                    $"Dynamic Release is missing {missing.Length} referenced Bundles:" +
                    Environment.NewLine + string.Join(Environment.NewLine, missing));
            }
        }

        private static DynamicContentBundleSourceRecord CloneSource(
            DynamicContentBundleSourceRecord source)
        {
            return new DynamicContentBundleSourceRecord
            {
                bundleName = source.bundleName,
                size = source.size,
                sha256 = source.sha256,
                artifactBuildId = source.artifactBuildId,
                artifactManifestRelativePath = source.artifactManifestRelativePath,
                artifactFileRelativePath = source.artifactFileRelativePath
            };
        }

        private static DynamicContentBundleSourceRecord FindSource(
            DynamicContentReleaseManifest release,
            string bundleName)
        {
            return release?.bundleSources.FirstOrDefault(value =>
                string.Equals(value.bundleName, bundleName, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetStorageRelativePath(string storageRoot, string path)
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(storageRoot), Path.GetFullPath(path))
                .Replace('\\', '/');
            if (relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                throw new InvalidDataException($"Content path escaped its storage root: {path}");
            return relative;
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

        internal static string[] RewriteCatalog(
            string catalogPath,
            Func<string, string> bundleInternalIdResolver,
            ISet<string> bundles)
        {
#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
            return RewriteBinaryCatalog(catalogPath, bundleInternalIdResolver, bundles);
#else
            return RewriteJsonCatalog(catalogPath, bundleInternalIdResolver, bundles);
#endif
        }

#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
        private static string[] RewriteBinaryCatalog(
            string catalogPath,
            Func<string, string> bundleInternalIdResolver,
            ISet<string> bundles)
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
                var internalId = RewriteInternalId(
                    location.InternalId,
                    bundleInternalIdResolver,
                    bundles,
                    referenced);
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
            Func<string, string> bundleInternalIdResolver,
            ISet<string> bundles)
        {
            var catalog = JsonUtility.FromJson<ContentCatalogData>(
                ContentPipelineFileSystem.ReadAllText(catalogPath));
            if (catalog?.InternalIds == null)
                throw new InvalidDataException($"JSON content catalog has no internal IDs: {catalogPath}");
            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < catalog.InternalIds.Length; i++)
                catalog.InternalIds[i] = RewriteInternalId(
                    catalog.InternalIds[i],
                    bundleInternalIdResolver,
                    bundles,
                    referenced);
            ContentPipelineFileSystem.WriteAllText(catalogPath, JsonUtility.ToJson(catalog));
            return referenced.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
#endif

        private static string RewriteInternalId(
            string internalId,
            Func<string, string> bundleInternalIdResolver,
            ISet<string> bundles,
            ISet<string> referenced)
        {
            if (!TryGetBundleName(internalId, out var bundleName)) return internalId;
            if (!bundles.Contains(bundleName))
                throw new FileNotFoundException(
                    $"Catalog references Bundle '{bundleName}' that is absent from the candidate and Baseline Releases.");
            referenced.Add(bundleName);
            return bundleInternalIdResolver(bundleName);
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

        private static void ValidateReleaseFiles(
            string root,
            IEnumerable<ContentArtifactRecord> files)
        {
            foreach (var file in files)
            {
                var path = ResolveWithin(root, file.relativePath);
                ValidateFile(path, file);
            }
        }

        internal static void ValidateFile(string path, ContentArtifactRecord file)
        {
            ValidateFileMetadata(path, file, true);
        }

        private static void ValidateFileMetadata(
            string path,
            ContentArtifactRecord file,
            bool validateHash)
        {
            if (!ContentPipelineFileSystem.FileExists(path))
                throw new FileNotFoundException("Dynamic content file is missing.", path);
            if (ContentPipelineFileSystem.GetFileLength(path) != file.size ||
                (validateHash && !string.Equals(
                    ContentPipelineHash.Sha256File(path),
                    file.sha256,
                    StringComparison.Ordinal)))
                throw new InvalidDataException($"Dynamic content file failed integrity validation: {path}");
        }

        private static string ResolveArtifactPath(string manifestPath, string relativePath)
        {
            return ResolveWithin(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, relativePath);
        }

        private static string ResolveWithinStorage(string storageRoot, string path)
        {
            var resolved = Path.GetFullPath(path);
            if (!ContentPipelineFileSystem.IsWithin(resolved, storageRoot))
                throw new InvalidDataException($"Artifact Manifest escaped its storage root: {path}");
            return resolved;
        }

        private static string ResolveWithin(string root, string relativePath)
        {
            var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!ContentPipelineFileSystem.IsWithin(path, root))
                throw new InvalidDataException($"Unsafe relative content path: {relativePath}");
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
