using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Ceres.ContentPipeline
{
    [Serializable]
    public sealed class ContentArtifactRecord
    {
        public string relativePath;
        public string kind;
        public long size;
        public string sha256;
        public string partitionId;
        public string[] sourceScopes = Array.Empty<string>();
    }

    public enum ContentBundlePackingMode
    {
        LogicalPartitions,
        SizeOptimized
    }

    public sealed class ContentBundlePackingOptions
    {
        public const long DefaultTargetBundleSizeBytes = 128L * 1024L * 1024L;
        public const string LogicalAlgorithmVersion = "logical-v1";
        public const string DefaultAlgorithmVersion = "size-aware-v1";

        public ContentBundlePackingMode Mode { get; set; } = ContentBundlePackingMode.LogicalPartitions;

        public long TargetBundleSizeBytes { get; set; } = DefaultTargetBundleSizeBytes;

        public string AlgorithmVersion { get; set; } = DefaultAlgorithmVersion;
    }

    [Serializable]
    public sealed class ContentBundlePackingSnapshot
    {
        public string mode;
        public long targetBundleSizeBytes;
        public string algorithmVersion;
        public string classifierVersion;
    }

    [Serializable]
    public sealed class ContentBundlePartitionSnapshot
    {
        public string id;
        public string family;
        public string location;
        public bool containsScenes;
        public long estimatedBytes;
        public long actualBytes;
        public string[] assetIds = Array.Empty<string>();
        public string[] sourceScopes = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ContentBundleFamilyStatistics
    {
        public string family;
        public int partitionCount;
        public int bundleCount;
        public long bundleBytes;
        public long estimatedUniqueSourceBytes;
        public long estimatedPartitionSourceBytes;
        public long estimatedCrossPartitionDuplicateBytes;
    }

    [Serializable]
    public sealed class ContentScopeTransferStatistics
    {
        public string scopeId;
        public int changedBundleCount;
        public long changedBundleBytes;
    }

    [Serializable]
    public sealed class ContentBundleBuildStatistics
    {
        public int plannedPartitionCount;
        public int bundleCount;
        public long bundleBytes;
        public long p50BundleBytes;
        public long p90BundleBytes;
        public long p95BundleBytes;
        public int bundlesUnder4KiB;
        public int bundlesUnder64KiB;
        public int managedBundleCount;
        public int managedBundlesUnder64KiB;
        public int oversizedBundleCount;
        public int listInfoBundleCount;
        public long estimatedUniqueSourceBytes;
        public long estimatedPartitionSourceBytes;
        public long estimatedCrossPartitionDuplicateBytes;
        public ContentBundleFamilyStatistics[] families = Array.Empty<ContentBundleFamilyStatistics>();
        public ContentScopeTransferStatistics[] scopeTransfers = Array.Empty<ContentScopeTransferStatistics>();
    }

    [Serializable]
    public sealed class ContentScopeSnapshot
    {
        public string id;
        public string version;
        public string fingerprint;
    }

    [Serializable]
    public sealed class ContentAssetSnapshot
    {
        public string id;
        public string fingerprint;
        public string ownership;
        public string location;
        public string ownerScopeId;
        public string[] usageScopeIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ContentArtifactManifest
    {
        public const int CurrentSchemaVersion = 1;
        public const string BuildIdentity = "artifact-manifest-v1";

        public int schemaVersion = CurrentSchemaVersion;
        public string buildKind;
        public string buildId;
        public string baselineId;
        public string channel;
        public string platform;
        public string playerVersion;
        public string graphFingerprint;
        public string configurationFingerprint;
        public string unityVersion;
        public string addressablesVersion;
        public string scriptableBuildPipelineVersion;
        public string remoteLoadPath;
        public string contentStateRelativePath;
        public ContentScopeSnapshot[] scopes = Array.Empty<ContentScopeSnapshot>();
        public ContentAssetSnapshot[] assets = Array.Empty<ContentAssetSnapshot>();
        public ContentArtifactRecord[] artifacts = Array.Empty<ContentArtifactRecord>();
        public string[] impactedScopeIds = Array.Empty<string>();
        public ContentBundlePackingSnapshot packing;
        public ContentBundlePartitionSnapshot[] partitions = Array.Empty<ContentBundlePartitionSnapshot>();
        public ContentBundleBuildStatistics bundleStatistics;

        public static ContentArtifactManifest Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !ContentPipelineFileSystem.FileExists(path))
            {
                throw new FileNotFoundException("Content artifact manifest was not found.", path);
            }

            var json = ContentPipelineFileSystem.ReadAllText(path);
            ValidateSerializedContract(json, path);
            var manifest = JsonUtility.FromJson<ContentArtifactManifest>(json);
            if (manifest == null || manifest.schemaVersion != CurrentSchemaVersion)
            {
                throw new InvalidDataException($"Unsupported content artifact manifest: {path}");
            }
            manifest.Validate(path);
            return manifest;
        }

        public void Save(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Manifest path is empty.", nameof(path));
            Validate(path);
            ContentPipelineFileSystem.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            ContentPipelineFileSystem.WriteAllText(
                path,
                JsonUtility.ToJson(this, true),
                new UTF8Encoding(false));
        }

        private void Validate(string path)
        {
            static void Require(string value, string field, string manifestPath)
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new InvalidDataException(
                        $"Content artifact manifest field '{field}' is empty: {manifestPath}");
            }

            static void RequireArray(Array value, string field, string manifestPath)
            {
                if (value == null)
                    throw new InvalidDataException(
                        $"Content artifact manifest array '{field}' is missing: {manifestPath}");
            }

            if (schemaVersion != CurrentSchemaVersion)
                throw new InvalidDataException(
                    $"Unsupported content artifact manifest schema '{schemaVersion}': {path}");
            Require(buildKind, nameof(buildKind), path);
            if (buildKind is not ("baseline" or "update"))
                throw new InvalidDataException($"Invalid content build kind '{buildKind}': {path}");
            Require(buildId, nameof(buildId), path);
            Require(baselineId, nameof(baselineId), path);
            if (buildKind == "baseline" &&
                !string.Equals(buildId, baselineId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Baseline Build ID and Baseline ID do not match: {path}");
            }
            Require(channel, nameof(channel), path);
            Require(platform, nameof(platform), path);
            Require(playerVersion, nameof(playerVersion), path);
            Require(graphFingerprint, nameof(graphFingerprint), path);
            Require(configurationFingerprint, nameof(configurationFingerprint), path);
            if (!IsSha256(buildId) ||
                !IsSha256(baselineId) ||
                !IsSha256(graphFingerprint) ||
                !IsSha256(configurationFingerprint))
            {
                throw new InvalidDataException(
                    $"Content artifact manifest identity hash is invalid: {path}");
            }
            Require(unityVersion, nameof(unityVersion), path);
            Require(addressablesVersion, nameof(addressablesVersion), path);
            Require(scriptableBuildPipelineVersion, nameof(scriptableBuildPipelineVersion), path);
            Require(remoteLoadPath, nameof(remoteLoadPath), path);
            Require(contentStateRelativePath, nameof(contentStateRelativePath), path);
            RequireArray(scopes, nameof(scopes), path);
            RequireArray(assets, nameof(assets), path);
            RequireArray(artifacts, nameof(artifacts), path);
            RequireArray(impactedScopeIds, nameof(impactedScopeIds), path);
            RequireArray(partitions, nameof(partitions), path);

            ValidatePacking(path);
            ValidateScopes(path);
            ValidateAssets(path);
            ValidateArtifacts(path);
            ValidatePartitions(path);
            ValidateStatistics(path);
        }

        private void ValidatePacking(string path)
        {
            if (packing == null)
                throw new InvalidDataException($"Content artifact manifest packing is missing: {path}");
            if (!TryParseDefinedEnum(
                    packing.mode,
                    out ContentBundlePackingMode mode))
            {
                throw new InvalidDataException(
                    $"Unknown content bundle packing mode '{packing.mode}': {path}");
            }
            var expectedAlgorithm = mode == ContentBundlePackingMode.LogicalPartitions
                ? ContentBundlePackingOptions.LogicalAlgorithmVersion
                : ContentBundlePackingOptions.DefaultAlgorithmVersion;
            if (!string.Equals(
                    packing.algorithmVersion,
                    expectedAlgorithm,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Unsupported content bundle packing algorithm '{packing.algorithmVersion}': {path}");
            }
            if (!string.Equals(
                    packing.classifierVersion,
                    ContentBundlePartitionPlanner.ClassifierVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Unsupported content bundle classifier '{packing.classifierVersion}': {path}");
            }
            if (packing.targetBundleSizeBytes <= 0)
            {
                throw new InvalidDataException(
                    $"Content bundle target size must be positive: {path}");
            }
            if (mode == ContentBundlePackingMode.SizeOptimized &&
                (packing.targetBundleSizeBytes < 32L * 1024L * 1024L ||
                 packing.targetBundleSizeBytes > 512L * 1024L * 1024L))
            {
                throw new InvalidDataException(
                    $"Size-optimized bundle target is outside 32-512 MiB: {path}");
            }
        }

        private void ValidateScopes(string path)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scope in scopes)
            {
                if (scope == null ||
                    string.IsNullOrWhiteSpace(scope.id) ||
                    !ids.Add(scope.id) ||
                    string.IsNullOrWhiteSpace(scope.version) ||
                    !IsSha256(scope.fingerprint))
                {
                    throw new InvalidDataException(
                        $"Content artifact manifest contains an invalid or duplicate scope: {path}");
                }
            }
        }

        private void ValidateAssets(string path)
        {
            var scopeIds = new HashSet<string>(
                scopes.Select(scope => scope.id),
                StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asset in assets)
            {
                if (asset == null ||
                    string.IsNullOrWhiteSpace(asset.id) ||
                    !ids.Add(asset.id) ||
                    !IsSha256(asset.fingerprint) ||
                    !TryParseDefinedEnum(asset.ownership, out ContentOwnership _) ||
                    !TryParseDefinedEnum(asset.location, out ContentLocation _) ||
                    asset.usageScopeIds == null ||
                    !string.IsNullOrEmpty(asset.ownerScopeId) &&
                    !scopeIds.Contains(asset.ownerScopeId) ||
                    asset.usageScopeIds.Any(scopeId =>
                        string.IsNullOrWhiteSpace(scopeId) ||
                        !scopeIds.Contains(scopeId)) ||
                    asset.usageScopeIds.Distinct(StringComparer.Ordinal).Count() !=
                    asset.usageScopeIds.Length)
                {
                    throw new InvalidDataException(
                        $"Content artifact manifest contains an invalid or duplicate asset: {path}");
                }
            }
        }

        private void ValidateArtifacts(string path)
        {
            var scopeIds = new HashSet<string>(
                scopes.Select(scope => scope.id),
                StringComparer.Ordinal);
            var relativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var artifact in artifacts)
            {
                if (artifact == null ||
                    string.IsNullOrWhiteSpace(artifact.relativePath) ||
                    !IsKnownArtifactKind(artifact.kind) ||
                    artifact.size < 0 ||
                    artifact.sourceScopes == null ||
                    artifact.sourceScopes.Any(scopeId =>
                        string.IsNullOrWhiteSpace(scopeId) ||
                        !scopeIds.Contains(scopeId)) ||
                    artifact.sourceScopes.Distinct(StringComparer.Ordinal).Count() !=
                    artifact.sourceScopes.Length ||
                    string.Equals(artifact.kind, "bundle", StringComparison.Ordinal) &&
                    string.IsNullOrWhiteSpace(artifact.partitionId) ||
                    !IsSafeRelativePath(artifact.relativePath) ||
                    !relativePaths.Add(artifact.relativePath.Replace('\\', '/')) ||
                    !IsSha256(artifact.sha256))
                {
                    throw new InvalidDataException(
                        $"Content artifact manifest contains an invalid or duplicate artifact: {path}");
                }
            }
        }

        private void ValidatePartitions(string path)
        {
            var knownAssets = new HashSet<string>(
                assets.Select(asset => asset.id),
                StringComparer.Ordinal);
            var knownScopes = new HashSet<string>(
                scopes.Select(scope => scope.id),
                StringComparer.Ordinal);
            var partitionIds = new HashSet<string>(StringComparer.Ordinal);
            var assetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var partition in partitions)
            {
                if (partition == null ||
                    string.IsNullOrWhiteSpace(partition.id) ||
                    !partitionIds.Add(partition.id) ||
                    string.IsNullOrWhiteSpace(partition.family) ||
                    !TryParseDefinedEnum(partition.location, out ContentLocation _) ||
                    partition.estimatedBytes < 0 ||
                    partition.actualBytes < 0 ||
                    partition.assetIds == null ||
                    partition.sourceScopes == null ||
                    partition.sourceScopes.Any(scopeId =>
                        string.IsNullOrWhiteSpace(scopeId) ||
                        !knownScopes.Contains(scopeId)) ||
                    partition.sourceScopes.Distinct(StringComparer.Ordinal).Count() !=
                    partition.sourceScopes.Length)
                {
                    throw new InvalidDataException(
                        $"Content artifact manifest contains an invalid or duplicate partition: {path}");
                }

                foreach (var assetId in partition.assetIds)
                {
                    if (string.IsNullOrWhiteSpace(assetId) ||
                        !knownAssets.Contains(assetId) ||
                        !assetIds.Add(assetId))
                    {
                        throw new InvalidDataException(
                            $"Content artifact manifest contains an empty or multiply assigned partition asset: {path}");
                    }
                }
            }
        }

        private void ValidateStatistics(string path)
        {
            if (bundleStatistics == null ||
                bundleStatistics.families == null ||
                bundleStatistics.scopeTransfers == null ||
                bundleStatistics.plannedPartitionCount < 0 ||
                bundleStatistics.bundleCount < 0 ||
                bundleStatistics.bundleBytes < 0 ||
                bundleStatistics.p50BundleBytes < 0 ||
                bundleStatistics.p90BundleBytes < 0 ||
                bundleStatistics.p95BundleBytes < 0 ||
                bundleStatistics.bundlesUnder4KiB < 0 ||
                bundleStatistics.bundlesUnder64KiB < 0 ||
                bundleStatistics.managedBundleCount < 0 ||
                bundleStatistics.managedBundlesUnder64KiB < 0 ||
                bundleStatistics.oversizedBundleCount < 0 ||
                bundleStatistics.listInfoBundleCount < 0 ||
                bundleStatistics.estimatedUniqueSourceBytes < 0 ||
                bundleStatistics.estimatedPartitionSourceBytes < 0 ||
                bundleStatistics.estimatedCrossPartitionDuplicateBytes < 0)
            {
                throw new InvalidDataException(
                    $"Content artifact manifest bundle statistics are invalid: {path}");
            }
            if (bundleStatistics.plannedPartitionCount != partitions.Length)
            {
                throw new InvalidDataException(
                    $"Content artifact manifest partition count does not match its statistics: {path}");
            }
            if (bundleStatistics.bundlesUnder4KiB > bundleStatistics.bundleCount ||
                bundleStatistics.bundlesUnder64KiB > bundleStatistics.bundleCount ||
                bundleStatistics.managedBundleCount > bundleStatistics.bundleCount ||
                bundleStatistics.managedBundlesUnder64KiB > bundleStatistics.managedBundleCount ||
                bundleStatistics.oversizedBundleCount > bundleStatistics.plannedPartitionCount ||
                bundleStatistics.listInfoBundleCount > bundleStatistics.plannedPartitionCount ||
                bundleStatistics.estimatedPartitionSourceBytes <
                bundleStatistics.estimatedUniqueSourceBytes ||
                bundleStatistics.estimatedCrossPartitionDuplicateBytes !=
                bundleStatistics.estimatedPartitionSourceBytes -
                bundleStatistics.estimatedUniqueSourceBytes)
            {
                throw new InvalidDataException(
                    $"Content artifact manifest bundle statistics are inconsistent: {path}");
            }
            var familyNames = new HashSet<string>(StringComparer.Ordinal);
            var familyPartitionCount = 0;
            var familyBundleCount = 0;
            foreach (var family in bundleStatistics.families)
            {
                if (family == null ||
                    string.IsNullOrWhiteSpace(family.family) ||
                    !familyNames.Add(family.family) ||
                    family.partitionCount < 0 ||
                    family.bundleCount < 0 ||
                    family.bundleBytes < 0 ||
                    family.estimatedUniqueSourceBytes < 0 ||
                    family.estimatedPartitionSourceBytes < 0 ||
                    family.estimatedCrossPartitionDuplicateBytes < 0 ||
                    family.estimatedPartitionSourceBytes <
                    family.estimatedUniqueSourceBytes ||
                    family.estimatedCrossPartitionDuplicateBytes !=
                    family.estimatedPartitionSourceBytes -
                    family.estimatedUniqueSourceBytes)
                {
                    throw new InvalidDataException(
                        $"Content artifact manifest family statistics are invalid: {path}");
                }
                familyPartitionCount += family.partitionCount;
                familyBundleCount += family.bundleCount;
            }
            if (familyPartitionCount != bundleStatistics.plannedPartitionCount ||
                familyBundleCount != bundleStatistics.managedBundleCount)
            {
                throw new InvalidDataException(
                    $"Content artifact manifest family statistics do not match managed totals: {path}");
            }
            var transferScopes = new HashSet<string>(StringComparer.Ordinal);
            var knownScopes = new HashSet<string>(
                scopes.Select(scope => scope.id),
                StringComparer.Ordinal);
            foreach (var transfer in bundleStatistics.scopeTransfers)
            {
                if (transfer == null ||
                    string.IsNullOrWhiteSpace(transfer.scopeId) ||
                    !knownScopes.Contains(transfer.scopeId) ||
                    !transferScopes.Add(transfer.scopeId) ||
                    transfer.changedBundleCount < 0 ||
                    transfer.changedBundleBytes < 0)
                {
                    throw new InvalidDataException(
                    $"Content artifact manifest scope transfer statistics are invalid: {path}");
                }
            }
            ValidateScopeList(
                impactedScopeIds,
                knownScopes,
                nameof(impactedScopeIds),
                path);
        }

        private static void ValidateScopeList(
            IReadOnlyCollection<string> values,
            ISet<string> knownScopes,
            string field,
            string path)
        {
            if (values.Any(value =>
                    string.IsNullOrWhiteSpace(value) ||
                    !knownScopes.Contains(value)) ||
                values.Distinct(StringComparer.Ordinal).Count() != values.Count)
            {
                throw new InvalidDataException(
                    $"Content artifact manifest scope list '{field}' is invalid: {path}");
            }
        }

        private static bool IsSafeRelativePath(string value)
        {
            if (Path.IsPathRooted(value)) return false;
            var segments = value.Replace('\\', '/')
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 &&
                   segments.All(segment => segment is not "." and not "..");
        }

        private static bool IsSha256(string value)
        {
            return value is { Length: 64 } &&
                   value.All(character =>
                       character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
        }

        private static bool IsKnownArtifactKind(string value)
        {
            return value is
                "bundle" or
                "catalog" or
                "catalog-hash" or
                "content-state" or
                "runtime-settings" or
                "metadata";
        }

        private static bool TryParseDefinedEnum<T>(string value, out T result)
            where T : struct, Enum
        {
            return Enum.TryParse(value, false, out result) &&
                   Enum.IsDefined(typeof(T), result);
        }

        private static void ValidateSerializedContract(string json, string path)
        {
            var requiredFields = new[]
            {
                nameof(schemaVersion),
                nameof(buildKind),
                nameof(buildId),
                nameof(baselineId),
                nameof(channel),
                nameof(platform),
                nameof(playerVersion),
                nameof(graphFingerprint),
                nameof(configurationFingerprint),
                nameof(unityVersion),
                nameof(addressablesVersion),
                nameof(scriptableBuildPipelineVersion),
                nameof(remoteLoadPath),
                nameof(contentStateRelativePath),
                nameof(scopes),
                nameof(assets),
                nameof(artifacts),
                nameof(impactedScopeIds),
                nameof(packing),
                nameof(partitions),
                nameof(bundleStatistics),
                nameof(ContentBundleBuildStatistics.managedBundleCount),
                nameof(ContentBundleBuildStatistics.managedBundlesUnder64KiB),
                nameof(ContentBundleBuildStatistics.estimatedUniqueSourceBytes),
                nameof(ContentBundleBuildStatistics.estimatedPartitionSourceBytes),
                nameof(ContentBundleBuildStatistics.estimatedCrossPartitionDuplicateBytes),
                nameof(ContentBundleBuildStatistics.families),
                nameof(ContentBundleBuildStatistics.scopeTransfers)
            };
            foreach (var field in requiredFields)
            {
                if (Regex.IsMatch(
                        json,
                        $"\"{Regex.Escape(field)}\"\\s*:",
                        RegexOptions.CultureInvariant))
                {
                    continue;
                }

                throw new InvalidDataException(
                    $"Content artifact manifest field '{field}' is missing: {path}");
            }
        }
    }

    public enum ContentPipelineBuildKind
    {
        Baseline,
        Update
    }

    public sealed class ContentPipelineBuildRequest
    {
        public ContentBuildGraph Graph { get; set; }

        public string OutputRoot { get; set; }

        public string Channel { get; set; } = "development";

        public string PlayerVersion { get; set; } = "1.0.0";

        public string RemoteLoadPath { get; set; }

        public UnityEditor.BuildTarget Target { get; set; } = UnityEditor.EditorUserBuildSettings.activeBuildTarget;

        public bool DevelopmentBuild { get; set; }

        public ContentPipelineBuildKind BuildKind { get; set; }

        public string BaselineManifestPath { get; set; }

        public string PreviousManifestPath { get; set; }

        public ContentBundlePackingOptions Packing { get; set; } = new();
    }

    public sealed class ContentPipelineBuildResult
    {
        public bool Succeeded => Exception == null && !string.IsNullOrEmpty(OutputPath);

        public bool UpToDate { get; internal set; }

        public string OutputPath { get; internal set; }

        public string ManifestPath { get; internal set; }

        public ContentArtifactManifest Manifest { get; internal set; }

        public Exception Exception { get; internal set; }
    }

    internal static class ContentPipelineHash
    {
        public static string Sha256(string value)
        {
            return Sha256(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        public static string Sha256(byte[] value)
        {
            using var algorithm = SHA256.Create();
            return ToHex(algorithm.ComputeHash(value ?? Array.Empty<byte>()));
        }

        public static string Sha256File(string path)
        {
            using var stream = ContentPipelineFileSystem.OpenRead(path);
            using var algorithm = SHA256.Create();
            return ToHex(algorithm.ComputeHash(stream));
        }

        public static string Short(string value, int length = 12)
        {
            var hash = Sha256(value);
            return hash.Substring(0, Math.Min(length, hash.Length));
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Performs content-pipeline-owned file operations while keeping Windows extended paths
    /// confined to the direct System.IO boundary.
    /// </summary>
    public static class ContentPipelineFileSystem
    {
        private const int LegacyMaxPath = 260;
        private const int LegacyMaxDirectoryPath = 248;
        private const string ExtendedPathPrefix = @"\\?\";
        private const string ExtendedUncPathPrefix = @"\\?\UNC\";

        public static string Normalize(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static bool IsWithin(string path, string root)
        {
            var fullPath = Normalize(path) + Path.DirectorySeparatorChar;
            var fullRoot = Normalize(root) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        public static string MakeRelative(string root, string path)
        {
            return Path.GetRelativePath(Normalize(root), Normalize(path)).Replace('\\', '/');
        }

        public static bool FileExists(string path)
        {
            return File.Exists(ToSystemPath(path));
        }

        public static bool DirectoryExists(string path)
        {
            return Directory.Exists(ToSystemPath(path, maximumLength: LegacyMaxDirectoryPath));
        }

        public static void CreateDirectory(string path)
        {
            Directory.CreateDirectory(ToSystemPath(path, maximumLength: LegacyMaxDirectoryPath));
        }

        public static Stream OpenRead(string path)
        {
            return File.OpenRead(ToSystemPath(path));
        }

        public static Stream OpenRead(string path, int bufferSize, FileOptions options)
        {
            return new FileStream(
                ToSystemPath(path),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize,
                options);
        }

        public static FileStream OpenFile(
            string path,
            FileMode mode,
            FileAccess access,
            FileShare share)
        {
            return new FileStream(ToSystemPath(path), mode, access, share);
        }

        public static string ReadAllText(string path)
        {
            return File.ReadAllText(ToSystemPath(path));
        }

        public static byte[] ReadAllBytes(string path)
        {
            return File.ReadAllBytes(ToSystemPath(path));
        }

        public static void WriteAllText(string path, string content)
        {
            File.WriteAllText(ToSystemPath(path), content);
        }

        public static void WriteAllText(string path, string content, Encoding encoding)
        {
            File.WriteAllText(ToSystemPath(path), content, encoding);
        }

        public static void WriteAllBytes(string path, byte[] content)
        {
            File.WriteAllBytes(ToSystemPath(path), content);
        }

        public static void CopyFile(string source, string destination, bool overwrite)
        {
            File.Copy(ToSystemPath(source), ToSystemPath(destination), overwrite);
        }

        public static void MoveFile(string source, string destination)
        {
            File.Move(ToSystemPath(source), ToSystemPath(destination));
        }

        public static void ReplaceFile(string source, string destination)
        {
            File.Replace(ToSystemPath(source), ToSystemPath(destination), null);
        }

        public static void DeleteFile(string path)
        {
            File.Delete(ToSystemPath(path));
        }

        public static long GetFileLength(string path)
        {
            return new FileInfo(ToSystemPath(path)).Length;
        }

        public static string[] GetDirectories(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return Directory.GetDirectories(
                    ToSystemPath(path, forceExtended: true),
                    searchPattern,
                    searchOption)
                .Select(FromSystemPath)
                .ToArray();
        }

        public static string[] GetFiles(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return Directory.GetFiles(
                    ToSystemPath(path, forceExtended: true),
                    searchPattern,
                    searchOption)
                .Select(FromSystemPath)
                .ToArray();
        }

        public static string[] GetFileSystemEntries(string path)
        {
            return Directory.GetFileSystemEntries(ToSystemPath(path, forceExtended: true))
                .Select(FromSystemPath)
                .ToArray();
        }

        public static void MoveDirectory(string source, string destination)
        {
            Directory.Move(
                ToSystemPath(source, maximumLength: LegacyMaxDirectoryPath),
                ToSystemPath(destination, maximumLength: LegacyMaxDirectoryPath));
        }

        public static void DeleteDirectory(string path, bool recursive = false)
        {
            Directory.Delete(
                ToSystemPath(path, maximumLength: LegacyMaxDirectoryPath),
                recursive);
        }

        public static void CopyDirectory(string source, string destination)
        {
            if (!DirectoryExists(source)) return;
            CreateDirectory(destination);
            foreach (var directory in GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
            }

            foreach (var file in GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(destination, Path.GetRelativePath(source, file));
                CreateDirectory(Path.GetDirectoryName(target)!);
                CopyFile(file, target, true);
            }
        }

        public static void AtomicWrite(string path, string content)
        {
            CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temporaryPath = path + ".tmp";
            WriteAllText(temporaryPath, content, new UTF8Encoding(false));
            if (FileExists(path))
            {
                ReplaceFile(temporaryPath, path);
            }
            else
            {
                MoveFile(temporaryPath, path);
            }
        }

        private static string ToSystemPath(
            string path,
            bool forceExtended = false,
            int maximumLength = LegacyMaxPath)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                Path.DirectorySeparatorChar != '\\' ||
                path.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal))
            {
                return path;
            }

            var fullPath = Path.GetFullPath(path);
            if (!forceExtended && fullPath.Length < maximumLength)
            {
                return path;
            }

            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return ExtendedUncPathPrefix + fullPath[2..];
            }

            return ExtendedPathPrefix + fullPath;
        }

        private static string FromSystemPath(string path)
        {
            if (path.StartsWith(ExtendedUncPathPrefix, StringComparison.Ordinal))
            {
                return @"\\" + path[ExtendedUncPathPrefix.Length..];
            }

            return path.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal)
                ? path[ExtendedPathPrefix.Length..]
                : path;
        }
    }
}
