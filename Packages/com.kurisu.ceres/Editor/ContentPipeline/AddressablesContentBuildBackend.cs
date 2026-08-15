using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Ceres.ContentPipeline
{
    public sealed class AddressablesContentBuildBackend
    {
        private const string ManifestFileName = "artifact-manifest.json";
        private const string SupportedAddressablesVersionPrefix = "2.9.";
        private const string BackendIdentity = "ceres-addressables-backend-v1";
        private static readonly object BuildGate = new();

        public ContentPipelineBuildResult Build(ContentPipelineBuildRequest request)
        {
            var result = new ContentPipelineBuildResult();
            try
            {
                lock (BuildGate)
                {
                    BuildInternal(request, result);
                }
            }
            catch (Exception exception)
            {
                result.Exception = exception;
                Debug.LogException(exception);
            }

            return result;
        }

        public static string GetCurrentBaselineManifestPath(
            string outputRoot,
            string channel,
            BuildTarget target)
        {
            var platformRoot = ContentPipelinePlatformPath.GetPlatformRoot(outputRoot, channel, target);
            return GetPointerManifestPath(
                platformRoot,
                "current-baseline.json",
                ContentPipelineBuildKind.Baseline);
        }

        public static string GetCurrentPackingManifestPath(
            string outputRoot,
            string channel,
            BuildTarget target)
        {
            var platformRoot = ContentPipelinePlatformPath.GetPlatformRoot(outputRoot, channel, target);
            var baselinePath = GetPointerManifestPath(
                platformRoot,
                "current-baseline.json",
                ContentPipelineBuildKind.Baseline);
            if (string.IsNullOrEmpty(baselinePath)) return string.Empty;
            var updatePath = GetPointerManifestPath(
                platformRoot,
                "latest-update-candidate.json",
                ContentPipelineBuildKind.Update);
            if (string.IsNullOrEmpty(updatePath)) return baselinePath;
            var baseline = ContentArtifactManifest.Load(baselinePath);
            var update = ContentArtifactManifest.Load(updatePath);
            return string.Equals(update.baselineId, baseline.baselineId, StringComparison.Ordinal) &&
                   string.Equals(
                       update.configurationFingerprint,
                       baseline.configurationFingerprint,
                       StringComparison.Ordinal)
                ? updatePath
                : baselinePath;
        }

        private static void BuildInternal(
            ContentPipelineBuildRequest request,
            ContentPipelineBuildResult output)
        {
            ValidateRequest(request);
            var addressablesVersion = GetPackageVersion(typeof(AddressableAssetSettings).Assembly);
            if (!addressablesVersion.StartsWith(SupportedAddressablesVersionPrefix, StringComparison.Ordinal))
            {
                throw new NotSupportedException(
                    $"Ceres transient Addressables backend supports Addressables {SupportedAddressablesVersionPrefix}x, " +
                    $"but the project uses {addressablesVersion}. Refuse to guess internal Group identity behavior.");
            }
            AddressablesCompatibility.ValidateApi();

            var sbpVersion = GetPackageVersion(typeof(UnityEditor.Build.Pipeline.ContentPipeline).Assembly);
            var platformRoot = ContentPipelinePlatformPath.GetPlatformRoot(
                request.OutputRoot,
                request.Channel,
                request.Target);
            var stagingParent = Path.Combine(platformRoot, ".staging");
            ContentPipelineFileSystem.CreateDirectory(stagingParent);
            using var processLock = ContentBuildProcessLock.Acquire(platformRoot);
            var projectBuildRoot = Path.Combine(
                Path.GetDirectoryName(Application.dataPath)!,
                "Library",
                "CeresContentPipeline");
            using var addressablesBuildLock = ContentBuildProcessLock.Acquire(projectBuildRoot);
            using var session = new AddressablesBuildSession(
                Path.Combine(stagingParent, Guid.NewGuid().ToString("N")));

            var stopwatch = Stopwatch.StartNew();
            var configurationFingerprint = CreateConfigurationFingerprint(request, addressablesVersion, sbpVersion);
            var currentSnapshot = ContentBuildSnapshot.Create(request.Graph);
            ContentArtifactManifest baseline = null;
            string contentStatePath = null;
            string[] impactedScopes = Array.Empty<string>();
            ContentArtifactManifest previousPackingManifest = null;
            if (request.BuildKind == ContentPipelineBuildKind.Update)
            {
                baseline = ContentArtifactManifest.Load(request.BaselineManifestPath);
                ValidateBaselineCompatibility(
                    request,
                    baseline,
                    configurationFingerprint,
                    addressablesVersion,
                    sbpVersion);
                impactedScopes = ContentBuildChangeValidator.Validate(
                    baseline,
                    currentSnapshot,
                    request.AllowedChangedScopeIds);
                contentStatePath = ResolveArtifactPath(request.BaselineManifestPath, baseline.contentStateRelativePath);
                previousPackingManifest = LoadPreviousPackingManifest(request, baseline);
            }

            var partitionPlan = ContentBundlePartitionPlanner.Plan(
                request.Graph,
                request.Packing,
                previousPackingManifest);
            AddressableAssetSettings settings = null;
            BuildScriptPackedMode builder = null;
            AddressablesPlayerBuildResult addressablesResult = null;
            Dictionary<string, PartitionRuntime> partitions = null;
            IDisposable defaultSettingsOverride = null;
            IDisposable buildLayoutOverride = null;
            try
            {
                settings = CreateSettings(
                    request,
                    session,
                    configurationFingerprint,
                    partitionPlan,
                    out builder,
                    out partitions,
                    out defaultSettingsOverride);
                AddressablesCompatibility.ValidateTransientSettings(settings);
                buildLayoutOverride = AddressablesCompatibility.DisableBuildLayout();
                var buildInput = new AddressablesDataBuilderInput(
                    settings,
                    request.PlayerVersion,
                    new RequestBuildSettingsProvider(request));
                addressablesResult = request.BuildKind == ContentPipelineBuildKind.Baseline
                    ? builder.BuildData<AddressablesPlayerBuildResult>(buildInput)
                    : ContentUpdateScript.BuildContentUpdate(settings, contentStatePath);
                if (addressablesResult == null)
                {
                    throw new InvalidOperationException("Addressables build returned no result.");
                }

                if (!string.IsNullOrWhiteSpace(addressablesResult.Error))
                {
                    throw new InvalidOperationException($"Addressables build failed: {addressablesResult.Error}");
                }

                ValidateAddressablesArtifacts(request, session, addressablesResult);

                var artifacts = CollectArtifacts(
                    session,
                    addressablesResult,
                    partitions,
                    baseline,
                    contentStatePath,
                    request.BuildKind == ContentPipelineBuildKind.Update);
                var manifest = CreateManifest(
                    request,
                    addressablesVersion,
                    sbpVersion,
                    configurationFingerprint,
                    currentSnapshot,
                    artifacts,
                    addressablesResult,
                    session,
                    baseline,
                    impactedScopes,
                    partitionPlan);
                var manifestPath = Path.Combine(session.Root, ManifestFileName);
                manifest.Save(manifestPath);
                Commit(platformRoot, session, manifest, out var committedPath);
                output.OutputPath = committedPath;
                output.ManifestPath = Path.Combine(committedPath, ManifestFileName);
                output.Manifest = manifest;
                var bundleStatistics = manifest.bundleStatistics;
                Debug.Log(
                    $"[Ceres.ContentPipeline] {request.BuildKind} '{manifest.buildId}' completed in " +
                    $"{stopwatch.Elapsed.TotalSeconds:F2}s with {manifest.artifacts.Length} artifacts. " +
                    $"packing={manifest.packing.mode}, target={FormatBytes(manifest.packing.targetBundleSizeBytes)}, " +
                    $"partitions={bundleStatistics.plannedPartitionCount}, bundles={bundleStatistics.bundleCount}, " +
                    $"bundleBytes={FormatBytes(bundleStatistics.bundleBytes)}, " +
                    $"managedUnder64KiB={bundleStatistics.managedBundlesUnder64KiB}/" +
                    $"{bundleStatistics.managedBundleCount}, " +
                    $"estimatedDuplicate={FormatBytes(bundleStatistics.estimatedCrossPartitionDuplicateBytes)}, " +
                    $"oversized={bundleStatistics.oversizedBundleCount}: {committedPath}");
            }
            finally
            {
                buildLayoutOverride?.Dispose();
                defaultSettingsOverride?.Dispose();
                DestroyTransientSettings(settings, builder);
            }
        }

        private static void ValidateRequest(ContentPipelineBuildRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Graph == null) throw new ArgumentException("Build graph is missing.", nameof(request));
            if (!request.Graph.IsBuildable)
            {
                var errors = request.Graph.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == ContentDiagnosticSeverity.Error)
                    .Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}");
                throw new InvalidOperationException(
                    "Content graph is not buildable." + Environment.NewLine + string.Join(Environment.NewLine, errors));
            }

            if (string.IsNullOrWhiteSpace(request.OutputRoot))
                throw new ArgumentException("Output root is empty.", nameof(request));
            ContentPipelinePlatformPath.ValidateChannel(request.Channel);
            if (string.IsNullOrWhiteSpace(request.PlayerVersion))
                throw new ArgumentException("Player version is empty.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.RemoteLoadPath))
                throw new ArgumentException("Remote load path is empty.", nameof(request));
            if (request.BuildKind == ContentPipelineBuildKind.Update &&
                string.IsNullOrWhiteSpace(request.BaselineManifestPath))
                throw new ArgumentException("An update build requires a baseline manifest.", nameof(request));
            ContentBundlePartitionPlanner.Normalize(request.Packing);
        }

        private static AddressableAssetSettings CreateSettings(
            ContentPipelineBuildRequest request,
            AddressablesBuildSession session,
            string configurationFingerprint,
            ContentBundlePartitionPlan partitionPlan,
            out BuildScriptPackedMode builder,
            out Dictionary<string, PartitionRuntime> partitionMap,
            out IDisposable defaultSettingsOverride)
        {
            var settings = AddressableAssetSettings.Create(
                "Library/CeresContentPipeline",
                "TransientContentSettings",
                false,
                false);
            settings.hideFlags = HideFlags.HideAndDontSave;
            settings.OverridePlayerVersion = request.PlayerVersion;
            settings.BuildRemoteCatalog = true;
            settings.ContentStateBuildPath = session.StateRoot;
            settings.InternalBundleIdMode = BundledAssetGroupSchema.BundleInternalIdMode.GroupGuid;
            settings.BuiltInBundleNaming = BuiltInBundleNaming.Custom;
            settings.BuiltInBundleCustomNaming = $"aic-{request.Channel}-{request.Target}-{configurationFingerprint[..10]}";
            settings.MonoScriptBundleNaming = MonoScriptBundleNaming.Custom;
            settings.MonoScriptBundleCustomNaming = $"aic-{request.Channel}-{request.Target}-{configurationFingerprint[..10]}";

            var profile = settings.activeProfileId;
            settings.profileSettings.SetValue(profile, AddressableAssetSettings.kRemoteBuildPath, EnsureTrailingSlash(session.RemoteRoot));
            settings.profileSettings.SetValue(profile, AddressableAssetSettings.kRemoteLoadPath, EnsureTrailingSlash(request.RemoteLoadPath));
            // Non-persisted Settings intentionally skip parts of the serialized default initialization.
            settings.RemoteCatalogBuildPath = new ProfileValueReference();
            settings.RemoteCatalogLoadPath = new ProfileValueReference();
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);

            builder = ScriptableObject.CreateInstance<BuildScriptPackedMode>();
            builder.name = "Ceres Transient Packed Build";
            builder.hideFlags = HideFlags.HideAndDontSave;
            settings.AddDataBuilder(builder, false);
            settings.ActivePlayerDataBuilderIndex = 0;

            partitionMap = CreatePartitions(partitionPlan, settings, request);
            // Addressables still consults its static default during Packed Build, but entry creation
            // consults the persisted default ConfigFolder. Switch only after the transient model is complete.
            defaultSettingsOverride = AddressablesCompatibility.OverrideDefaultSettings(settings);
            return settings;
        }

        private static Dictionary<string, PartitionRuntime> CreatePartitions(
            ContentBundlePartitionPlan plan,
            AddressableAssetSettings settings,
            ContentPipelineBuildRequest request)
        {
            var runtime = new Dictionary<string, PartitionRuntime>(StringComparer.Ordinal);

            var infrastructure = CreateGroup(
                settings,
                "infrastructure",
                ContentLocation.Local,
                request,
                setAsDefault: true);
            runtime[infrastructure.Group.Guid] = infrastructure;

            foreach (var definition in plan.Partitions)
            {
                var partition = CreateGroup(
                    settings,
                    definition.Id,
                    definition.Location,
                    request,
                    false);
                runtime[partition.Group.Guid] = partition;
                foreach (var node in definition.Nodes)
                {
                    var guid = AssetDatabase.AssetPathToGUID(node.AssetPath);
                    if (string.IsNullOrEmpty(guid))
                    {
                        throw new InvalidOperationException(
                            $"Asset '{node.AssetPath}' in partition '{definition.Id}' has no AssetDatabase GUID.");
                    }

                    var entry = settings.CreateOrMoveEntry(guid, partition.Group, false, false);
                    entry.address = string.IsNullOrWhiteSpace(node.Address)
                        ? $"__content/{node.AssetId}"
                        : node.Address;
                    foreach (var label in node.Labels)
                    {
                        entry.SetLabel(label, true, true, false);
                    }

                    partition.ScopeIds.UnionWith(node.UsageScopeIds);
                }
            }

            return runtime;
        }

        private static PartitionRuntime CreateGroup(
            AddressableAssetSettings settings,
            string partitionId,
            ContentLocation location,
            ContentPipelineBuildRequest request,
            bool setAsDefault)
        {
            var stableSeed = string.Join("|", "ceres-content-group-v1", request.Channel, request.Target, partitionId);
            var stableGuid = ContentPipelineHash.Sha256(stableSeed)[..32];
            // Keep the physical bundle filename well below Windows MAX_PATH. Full logical identity
            // remains in the deterministic GUID and manifest; the display name is only a short hint.
            var locationToken = location == ContentLocation.Local ? "l" : "r";
            var groupName = $"aic-{locationToken}-{ShortPartitionLabel(partitionId)}-{stableGuid[..10]}";
            var group = AddressablesCompatibility.CreateStableGroup(
                settings,
                groupName,
                stableGuid);
            if (setAsDefault) AddressablesCompatibility.SetDefaultGroup(settings, group);

            var bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
            bundleSchema.BuildPath.SetVariableByName(
                settings,
                location == ContentLocation.Local
                    ? AddressableAssetSettings.kLocalBuildPath
                    : AddressableAssetSettings.kRemoteBuildPath);
            bundleSchema.LoadPath.SetVariableByName(
                settings,
                location == ContentLocation.Local
                    ? AddressableAssetSettings.kLocalLoadPath
                    : AddressableAssetSettings.kRemoteLoadPath);
            bundleSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundleSchema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.AppendHash;
            bundleSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundleSchema.IncludeAddressInCatalog = true;
            bundleSchema.IncludeGUIDInCatalog = false;
            bundleSchema.IncludeLabelsInCatalog = true;
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = false;
            return new PartitionRuntime(group, partitionId, location);
        }

        private static List<ContentArtifactRecord> CollectArtifacts(
            AddressablesBuildSession session,
            AddressablesPlayerBuildResult result,
            IReadOnlyDictionary<string, PartitionRuntime> partitions,
            ContentArtifactManifest baseline,
            string baselineContentStatePath,
            bool update)
        {
            var sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (result.FileRegistry != null)
            {
                foreach (var path in result.FileRegistry.GetFilePaths())
                {
                    if (!string.IsNullOrWhiteSpace(path) &&
                        ContentPipelineFileSystem.FileExists(path))
                    {
                        sourcePaths.Add(Path.GetFullPath(path));
                    }
                }
            }

            AddFile(sourcePaths, result.ContentStateFilePath);
            AddFile(sourcePaths, result.RemoteCatalogHashFilePath);
            AddFile(sourcePaths, result.RemoteCatalogJsonFilePath);
            if (update) AddFile(sourcePaths, baselineContentStatePath);
            foreach (var bundle in result.AssetBundleBuildResults)
            {
                AddFile(sourcePaths, bundle.FilePath);
            }

            var bundlePartitions = result.AssetBundleBuildResults
                .Where(bundle => bundle.SourceAssetGroup && !string.IsNullOrEmpty(bundle.FilePath))
                .GroupBy(bundle => Path.GetFullPath(bundle.FilePath), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => partitions.TryGetValue(group.First().SourceAssetGroup.Guid, out var partition)
                        ? new BundlePartitionMetadata(
                            partition.PartitionId,
                            partition.ScopeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray())
                        : BundlePartitionMetadata.Empty,
                    StringComparer.OrdinalIgnoreCase);
            var records = new List<ContentArtifactRecord>();
            foreach (var sourcePath in sourcePaths.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                var relativePath = GetArtifactRelativePath(session, sourcePath);
                // Content updates are remote release candidates. Local catalog/settings belong to
                // the Player baseline and must never be copied into an update payload.
                if (update && relativePath.StartsWith("local/", StringComparison.Ordinal))
                    continue;

                var destination = Path.Combine(session.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!string.Equals(
                        ContentPipelineFileSystem.Normalize(sourcePath),
                        ContentPipelineFileSystem.Normalize(destination),
                        StringComparison.OrdinalIgnoreCase))
                {
                    ContentPipelineFileSystem.CreateDirectory(Path.GetDirectoryName(destination)!);
                    ContentPipelineFileSystem.CopyFile(sourcePath, destination, true);
                }

                var record = new ContentArtifactRecord
                {
                    relativePath = relativePath,
                    kind = ClassifyArtifact(relativePath),
                    size = ContentPipelineFileSystem.GetFileLength(destination),
                    sha256 = ContentPipelineHash.Sha256File(destination),
                    partitionId = bundlePartitions.TryGetValue(sourcePath, out var partition)
                        ? partition.PartitionId
                        : string.Empty,
                    sourceScopes = partition?.ScopeIds ?? Array.Empty<string>()
                };
                if (!update || !IsUnchangedBaselineArtifact(record, baseline))
                {
                    records.Add(record);
                }
                else if (ContentPipelineFileSystem.FileExists(destination))
                {
                    ContentPipelineFileSystem.DeleteFile(destination);
                }
            }

            return records.OrderBy(record => record.relativePath, StringComparer.Ordinal).ToList();
        }

        private static bool IsUnchangedBaselineArtifact(
            ContentArtifactRecord candidate,
            ContentArtifactManifest baseline)
        {
            if (baseline == null) return false;
            if (candidate.kind is "catalog" or "catalog-hash" or "content-state")
                return false;
            return baseline.artifacts.Any(existing =>
                string.Equals(existing.relativePath, candidate.relativePath, StringComparison.Ordinal) &&
                string.Equals(existing.sha256, candidate.sha256, StringComparison.Ordinal));
        }

        private static ContentArtifactManifest CreateManifest(
            ContentPipelineBuildRequest request,
            string addressablesVersion,
            string sbpVersion,
            string configurationFingerprint,
            ContentBuildSnapshot snapshot,
            IReadOnlyList<ContentArtifactRecord> artifacts,
            AddressablesPlayerBuildResult buildResult,
            AddressablesBuildSession session,
            ContentArtifactManifest baseline,
            string[] impactedScopes,
            ContentBundlePartitionPlan partitionPlan)
        {
            var artifactIdentity = string.Join(
                "\n",
                artifacts.Select(artifact => $"{artifact.relativePath}|{artifact.size}|{artifact.sha256}"));
            var buildId = ContentPipelineHash.Sha256(string.Join(
                "\n",
                ContentArtifactManifest.BuildIdentity,
                request.BuildKind,
                request.Graph.Fingerprint,
                configurationFingerprint,
                baseline?.baselineId ?? string.Empty,
                artifactIdentity));
            var contentStateRelativePath = string.Empty;
            if (!string.IsNullOrEmpty(buildResult.ContentStateFilePath) &&
                ContentPipelineFileSystem.FileExists(buildResult.ContentStateFilePath))
            {
                contentStateRelativePath = GetArtifactRelativePath(session, buildResult.ContentStateFilePath);
            }
            else if (baseline != null)
            {
                contentStateRelativePath = artifacts
                    .FirstOrDefault(artifact => artifact.kind == "content-state")
                    ?.relativePath ?? string.Empty;
            }

            var partitionSnapshots = CreatePartitionSnapshots(partitionPlan, artifacts);
            return new ContentArtifactManifest
            {
                schemaVersion = ContentArtifactManifest.CurrentSchemaVersion,
                buildKind = request.BuildKind.ToString().ToLowerInvariant(),
                buildId = buildId,
                baselineId = request.BuildKind == ContentPipelineBuildKind.Baseline
                    ? buildId
                    : baseline.baselineId,
                channel = request.Channel,
                platform = request.Target.ToString(),
                playerVersion = request.PlayerVersion,
                graphFingerprint = request.Graph.Fingerprint,
                configurationFingerprint = configurationFingerprint,
                unityVersion = Application.unityVersion,
                addressablesVersion = addressablesVersion,
                scriptableBuildPipelineVersion = sbpVersion,
                remoteLoadPath = request.RemoteLoadPath,
                contentStateRelativePath = contentStateRelativePath,
                scopes = snapshot.Scopes,
                assets = snapshot.Assets,
                artifacts = artifacts.ToArray(),
                allowedChangedScopeIds = (request.AllowedChangedScopeIds ?? Array.Empty<string>())
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                impactedScopeIds = impactedScopes,
                packing = new ContentBundlePackingSnapshot
                {
                    mode = partitionPlan.Options.Mode.ToString(),
                    targetBundleSizeBytes = partitionPlan.Options.TargetBundleSizeBytes,
                    algorithmVersion = partitionPlan.Options.AlgorithmVersion,
                    classifierVersion = ContentBundlePartitionPlanner.ClassifierVersion
                },
                partitions = partitionSnapshots,
                bundleStatistics = CreateBundleStatistics(
                    partitionPlan,
                    partitionSnapshots,
                    artifacts,
                    impactedScopes)
            };
        }

        private static ContentBundlePartitionSnapshot[] CreatePartitionSnapshots(
            ContentBundlePartitionPlan plan,
            IReadOnlyCollection<ContentArtifactRecord> artifacts)
        {
            return plan.Partitions.Select(partition =>
            {
                var actual = artifacts
                    .Where(artifact =>
                        artifact.kind == "bundle" &&
                        string.Equals(artifact.partitionId, partition.Id, StringComparison.Ordinal))
                    .Sum(artifact => artifact.size);
                if (actual == 0) actual = partition.PreviousActualBytes;
                return new ContentBundlePartitionSnapshot
                {
                    id = partition.Id,
                    family = partition.Family,
                    location = partition.Location.ToString(),
                    containsScenes = partition.ContainsScenes,
                    estimatedBytes = partition.EstimatedBytes,
                    actualBytes = actual,
                    assetIds = partition.Nodes
                        .Select(node => node.AssetId)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    sourceScopes = partition.SourceScopeIds.ToArray()
                };
            }).OrderBy(partition => partition.id, StringComparer.Ordinal).ToArray();
        }

        private static ContentBundleBuildStatistics CreateBundleStatistics(
            ContentBundlePartitionPlan plan,
            IReadOnlyCollection<ContentBundlePartitionSnapshot> partitions,
            IReadOnlyCollection<ContentArtifactRecord> artifacts,
            IReadOnlyCollection<string> impactedScopes)
        {
            var bundles = artifacts
                .Where(artifact => artifact.kind == "bundle")
                .OrderBy(artifact => artifact.size)
                .ToArray();
            var sizes = bundles.Select(bundle => bundle.size).ToArray();
            var partitionIds = new HashSet<string>(
                plan.Partitions.Select(partition => partition.Id),
                StringComparer.Ordinal);
            var managedBundles = bundles
                .Where(bundle => partitionIds.Contains(bundle.partitionId))
                .ToArray();
            var families = plan.Partitions
                .GroupBy(partition => partition.Family, StringComparer.Ordinal)
                .Select(group =>
                {
                    var ids = new HashSet<string>(
                        group.Select(partition => partition.Id),
                        StringComparer.Ordinal);
                    var familyBundles = bundles.Where(bundle => ids.Contains(bundle.partitionId)).ToArray();
                    var uniqueContent = new ContentBundleEstimatedContent();
                    foreach (var partition in group)
                    {
                        uniqueContent.UnionWith(partition.EstimatedContent);
                    }

                    var partitionSourceBytes = group.Aggregate(
                        0L,
                        (total, partition) => AddSaturating(total, partition.EstimatedBytes));
                    return new ContentBundleFamilyStatistics
                    {
                        family = group.Key,
                        partitionCount = group.Count(),
                        bundleCount = familyBundles.Length,
                        bundleBytes = familyBundles.Sum(bundle => bundle.size),
                        estimatedUniqueSourceBytes = uniqueContent.Bytes,
                        estimatedPartitionSourceBytes = partitionSourceBytes,
                        estimatedCrossPartitionDuplicateBytes = Math.Max(
                            0,
                            partitionSourceBytes - uniqueContent.Bytes)
                    };
                })
                .OrderBy(statistic => statistic.family, StringComparer.Ordinal)
                .ToArray();
            var transfers = (impactedScopes ?? Array.Empty<string>())
                .OrderBy(scopeId => scopeId, StringComparer.Ordinal)
                .Select(scopeId =>
                {
                    var changed = bundles
                        .Where(bundle => bundle.sourceScopes.Contains(scopeId, StringComparer.Ordinal))
                        .ToArray();
                    return new ContentScopeTransferStatistics
                    {
                        scopeId = scopeId,
                        changedBundleCount = changed.Length,
                        changedBundleBytes = changed.Sum(bundle => bundle.size)
                    };
                })
                .ToArray();
            return new ContentBundleBuildStatistics
            {
                plannedPartitionCount = plan.Partitions.Count,
                bundleCount = bundles.Length,
                bundleBytes = sizes.Sum(),
                p50BundleBytes = Percentile(sizes, 0.50),
                p90BundleBytes = Percentile(sizes, 0.90),
                p95BundleBytes = Percentile(sizes, 0.95),
                bundlesUnder4KiB = sizes.Count(size => size < 4L * 1024L),
                bundlesUnder64KiB = sizes.Count(size => size < 64L * 1024L),
                managedBundleCount = managedBundles.Length,
                managedBundlesUnder64KiB = managedBundles.Count(bundle => bundle.size < 64L * 1024L),
                oversizedBundleCount = partitions.Count(partition =>
                    partition.actualBytes > plan.Options.TargetBundleSizeBytes),
                listInfoBundleCount = partitions.Count(partition =>
                    string.Equals(partition.family, "info", StringComparison.Ordinal) &&
                    partition.actualBytes > 0),
                estimatedUniqueSourceBytes = plan.EstimatedUniqueSourceBytes,
                estimatedPartitionSourceBytes = plan.EstimatedPartitionSourceBytes,
                estimatedCrossPartitionDuplicateBytes = plan.EstimatedCrossPartitionDuplicateBytes,
                families = families,
                scopeTransfers = transfers
            };
        }

        private static long AddSaturating(long left, long right)
        {
            return left > long.MaxValue - right ? long.MaxValue : left + right;
        }

        private static long Percentile(IReadOnlyList<long> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0) return 0;
            var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
        }

        private static string FormatBytes(long bytes)
        {
            const double mebibyte = 1024d * 1024d;
            const double gibibyte = mebibyte * 1024d;
            return bytes >= gibibyte
                ? $"{bytes / gibibyte:F2} GiB"
                : $"{bytes / mebibyte:F2} MiB";
        }

        private static void Commit(
            string platformRoot,
            AddressablesBuildSession session,
            ContentArtifactManifest manifest,
            out string committedPath)
        {
            var container = manifest.buildKind == "baseline" ? "baselines" : "updates";
            committedPath = Path.Combine(platformRoot, container, manifest.buildId);
            ContentPipelineFileSystem.CreateDirectory(Path.GetDirectoryName(committedPath)!);
            if (ContentPipelineFileSystem.DirectoryExists(committedPath))
            {
                var existingManifest = Path.Combine(committedPath, ManifestFileName);
                if (!ContentPipelineFileSystem.FileExists(existingManifest) ||
                    !string.Equals(
                        ContentArtifactManifest.Load(existingManifest).buildId,
                        manifest.buildId,
                        StringComparison.Ordinal))
                {
                    throw new IOException($"Committed content directory already exists with different data: {committedPath}");
                }

                session.Discard();
            }
            else
            {
                ContentPipelineFileSystem.MoveDirectory(session.Root, committedPath);
                session.MarkCommitted();
            }

            var pointer = new ContentBuildPointer
            {
                buildId = manifest.buildId,
                manifest = ContentPipelineFileSystem.MakeRelative(
                    platformRoot,
                    Path.Combine(committedPath, ManifestFileName))
            };
            var pointerName = manifest.buildKind == "baseline"
                ? "current-baseline.json"
                : "latest-update-candidate.json";
            if (manifest.buildKind == "baseline")
            {
                var staleUpdatePointer = Path.Combine(platformRoot, "latest-update-candidate.json");
                if (ContentPipelineFileSystem.FileExists(staleUpdatePointer))
                    ContentPipelineFileSystem.DeleteFile(staleUpdatePointer);
            }
            ContentPipelineFileSystem.AtomicWrite(
                Path.Combine(platformRoot, pointerName),
                JsonUtility.ToJson(pointer, true));
        }

        private static string GetPointerManifestPath(
            string platformRoot,
            string pointerName,
            ContentPipelineBuildKind expectedKind)
        {
            var pointerPath = Path.Combine(platformRoot, pointerName);
            if (!ContentPipelineFileSystem.FileExists(pointerPath)) return string.Empty;
            var pointer = JsonUtility.FromJson<ContentBuildPointer>(
                ContentPipelineFileSystem.ReadAllText(pointerPath));
            if (pointer == null ||
                string.IsNullOrWhiteSpace(pointer.buildId) ||
                string.IsNullOrWhiteSpace(pointer.manifest))
            {
                throw new InvalidDataException($"Content build pointer is invalid: {pointerPath}");
            }
            var manifestPath = Path.GetFullPath(Path.Combine(platformRoot, pointer.manifest));
            if (!ContentPipelineFileSystem.IsWithin(manifestPath, platformRoot))
                throw new InvalidDataException($"Content build pointer escapes its platform root: {pointerPath}");
            if (!ContentPipelineFileSystem.FileExists(manifestPath))
                throw new FileNotFoundException("Content build pointer target is missing.", manifestPath);
            var manifest = ContentArtifactManifest.Load(manifestPath);
            var expectedKindValue = expectedKind.ToString().ToLowerInvariant();
            if (!string.Equals(pointer.buildId, manifest.buildId, StringComparison.Ordinal) ||
                !string.Equals(manifest.buildKind, expectedKindValue, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Content build pointer identity does not match its {expectedKindValue} manifest: {pointerPath}");
            }
            return manifestPath;
        }

        private static void ValidateBaselineCompatibility(
            ContentPipelineBuildRequest request,
            ContentArtifactManifest baseline,
            string configurationFingerprint,
            string addressablesVersion,
            string sbpVersion)
        {
            var errors = new List<string>();
            if (!string.Equals(baseline.platform, request.Target.ToString(), StringComparison.Ordinal))
                errors.Add($"platform {baseline.platform} != {request.Target}");
            if (!string.Equals(baseline.channel, request.Channel, StringComparison.Ordinal))
                errors.Add($"channel {baseline.channel} != {request.Channel}");
            if (!string.Equals(baseline.unityVersion, Application.unityVersion, StringComparison.Ordinal))
                errors.Add($"Unity {baseline.unityVersion} != {Application.unityVersion}");
            if (!string.Equals(baseline.addressablesVersion, addressablesVersion, StringComparison.Ordinal))
                errors.Add($"Addressables {baseline.addressablesVersion} != {addressablesVersion}");
            if (!string.Equals(baseline.scriptableBuildPipelineVersion, sbpVersion, StringComparison.Ordinal))
                errors.Add($"SBP {baseline.scriptableBuildPipelineVersion} != {sbpVersion}");
            if (!string.Equals(baseline.remoteLoadPath, request.RemoteLoadPath, StringComparison.Ordinal))
                errors.Add("Remote LoadPath changed");
            if (!string.Equals(
                    baseline.configurationFingerprint,
                    configurationFingerprint,
                    StringComparison.Ordinal))
            {
                var requestedPacking = ContentBundlePartitionPlanner.Normalize(request.Packing);
                errors.Add(
                    "bundle packing or backend configuration changed " +
                    $"(baseline {baseline.packing.mode}/{baseline.packing.targetBundleSizeBytes} bytes, " +
                    $"requested {requestedPacking.Mode}/{requestedPacking.TargetBundleSizeBytes} bytes)");
            }
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Content update is incompatible with its baseline; build a new baseline: " +
                    string.Join(", ", errors));
            }
        }

        private static ContentArtifactManifest LoadPreviousPackingManifest(
            ContentPipelineBuildRequest request,
            ContentArtifactManifest baseline)
        {
            var path = string.IsNullOrWhiteSpace(request.PreviousPackingManifestPath)
                ? request.BaselineManifestPath
                : request.PreviousPackingManifestPath;
            var previous = ContentArtifactManifest.Load(path);
            if (!string.Equals(previous.baselineId, baseline.baselineId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The current bundle packing plan belongs to a different baseline; build a new baseline.");
            }

            if (!string.Equals(
                    previous.configurationFingerprint,
                    baseline.configurationFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The current bundle packing plan has incompatible build configuration.");
            }

            return previous;
        }

        private static string ResolveArtifactPath(string manifestPath, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new InvalidDataException("Baseline manifest does not contain a Content State path.");
            var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath)!, relativePath));
            if (!ContentPipelineFileSystem.FileExists(path))
                throw new FileNotFoundException("Baseline Content State is missing.", path);
            return path;
        }

        private static string CreateConfigurationFingerprint(
            ContentPipelineBuildRequest request,
            string addressablesVersion,
            string sbpVersion)
        {
            var packing = ContentBundlePartitionPlanner.Normalize(request.Packing);
            return ContentPipelineHash.Sha256(string.Join(
                "\n",
                BackendIdentity,
                request.Channel,
                request.Target,
                request.PlayerVersion,
                request.RemoteLoadPath,
                request.DevelopmentBuild,
                packing.Mode,
                packing.AlgorithmVersion,
                ContentBundlePartitionPlanner.ClassifierVersion,
                packing.Mode == ContentBundlePackingMode.SizeOptimized
                    ? packing.TargetBundleSizeBytes.ToString()
                    : string.Empty,
                Application.unityVersion,
                addressablesVersion,
                sbpVersion));
        }

        private static string GetArtifactRelativePath(AddressablesBuildSession session, string sourcePath)
        {
            var fullPath = Path.GetFullPath(sourcePath);
            if (ContentPipelineFileSystem.IsWithin(fullPath, session.Root))
                return ContentPipelineFileSystem.MakeRelative(session.Root, fullPath);
            if (ContentPipelineFileSystem.IsWithin(fullPath, Addressables.BuildPath))
                return "local/" + ContentPipelineFileSystem.MakeRelative(Addressables.BuildPath, fullPath);
            return $"metadata/{ContentPipelineHash.Short(fullPath, 10)}-{Path.GetFileName(fullPath)}";
        }

        private static string ClassifyArtifact(string path)
        {
            var fileName = Path.GetFileName(path);
            if (fileName.EndsWith("addressables_content_state.bin", StringComparison.OrdinalIgnoreCase))
                return "content-state";
            if (fileName.StartsWith("catalog", StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(".hash", StringComparison.OrdinalIgnoreCase))
                return "catalog-hash";
            if (fileName.StartsWith("catalog", StringComparison.OrdinalIgnoreCase))
                return "catalog";
            if (fileName.StartsWith("settings", StringComparison.OrdinalIgnoreCase))
                return "runtime-settings";
            if (fileName.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
                return "bundle";
            return "metadata";
        }

        private static string SafeLabel(string value)
        {
            var builder = new StringBuilder();
            foreach (var character in value ?? string.Empty)
            {
                builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
            }

            var safe = builder.ToString().Trim('-');
            return string.IsNullOrEmpty(safe) ? "content" : safe;
        }

        private static string ShortPartitionLabel(string partitionId)
        {
            var segments = (partitionId ?? string.Empty)
                .Split(new[] { ':', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            var label = segments.LastOrDefault() ?? "content";
            label = SafeLabel(label);
            return label.Length <= 20 ? label : label[..20];
        }

        private static string EnsureTrailingSlash(string value)
        {
            return value.TrimEnd('/', '\\') + "/";
        }

        private static string GetPackageVersion(System.Reflection.Assembly assembly)
        {
            return UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly)?.version ?? "unknown";
        }

        private static void AddFile(ISet<string> paths, string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && ContentPipelineFileSystem.FileExists(path))
            {
                paths.Add(Path.GetFullPath(path));
            }
        }

        private static void ValidateAddressablesArtifacts(
            ContentPipelineBuildRequest request,
            AddressablesBuildSession session,
            AddressablesPlayerBuildResult result)
        {
            if (request.Graph.Assets.Any(ContentBundlePartitionPlanner.ShouldCreateExplicitEntry) &&
                result.AssetBundleBuildResults.Count == 0)
            {
                throw new InvalidDataException(
                    "Addressables returned success without any Bundle results. " +
                    "The transient Settings model was not accepted by the active Addressables version.");
            }

            var registeredFiles = result.FileRegistry?.GetFilePaths()
                .Where(path => !string.IsNullOrEmpty(path) &&
                               ContentPipelineFileSystem.FileExists(path))
                .Select(Path.GetFullPath)
                .ToArray() ?? Array.Empty<string>();
            var registeredRemoteFiles = registeredFiles
                .Where(path => ContentPipelineFileSystem.IsWithin(path, session.RemoteRoot))
                .ToArray();
            var hasRemoteCatalog = registeredRemoteFiles.Any(path =>
                Path.GetFileName(path).StartsWith("catalog_", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".hash", StringComparison.OrdinalIgnoreCase));
            var hasRemoteCatalogHash = registeredRemoteFiles.Any(path =>
                Path.GetFileName(path).StartsWith("catalog_", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".hash", StringComparison.OrdinalIgnoreCase));
            if (!hasRemoteCatalog || !hasRemoteCatalogHash)
            {
                throw new InvalidDataException("Addressables did not produce the required remote Catalog and Hash.");
            }

            if (request.BuildKind == ContentPipelineBuildKind.Baseline &&
                (string.IsNullOrEmpty(result.ContentStateFilePath) ||
                 !ContentPipelineFileSystem.FileExists(result.ContentStateFilePath)))
            {
                throw new InvalidDataException("Addressables did not produce the required baseline Content State.");
            }
        }

        private static void DestroyTransientSettings(
            AddressableAssetSettings settings,
            BuildScriptPackedMode builder)
        {
            if (settings)
            {
                foreach (var group in settings.groups.Where(group => group).ToArray())
                {
                    foreach (var schema in group.Schemas.Where(schema => schema).ToArray())
                    {
                        Object.DestroyImmediate(schema);
                    }

                    Object.DestroyImmediate(group);
                }

                Object.DestroyImmediate(settings);
            }

            if (builder) Object.DestroyImmediate(builder);
        }

        [Serializable]
        private sealed class ContentBuildPointer
        {
            public string buildId;
            public string manifest;
        }

        private sealed class RequestBuildSettingsProvider : IBuildSettingsProvider
        {
            private readonly ContentPipelineBuildRequest _request;

            public RequestBuildSettingsProvider(ContentPipelineBuildRequest request)
            {
                _request = request;
            }

            public BuildTarget activeBuildTarget => _request.Target;

            public bool development => _request.DevelopmentBuild;

            public string[] extraScriptingDefines => Array.Empty<string>();
        }

        private sealed class PartitionRuntime
        {
            public PartitionRuntime(
                AddressableAssetGroup group,
                string partitionId,
                ContentLocation location)
            {
                Group = group;
                PartitionId = partitionId;
                Location = location;
            }

            public AddressableAssetGroup Group { get; }

            public string PartitionId { get; }

            public ContentLocation Location { get; }

            public HashSet<string> ScopeIds { get; } = new(StringComparer.Ordinal);
        }

        private sealed class BundlePartitionMetadata
        {
            public static readonly BundlePartitionMetadata Empty = new(
                string.Empty,
                Array.Empty<string>());

            public BundlePartitionMetadata(string partitionId, string[] scopeIds)
            {
                PartitionId = partitionId ?? string.Empty;
                ScopeIds = scopeIds ?? Array.Empty<string>();
            }

            public string PartitionId { get; }

            public string[] ScopeIds { get; }
        }
    }

    internal static class AddressablesCompatibility
    {
        private static readonly FieldInfo DefaultSettingsField =
            typeof(AddressableAssetSettingsDefaultObject).GetField(
                "s_DefaultSettingsObject",
                BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly MethodInfo InitializeGroupMethod =
            typeof(AddressableAssetGroup).GetMethod(
                "Initialize",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(AddressableAssetSettings),
                    typeof(string),
                    typeof(string),
                    typeof(bool)
                },
                null);

        private static readonly FieldInfo DefaultGroupField =
            typeof(AddressableAssetSettings).GetField(
                "m_DefaultGroup",
                BindingFlags.Instance | BindingFlags.NonPublic);

        public static void ValidateApi()
        {
            if (DefaultSettingsField == null ||
                DefaultSettingsField.FieldType != typeof(AddressableAssetSettings))
            {
                throw new MissingFieldException(
                    typeof(AddressableAssetSettingsDefaultObject).FullName,
                    "s_DefaultSettingsObject");
            }
            if (InitializeGroupMethod == null ||
                InitializeGroupMethod.ReturnType != typeof(void))
            {
                throw new MissingMethodException(
                    typeof(AddressableAssetGroup).FullName,
                    "Initialize(AddressableAssetSettings,string,string,bool)");
            }
            if (DefaultGroupField == null ||
                DefaultGroupField.FieldType != typeof(string))
            {
                throw new MissingFieldException(
                    typeof(AddressableAssetSettings).FullName,
                    "m_DefaultGroup");
            }
        }

        public static AddressableAssetGroup CreateStableGroup(
            AddressableAssetSettings settings,
            string groupName,
            string guid)
        {
            if (InitializeGroupMethod == null)
            {
                throw new MissingMemberException(
                    typeof(AddressableAssetGroup).FullName,
                    "Initialize");
            }

            var group = ScriptableObject.CreateInstance<AddressableAssetGroup>();
            group.hideFlags = HideFlags.HideAndDontSave;
            InitializeGroupMethod.Invoke(
                group,
                new object[] { settings, groupName, guid, false });
            if (!string.Equals(group.Guid, guid, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Addressables rejected the deterministic transient Group GUID.");
            }

            settings.groups.Add(group);
            group.AddSchema(typeof(ContentUpdateGroupSchema), false);
            group.AddSchema(typeof(BundledAssetGroupSchema), false);
            foreach (var schema in group.Schemas)
            {
                schema.hideFlags = HideFlags.HideAndDontSave;
            }
            if (!ReferenceEquals(group.Settings, settings))
                throw new InvalidOperationException("Transient Addressables Group lost its Settings owner.");
            return group;
        }

        public static void SetDefaultGroup(
            AddressableAssetSettings settings,
            AddressableAssetGroup group)
        {
            if (DefaultGroupField == null)
                throw new MissingFieldException(typeof(AddressableAssetSettings).FullName, "m_DefaultGroup");
            DefaultGroupField.SetValue(settings, group.Guid);
        }

        public static IDisposable OverrideDefaultSettings(AddressableAssetSettings settings)
        {
            if (DefaultSettingsField == null)
                throw new MissingFieldException(
                    typeof(AddressableAssetSettingsDefaultObject).FullName,
                    "s_DefaultSettingsObject");
            var previous = DefaultSettingsField.GetValue(null);
            DefaultSettingsField.SetValue(null, settings);
            if (!ReferenceEquals(AddressableAssetSettingsDefaultObject.Settings, settings))
                throw new InvalidOperationException("Could not activate the transient Addressables Settings in memory.");
            return new CallbackScope(() => DefaultSettingsField.SetValue(null, previous));
        }

        public static void ValidateTransientSettings(AddressableAssetSettings settings)
        {
            if (!ReferenceEquals(AddressableAssetSettingsDefaultObject.Settings, settings))
                throw new InvalidOperationException("Transient Addressables Settings is not the active in-memory default.");
            if (settings.groups.Count == 0)
                throw new InvalidOperationException("Transient Addressables Settings has no groups.");
            if (settings.groups.Any(group => !group || !ReferenceEquals(group.Settings, settings)))
                throw new InvalidOperationException("A transient Addressables Group has an invalid Settings owner.");
            var defaultGroup = settings.DefaultGroup;
            if (!defaultGroup || !settings.groups.Contains(defaultGroup))
                throw new InvalidOperationException("Transient Addressables Settings has no valid default Group.");
            if (string.IsNullOrEmpty(settings.RemoteCatalogBuildPath.GetValue(settings)) ||
                string.IsNullOrEmpty(settings.RemoteCatalogLoadPath.GetValue(settings)))
                throw new InvalidOperationException("Transient Addressables remote Catalog paths are invalid.");
        }

        public static IDisposable DisableBuildLayout()
        {
            var previous = ProjectConfigData.GenerateBuildLayout;
            ProjectConfigData.GenerateBuildLayout = false;
            return new CallbackScope(() => ProjectConfigData.GenerateBuildLayout = previous);
        }

        private sealed class CallbackScope : IDisposable
        {
            private Action _callback;

            public CallbackScope(Action callback)
            {
                _callback = callback;
            }

            public void Dispose()
            {
                var callback = _callback;
                _callback = null;
                callback?.Invoke();
            }
        }
    }

    internal sealed class AddressablesBuildSession : IDisposable
    {
        private readonly string _addressablesBuildPath;
        private readonly string _backupPath;
        private bool _committed;
        private bool _disposed;

        public AddressablesBuildSession(string root)
        {
            Root = Path.GetFullPath(root);
            RemoteRoot = Path.Combine(Root, "remote");
            StateRoot = Path.Combine(Root, "state");
            ContentPipelineFileSystem.CreateDirectory(RemoteRoot);
            ContentPipelineFileSystem.CreateDirectory(StateRoot);

            _addressablesBuildPath = Path.GetFullPath(Addressables.BuildPath);
            _backupPath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath)!,
                "Library",
                "CeresContentPipeline",
                "backups",
                Guid.NewGuid().ToString("N"));
            if (ContentPipelineFileSystem.DirectoryExists(_addressablesBuildPath))
            {
                ContentPipelineFileSystem.CreateDirectory(Path.GetDirectoryName(_backupPath)!);
                ContentPipelineFileSystem.MoveDirectory(_addressablesBuildPath, _backupPath);
            }
        }

        public string Root { get; }

        public string RemoteRoot { get; }

        public string StateRoot { get; }

        public void MarkCommitted()
        {
            _committed = true;
        }

        public void Discard()
        {
            if (ContentPipelineFileSystem.DirectoryExists(Root))
                ContentPipelineFileSystem.DeleteDirectory(Root, true);
            _committed = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Exception cleanupException = null;
            try
            {
                if (ContentPipelineFileSystem.DirectoryExists(_addressablesBuildPath))
                {
                    ContentPipelineFileSystem.DeleteDirectory(_addressablesBuildPath, true);
                }

                if (ContentPipelineFileSystem.DirectoryExists(_backupPath))
                {
                    ContentPipelineFileSystem.CreateDirectory(
                        Path.GetDirectoryName(_addressablesBuildPath)!);
                    ContentPipelineFileSystem.MoveDirectory(_backupPath, _addressablesBuildPath);
                }
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }

            try
            {
                if (!_committed && ContentPipelineFileSystem.DirectoryExists(Root))
                {
                    ContentPipelineFileSystem.DeleteDirectory(Root, true);
                }
            }
            catch (Exception exception)
            {
                cleanupException ??= exception;
            }

            if (cleanupException != null)
            {
                Debug.LogException(new IOException(
                    "Ceres content build cleanup failed. The original build result was preserved.",
                    cleanupException));
            }
        }
    }

    public sealed class ContentBuildProcessLock : IDisposable
    {
        private readonly FileStream _stream;

        private ContentBuildProcessLock(FileStream stream, string root)
        {
            _stream = stream;
            Root = root;
        }

        public string Root { get; }

        public static ContentBuildProcessLock Acquire(string platformRoot)
        {
            var root = ContentPipelineFileSystem.Normalize(platformRoot);
            ContentPipelineFileSystem.CreateDirectory(root);
            var path = Path.Combine(root, ".build.lock");
            try
            {
                return new ContentBuildProcessLock(ContentPipelineFileSystem.OpenFile(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None), root);
            }
            catch (IOException exception)
            {
                throw new InvalidOperationException(
                    $"Another content build owns '{platformRoot}'. Wait for it to finish.",
                    exception);
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }

    internal sealed class ContentBuildSnapshot
    {
        public ContentScopeSnapshot[] Scopes { get; private set; }

        public ContentAssetSnapshot[] Assets { get; private set; }

        public static ContentBuildSnapshot Create(ContentBuildGraph graph)
        {
            var assets = graph.Assets.Select(node =>
            {
                var dependencyHash = !string.IsNullOrEmpty(node.AssetPath) &&
                                     ContentPipelineFileSystem.FileExists(node.AssetPath)
                    ? AssetDatabase.GetAssetDependencyHash(node.AssetPath).ToString()
                    : "missing";
                var fingerprint = ContentPipelineHash.Sha256(string.Join(
                    "\n",
                    node.AssetId,
                    node.AssetPath,
                    node.Address,
                    string.Join("|", node.Labels),
                    node.TypeName,
                    node.IsExplicit,
                    string.Join("|", node.ExplicitScopeIds),
                    string.Join("|", node.UsageScopeIds),
                    node.Location,
                    node.Ownership,
                    node.OwnerScopeId,
                    node.PartitionId,
                    string.Join("|", node.PackingHints),
                    dependencyHash));
                return new ContentAssetSnapshot
                {
                    id = node.AssetId,
                    fingerprint = fingerprint,
                    ownership = node.Ownership.ToString(),
                    location = node.Location.ToString(),
                    ownerScopeId = node.OwnerScopeId,
                    usageScopeIds = node.UsageScopeIds.ToArray()
                };
            }).OrderBy(asset => asset.id, StringComparer.Ordinal).ToArray();
            var scopes = graph.Scopes.Select(scope =>
            {
                var relevantAssets = assets
                    .Where(asset => asset.usageScopeIds.Contains(scope.Id, StringComparer.Ordinal))
                    .Select(asset => $"{asset.id}:{asset.fingerprint}");
                return new ContentScopeSnapshot
                {
                    id = scope.Id,
                    version = scope.Version,
                    fingerprint = ContentPipelineHash.Sha256(string.Join(
                        "\n",
                        scope.Id,
                        scope.DisplayName,
                        scope.Version,
                        scope.Enabled,
                        scope.DefaultLocation,
                        string.Join("|", scope.Properties.Select(pair => $"{pair.Key}={pair.Value}")),
                        string.Join("|", relevantAssets)))
                };
            }).OrderBy(scope => scope.id, StringComparer.Ordinal).ToArray();
            return new ContentBuildSnapshot { Scopes = scopes, Assets = assets };
        }
    }

    internal static class ContentBuildChangeValidator
    {
        public static string[] Validate(
            ContentArtifactManifest baseline,
            ContentBuildSnapshot current,
            IReadOnlyCollection<string> allowedScopeIds)
        {
            var allowed = new HashSet<string>(allowedScopeIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (allowed.Count == 0)
                throw new InvalidOperationException("Select at least one allowed Collection scope for an update.");

            var oldAssets = baseline.assets.ToDictionary(asset => asset.id, StringComparer.Ordinal);
            var newAssets = current.Assets.ToDictionary(asset => asset.id, StringComparer.Ordinal);
            var impacted = new HashSet<string>(allowed, StringComparer.Ordinal);
            var violations = new List<string>();
            foreach (var assetId in oldAssets.Keys.Union(newAssets.Keys, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
            {
                oldAssets.TryGetValue(assetId, out var oldAsset);
                newAssets.TryGetValue(assetId, out var newAsset);
                if (oldAsset != null && newAsset != null &&
                    string.Equals(oldAsset.fingerprint, newAsset.fingerprint, StringComparison.Ordinal))
                    continue;

                var asset = newAsset ?? oldAsset;
                var scopes = (oldAsset?.usageScopeIds ?? Array.Empty<string>())
                    .Union(newAsset?.usageScopeIds ?? Array.Empty<string>(), StringComparer.Ordinal)
                    .ToArray();
                if (string.Equals(asset.location, ContentLocation.Local.ToString(), StringComparison.Ordinal))
                {
                    violations.Add($"{assetId} changes Local Player content and requires a new baseline");
                    continue;
                }

                var isShared = string.Equals(asset.ownership, ContentOwnership.Shared.ToString(), StringComparison.Ordinal) ||
                               scopes.Length > 1;
                if (isShared && scopes.Any(allowed.Contains))
                {
                    impacted.UnionWith(scopes);
                    continue;
                }

                var owner = asset.ownerScopeId;
                if (string.IsNullOrEmpty(owner) && scopes.Length == 1) owner = scopes[0];
                if (string.IsNullOrEmpty(owner) || !allowed.Contains(owner))
                {
                    violations.Add($"{assetId} (owner: {owner}, scopes: {string.Join(", ", scopes)})");
                }
            }

            var oldScopes = baseline.scopes.ToDictionary(scope => scope.id, StringComparer.Ordinal);
            var newScopes = current.Scopes.ToDictionary(scope => scope.id, StringComparer.Ordinal);
            foreach (var scopeId in oldScopes.Keys.Union(newScopes.Keys, StringComparer.Ordinal))
            {
                oldScopes.TryGetValue(scopeId, out var oldScope);
                newScopes.TryGetValue(scopeId, out var newScope);
                if (oldScope != null && newScope != null &&
                    string.Equals(oldScope.fingerprint, newScope.fingerprint, StringComparison.Ordinal))
                    continue;
                if (!allowed.Contains(scopeId) && !impacted.Contains(scopeId))
                {
                    violations.Add($"scope metadata changed outside the allowed set: {scopeId}");
                }
            }

            if (violations.Count > 0)
            {
                throw new InvalidOperationException(
                    "Update contains changes outside the allowed Collection scope:" +
                    Environment.NewLine + string.Join(Environment.NewLine, violations.Distinct()));
            }

            return impacted.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }
}
