using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ceres.ContentPipeline
{
    public sealed class DynamicContentCatalogProjectionResult
    {
        public string OutputPath { get; internal set; }

        public string CatalogPath { get; internal set; }
    }

    public static class DynamicContentCatalogProjection
    {
        public static DynamicContentCatalogProjectionResult Create(
            string releaseManifestPath,
            string storageRoot,
            string outputPath)
        {
            var manifestPath = Path.GetFullPath(releaseManifestPath);
            var release = DynamicContentReleaseManifest.Load(manifestPath);
            var releaseRoot = Path.GetDirectoryName(manifestPath)!;
            var catalogSource = ResolveWithin(releaseRoot, release.catalogRelativePath);
            var bundlePaths = DynamicContentReleaseBuilder.ResolveBundlePaths(
                release,
                storageRoot,
                false);
            var destinationRoot = Path.GetFullPath(outputPath);
            var stagingRoot = destinationRoot + ".staging-" + Guid.NewGuid().ToString("N");
            try
            {
                ContentPipelineFileSystem.CreateDirectory(stagingRoot);
                var catalogPath = Path.Combine(stagingRoot, Path.GetFileName(catalogSource));
                ContentPipelineFileSystem.CopyFile(catalogSource, catalogPath, true);
                var referenced = DynamicContentReleaseBuilder.RewriteCatalog(
                    catalogPath,
                    bundleName => bundlePaths[bundleName].Replace('\\', '/'),
                    new HashSet<string>(bundlePaths.Keys, StringComparer.OrdinalIgnoreCase));
                if (!new HashSet<string>(referenced, StringComparer.OrdinalIgnoreCase)
                        .SetEquals(release.referencedBundles))
                {
                    throw new InvalidDataException(
                        $"Projected Catalog Bundle references do not match the Release: {manifestPath}");
                }

                if (ContentPipelineFileSystem.DirectoryExists(destinationRoot))
                    ContentPipelineFileSystem.DeleteDirectory(destinationRoot, true);
                ContentPipelineFileSystem.CreateDirectory(Path.GetDirectoryName(destinationRoot)!);
                ContentPipelineFileSystem.MoveDirectory(stagingRoot, destinationRoot);
                return new DynamicContentCatalogProjectionResult
                {
                    OutputPath = destinationRoot,
                    CatalogPath = Path.Combine(destinationRoot, Path.GetFileName(catalogSource))
                };
            }
            catch
            {
                if (ContentPipelineFileSystem.DirectoryExists(stagingRoot))
                    ContentPipelineFileSystem.DeleteDirectory(stagingRoot, true);
                throw;
            }
        }

        private static string ResolveWithin(string root, string relativePath)
        {
            var path = Path.GetFullPath(Path.Combine(
                root,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!ContentPipelineFileSystem.IsWithin(path, root))
                throw new InvalidDataException($"Release file escaped its root: {relativePath}");
            return path;
        }
    }

    public sealed class DynamicContentPackageMaterializationResult
    {
        public string PackagePath { get; internal set; }
    }

    public static class DynamicContentPackageMaterializer
    {
        public static DynamicContentPackageMaterializationResult Materialize(
            string releaseManifestPath,
            string storageRoot,
            string packagePath)
        {
            var manifestPath = Path.GetFullPath(releaseManifestPath);
            var release = DynamicContentReleaseManifest.Load(manifestPath);
            var releaseRoot = Path.GetDirectoryName(manifestPath)!;
            var bundlePaths = DynamicContentReleaseBuilder.ResolveBundlePaths(
                release,
                storageRoot,
                true);
            var destinationRoot = Path.GetFullPath(packagePath);
            var stagingRoot = destinationRoot + ".staging-" + Guid.NewGuid().ToString("N");
            try
            {
                ContentPipelineFileSystem.CreateDirectory(stagingRoot);
                CopyReleaseFile(releaseRoot, release.catalogRelativePath, stagingRoot);
                CopyReleaseFile(releaseRoot, release.catalogHashRelativePath, stagingRoot);
                foreach (var bundleName in release.referencedBundles.OrderBy(value => value, StringComparer.Ordinal))
                {
                    if (!string.Equals(bundleName, Path.GetFileName(bundleName), StringComparison.Ordinal) ||
                        !bundlePaths.TryGetValue(bundleName, out var source))
                    {
                        throw new InvalidDataException($"Release contains an invalid Bundle source: {bundleName}");
                    }
                    ContentPipelineFileSystem.CopyFile(source, Path.Combine(stagingRoot, bundleName), true);
                }

                if (ContentPipelineFileSystem.DirectoryExists(destinationRoot))
                    throw new IOException($"Materialized content destination already exists: {destinationRoot}");
                ContentPipelineFileSystem.CreateDirectory(Path.GetDirectoryName(destinationRoot)!);
                ContentPipelineFileSystem.MoveDirectory(stagingRoot, destinationRoot);
                return new DynamicContentPackageMaterializationResult { PackagePath = destinationRoot };
            }
            catch
            {
                if (ContentPipelineFileSystem.DirectoryExists(stagingRoot))
                    ContentPipelineFileSystem.DeleteDirectory(stagingRoot, true);
                throw;
            }
        }

        private static void CopyReleaseFile(string releaseRoot, string relativePath, string destinationRoot)
        {
            var source = Path.GetFullPath(Path.Combine(
                releaseRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!ContentPipelineFileSystem.IsWithin(source, releaseRoot))
                throw new InvalidDataException($"Release file escaped its root: {relativePath}");
            ContentPipelineFileSystem.CopyFile(source, Path.Combine(destinationRoot, Path.GetFileName(source)), true);
        }
    }
}
