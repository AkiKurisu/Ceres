using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Ceres.ContentPipeline
{
    internal static class ContentPipelinePlatformPath
    {
        private static readonly Regex ChannelPattern = new(
            "^[a-z0-9]+(?:-[a-z0-9]+)*$",
            RegexOptions.CultureInvariant);

        public static string GetPlatformRoot(
            string outputRoot,
            string channel,
            BuildTarget target)
        {
            if (string.IsNullOrWhiteSpace(outputRoot))
                throw new ArgumentException("Content OutputRoot is empty.", nameof(outputRoot));
            ValidateChannel(channel);
            return Path.Combine(Path.GetFullPath(outputRoot), channel, target.ToString());
        }

        public static void ValidateChannel(string channel)
        {
            if (string.IsNullOrEmpty(channel) || !ChannelPattern.IsMatch(channel))
            {
                throw new ArgumentException(
                    "Content channel must contain only lowercase ASCII letters, digits, and " +
                    "single hyphens between segments, for example 'development' or 'preview-android'.",
                    nameof(channel));
            }
        }
    }

    public sealed class ContentBuildStorageCleanupPreview
    {
        public string PlatformRoot { get; internal set; }

        public string[] ProtectedDirectories { get; internal set; } = Array.Empty<string>();

        public string[] CandidateDirectories { get; internal set; } = Array.Empty<string>();

        public long CandidateBytes { get; internal set; }
    }

    public sealed class ContentBuildStorageCleanupResult
    {
        public string PlatformRoot { get; internal set; }

        public string[] DeletedDirectories { get; internal set; } = Array.Empty<string>();

        public long DeletedBytes { get; internal set; }

        public string[] Failures { get; internal set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Maintains immutable Addressables artifact history owned by the Ceres content backend.
    /// Callers may preview, but execution always rebuilds the plan while holding the build lock.
    /// </summary>
    public static class ContentBuildStorageMaintenance
    {
        private const string BaselineContainerName = "baselines";
        private const string UpdateContainerName = "updates";
        private const string StagingContainerName = ".staging";
        private const string ManifestFileName = "artifact-manifest.json";
        private const string BaselinePointerName = "current-baseline.json";
        private const string UpdatePointerName = "latest-update-candidate.json";

        public static string GetPlatformRoot(
            string outputRoot,
            string channel,
            BuildTarget target)
        {
            return ContentPipelinePlatformPath.GetPlatformRoot(outputRoot, channel, target);
        }

        public static ContentBuildStorageCleanupPreview Preview(
            string outputRoot,
            string channel,
            BuildTarget target,
            bool clearCurrent = false)
        {
            return CreatePlan(GetPlatformRoot(outputRoot, channel, target), clearCurrent);
        }

        public static ContentBuildStorageCleanupResult Execute(
            string outputRoot,
            string channel,
            BuildTarget target,
            bool clearCurrent = false)
        {
            var platformRoot = GetPlatformRoot(outputRoot, channel, target);
            ContentPipelineFileSystem.CreateDirectory(platformRoot);
            using var processLock = ContentBuildProcessLock.Acquire(platformRoot);
            return Execute(outputRoot, channel, target, processLock, clearCurrent);
        }

        public static ContentBuildStorageCleanupResult Execute(
            string outputRoot,
            string channel,
            BuildTarget target,
            ContentBuildProcessLock processLock,
            bool clearCurrent = false)
        {
            if (processLock == null) throw new ArgumentNullException(nameof(processLock));
            var platformRoot = GetPlatformRoot(outputRoot, channel, target);
            if (!string.Equals(
                    ContentPipelineFileSystem.Normalize(platformRoot),
                    processLock.Root,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Content build lock '{processLock.Root}' does not protect '{platformRoot}'.");
            }
            var plan = CreatePlan(platformRoot, clearCurrent);
            var deleted = new List<string>();
            var failures = new List<string>();
            long deletedBytes = 0;
            if (clearCurrent)
            {
                TryDeleteFile(Path.Combine(platformRoot, BaselinePointerName), failures);
                TryDeleteFile(Path.Combine(platformRoot, UpdatePointerName), failures);
                if (failures.Count > 0)
                {
                    return new ContentBuildStorageCleanupResult
                    {
                        PlatformRoot = platformRoot,
                        DeletedDirectories = deleted.ToArray(),
                        Failures = failures.ToArray()
                    };
                }
            }

            foreach (var candidate in plan.CandidateDirectories)
            {
                if (!ContentPipelineFileSystem.DirectoryExists(candidate)) continue;
                var size = GetDirectorySize(candidate);
                try
                {
                    ContentPipelineFileSystem.DeleteDirectory(candidate, true);
                    deleted.Add(candidate);
                    deletedBytes += size;
                }
                catch (Exception exception)
                {
                    failures.Add($"{candidate}: {exception.Message}");
                }
            }

            TryDeleteEmptyDirectory(Path.Combine(platformRoot, BaselineContainerName));
            TryDeleteEmptyDirectory(Path.Combine(platformRoot, UpdateContainerName));
            TryDeleteEmptyDirectory(Path.Combine(platformRoot, StagingContainerName));
            return new ContentBuildStorageCleanupResult
            {
                PlatformRoot = platformRoot,
                DeletedDirectories = deleted.ToArray(),
                DeletedBytes = deletedBytes,
                Failures = failures.ToArray()
            };
        }

        private static ContentBuildStorageCleanupPreview CreatePlan(
            string platformRoot,
            bool clearCurrent)
        {
            var protectedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var baseline = ReadPointer(
                platformRoot,
                BaselinePointerName,
                BaselineContainerName,
                "baseline");
            var update = ReadPointer(
                platformRoot,
                UpdatePointerName,
                UpdateContainerName,
                "update");
            if (update != null &&
                (baseline == null ||
                 !string.Equals(update.Manifest.baselineId, baseline.Manifest.buildId, StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    $"Latest Update does not belong to the current Baseline: {update.ManifestPath}");
            }

            if (!clearCurrent)
            {
                if (baseline != null) protectedDirectories.Add(baseline.Directory);
                if (update != null) protectedDirectories.Add(update.Directory);
            }

            var candidates = new List<string>();
            CollectArtifactCandidates(
                platformRoot,
                BaselineContainerName,
                "baseline",
                protectedDirectories,
                candidates);
            CollectArtifactCandidates(
                platformRoot,
                UpdateContainerName,
                "update",
                protectedDirectories,
                candidates);
            CollectStagingCandidates(platformRoot, candidates);
            var orderedCandidates = candidates
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new ContentBuildStorageCleanupPreview
            {
                PlatformRoot = platformRoot,
                ProtectedDirectories = protectedDirectories
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                CandidateDirectories = orderedCandidates,
                CandidateBytes = orderedCandidates.Sum(GetDirectorySize)
            };
        }

        private static PointerTarget ReadPointer(
            string platformRoot,
            string pointerName,
            string expectedContainer,
            string expectedKind)
        {
            var pointerPath = Path.Combine(platformRoot, pointerName);
            if (!ContentPipelineFileSystem.FileExists(pointerPath)) return null;
            var pointer = JsonUtility.FromJson<ContentBuildStoragePointer>(
                ContentPipelineFileSystem.ReadAllText(pointerPath));
            if (pointer == null ||
                string.IsNullOrWhiteSpace(pointer.buildId) ||
                string.IsNullOrWhiteSpace(pointer.manifest))
            {
                throw new InvalidDataException($"Content build pointer is invalid: {pointerPath}");
            }

            var manifestPath = ResolveWithin(platformRoot, pointer.manifest, "content build pointer");
            var expectedRoot = Path.Combine(platformRoot, expectedContainer);
            if (!ContentPipelineFileSystem.IsWithin(manifestPath, expectedRoot) ||
                !string.Equals(Path.GetFileName(manifestPath), ManifestFileName, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Content build pointer is outside its '{expectedContainer}' container: {pointerPath}");
            }

            var directory = Path.GetDirectoryName(manifestPath)!;
            if (!string.Equals(Path.GetFileName(directory), pointer.buildId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Content build pointer directory does not match Build ID: {pointerPath}");
            }

            var manifest = ContentArtifactManifest.Load(manifestPath);
            if (!string.Equals(pointer.buildId, manifest.buildId, StringComparison.Ordinal) ||
                !string.Equals(manifest.buildKind, expectedKind, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Content build pointer identity does not match its {expectedKind} Manifest: {pointerPath}");
            }

            return new PointerTarget(directory, manifestPath, manifest);
        }

        private static void CollectArtifactCandidates(
            string platformRoot,
            string containerName,
            string expectedKind,
            ISet<string> protectedDirectories,
            ICollection<string> candidates)
        {
            var container = Path.Combine(platformRoot, containerName);
            if (!ContentPipelineFileSystem.DirectoryExists(container)) return;
            foreach (var directory in ContentPipelineFileSystem.GetDirectories(container))
            {
                var resolved = Path.GetFullPath(directory);
                if (!ContentPipelineFileSystem.IsWithin(resolved, container))
                    throw new InvalidDataException($"Artifact directory escapes its container: {resolved}");
                var manifestPath = Path.Combine(resolved, ManifestFileName);
                var manifest = ContentArtifactManifest.Load(manifestPath);
                if (!string.Equals(manifest.buildKind, expectedKind, StringComparison.Ordinal) ||
                    !string.Equals(manifest.buildId, Path.GetFileName(resolved), StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Artifact directory identity is invalid: {resolved}");
                }
                if (!protectedDirectories.Contains(resolved)) candidates.Add(resolved);
            }
        }

        private static void CollectStagingCandidates(
            string platformRoot,
            ICollection<string> candidates)
        {
            var stagingRoot = Path.Combine(platformRoot, StagingContainerName);
            if (!ContentPipelineFileSystem.DirectoryExists(stagingRoot)) return;
            foreach (var directory in ContentPipelineFileSystem.GetDirectories(stagingRoot))
            {
                var resolved = Path.GetFullPath(directory);
                if (!ContentPipelineFileSystem.IsWithin(resolved, stagingRoot))
                    throw new InvalidDataException($"Staging directory escapes its container: {resolved}");
                if (Guid.TryParseExact(Path.GetFileName(resolved), "N", out _))
                    candidates.Add(resolved);
            }
        }

        private static string ResolveWithin(string root, string relativePath, string description)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                throw new InvalidDataException($"{description} path must be relative: {relativePath}");
            var resolved = Path.GetFullPath(Path.Combine(
                root,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!ContentPipelineFileSystem.IsWithin(resolved, root))
                throw new InvalidDataException($"{description} path escapes its root: {relativePath}");
            if (!ContentPipelineFileSystem.FileExists(resolved))
                throw new FileNotFoundException($"{description} target is missing.", resolved);
            return resolved;
        }

        private static long GetDirectorySize(string path)
        {
            if (!ContentPipelineFileSystem.DirectoryExists(path)) return 0;
            long result = 0;
            foreach (var file in ContentPipelineFileSystem.GetFiles(
                         path,
                         "*",
                         SearchOption.AllDirectories))
            {
                result = checked(result + ContentPipelineFileSystem.GetFileLength(file));
            }
            return result;
        }

        private static void TryDeleteFile(
            string path,
            ICollection<string> failures)
        {
            if (!ContentPipelineFileSystem.FileExists(path)) return;
            try
            {
                ContentPipelineFileSystem.DeleteFile(path);
            }
            catch (Exception exception)
            {
                failures.Add($"{path}: {exception.Message}");
            }
        }

        private static void TryDeleteEmptyDirectory(string path)
        {
            if (!ContentPipelineFileSystem.DirectoryExists(path) ||
                ContentPipelineFileSystem.GetFileSystemEntries(path).Length != 0)
            {
                return;
            }
            try
            {
                ContentPipelineFileSystem.DeleteDirectory(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        [Serializable]
        private sealed class ContentBuildStoragePointer
        {
            public string buildId;
            public string manifest;
        }

        private sealed class PointerTarget
        {
            public PointerTarget(
                string directory,
                string manifestPath,
                ContentArtifactManifest manifest)
            {
                Directory = directory;
                ManifestPath = manifestPath;
                Manifest = manifest;
            }

            public string Directory { get; }

            public string ManifestPath { get; }

            public ContentArtifactManifest Manifest { get; }
        }
    }
}
