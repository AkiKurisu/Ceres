using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Ceres.ContentPipeline
{
    internal sealed class ContentBuildSnapshot
    {
        public ContentScopeSnapshot[] Scopes { get; private set; }

        public ContentAssetSnapshot[] Assets { get; private set; }

        public static ContentBuildSnapshot Create(ContentBuildGraph graph, ContentAssetBuildDependencies dependencies)
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
                    fingerprint = dependencies.ApplyToFingerprint(node.AssetId, fingerprint),
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
            ContentArtifactManifest previous,
            ContentBuildSnapshot current)
        {
            var oldAssets = previous.assets.ToDictionary(asset => asset.id, StringComparer.Ordinal);
            var newAssets = current.Assets.ToDictionary(asset => asset.id, StringComparer.Ordinal);
            var impacted = new HashSet<string>(StringComparer.Ordinal);
            var violations = new List<string>();
            foreach (var assetId in oldAssets.Keys
                         .Union(newAssets.Keys, StringComparer.Ordinal)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                oldAssets.TryGetValue(assetId, out var oldAsset);
                newAssets.TryGetValue(assetId, out var newAsset);
                if (oldAsset != null && newAsset != null &&
                    string.Equals(oldAsset.fingerprint, newAsset.fingerprint, StringComparison.Ordinal))
                    continue;

                var asset = newAsset ?? oldAsset;
                if (string.Equals(oldAsset?.location, ContentLocation.Local.ToString(), StringComparison.Ordinal) ||
                    string.Equals(newAsset?.location, ContentLocation.Local.ToString(), StringComparison.Ordinal) ||
                    oldAsset != null && newAsset != null &&
                    !string.Equals(oldAsset.location, newAsset.location, StringComparison.Ordinal))
                {
                    violations.Add($"{assetId} changes a delivery boundary and requires Build Full");
                    continue;
                }

                var assetScopes = new HashSet<string>(
                    oldAsset?.usageScopeIds ?? Array.Empty<string>(),
                    StringComparer.Ordinal);
                assetScopes.UnionWith(newAsset?.usageScopeIds ?? Array.Empty<string>());
                if (!string.IsNullOrEmpty(oldAsset?.ownerScopeId)) assetScopes.Add(oldAsset.ownerScopeId);
                if (!string.IsNullOrEmpty(newAsset?.ownerScopeId)) assetScopes.Add(newAsset.ownerScopeId);
                impacted.UnionWith(assetScopes);
                if (assetScopes.Count == 0 &&
                    asset.ownership is not (nameof(ContentOwnership.BuiltIn) or nameof(ContentOwnership.Excluded)))
                {
                    violations.Add($"{assetId} has no delivery scope and requires Build Full");
                }
            }

            var oldScopes = previous.scopes.ToDictionary(scope => scope.id, StringComparer.Ordinal);
            var newScopes = current.Scopes.ToDictionary(scope => scope.id, StringComparer.Ordinal);
            foreach (var scopeId in oldScopes.Keys.Union(newScopes.Keys, StringComparer.Ordinal))
            {
                oldScopes.TryGetValue(scopeId, out var oldScope);
                newScopes.TryGetValue(scopeId, out var newScope);
                if (oldScope != null && newScope != null &&
                    string.Equals(oldScope.fingerprint, newScope.fingerprint, StringComparison.Ordinal))
                    continue;
                impacted.Add(scopeId);
            }

            if (violations.Count > 0)
            {
                throw new InvalidOperationException(
                    "Automatic incremental build contains changes that cannot be delivered remotely:" +
                    Environment.NewLine + string.Join(Environment.NewLine, violations));
            }

            return impacted.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }
}
