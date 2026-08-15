using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Ceres.ContentPipeline
{
    public sealed class UnityContentAssetDependencyResolver : IContentAssetDependencyResolver
    {
        public ResolvedContentAsset Resolve(string assetPath)
        {
            var normalizedPath = ContentBuildGraphCollections.NormalizePath(assetPath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return new ResolvedContentAsset(string.Empty, string.Empty, string.Empty, false);
            }

            var guid = AssetDatabase.AssetPathToGUID(normalizedPath);
            var type = AssetDatabase.GetMainAssetTypeAtPath(normalizedPath);
            return new ResolvedContentAsset(
                guid,
                normalizedPath,
                type?.AssemblyQualifiedName,
                !string.IsNullOrEmpty(guid) && type != null);
        }

        public IReadOnlyList<string> GetDirectDependencies(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return Array.Empty<string>();

            return AssetDatabase.GetDependencies(assetPath, false)
                .Select(ContentBuildGraphCollections.NormalizePath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
