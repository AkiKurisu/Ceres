using System;
using System.Collections.Generic;
using System.Linq;

namespace Ceres.ContentPipeline
{
    public sealed class ContentBuildGraphBuilder
    {
        public ContentBuildGraph Build(
            IEnumerable<IContentBuildGraphContributor> contributors,
            IContentAssetDependencyResolver dependencyResolver)
        {
            if (contributors == null) throw new ArgumentNullException(nameof(contributors));
            if (dependencyResolver == null) throw new ArgumentNullException(nameof(dependencyResolver));

            var diagnostics = new DiagnosticCollector();
            var orderedContributors = contributors
                .Where(contributor => contributor != null)
                .OrderBy(contributor => contributor.Id ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(contributor => contributor.GetType().AssemblyQualifiedName, StringComparer.Ordinal)
                .ToArray();
            DetectContributorIdConflicts(orderedContributors, diagnostics);

            var contexts = new List<ContentBuildGraphContributionContext>(orderedContributors.Length);
            for (var i = 0; i < orderedContributors.Length; i++)
            {
                var contributor = orderedContributors[i];
                var contributorId = contributor.Id ?? string.Empty;
                var context = new ContentBuildGraphContributionContext(contributorId);
                try
                {
                    contributor.Contribute(context);
                }
                catch (Exception exception)
                {
                    throw new ContentBuildGraphContributorException(contributorId, exception);
                }

                contexts.Add(context);
            }

            var scopes = MergeScopes(contexts, diagnostics);
            var nodes = MergeExplicitAssets(contexts, scopes, diagnostics);
            var edges = ResolveDependencies(nodes, scopes, dependencyResolver, diagnostics);
            var plannedNodes = PlanNodes(nodes, scopes, diagnostics);
            var orderedScopes = scopes.Values.OrderBy(scope => scope.Id, StringComparer.Ordinal).ToArray();
            var orderedEdges = edges
                .OrderBy(edge => edge.SourceAssetId, StringComparer.Ordinal)
                .ThenBy(edge => edge.DependencyAssetId, StringComparer.Ordinal)
                .Select(edge => new ContentDependencyEdge(edge.SourceAssetId, edge.DependencyAssetId))
                .ToArray();
            var orderedDiagnostics = diagnostics.ToArray();
            return new ContentBuildGraph(orderedScopes, plannedNodes, orderedEdges, orderedDiagnostics);
        }

        private static void DetectContributorIdConflicts(
            IReadOnlyList<IContentBuildGraphContributor> contributors,
            DiagnosticCollector diagnostics)
        {
            foreach (var group in contributors.GroupBy(
                         contributor => contributor.Id ?? string.Empty,
                         StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(group.Key))
                {
                    diagnostics.Add(
                        "CBG0001",
                        ContentDiagnosticSeverity.Error,
                        "A content graph contributor has an empty stable ID.",
                        suggestion: "Assign every contributor a stable, project-independent ID.");
                }
                else if (group.Count() > 1)
                {
                    diagnostics.Add(
                        "CBG0002",
                        ContentDiagnosticSeverity.Error,
                        $"Contributor ID '{group.Key}' is registered more than once.",
                        suggestion: "Register one contributor per stable contributor ID.");
                }
            }
        }

        private static Dictionary<string, ContentScopeDefinition> MergeScopes(
            IReadOnlyList<ContentBuildGraphContributionContext> contexts,
            DiagnosticCollector diagnostics)
        {
            var scopes = new Dictionary<string, ContentScopeDefinition>(StringComparer.Ordinal);
            var orderedScopes = contexts.SelectMany(context => context.Scopes)
                .OrderBy(scope => scope.Id, StringComparer.Ordinal)
                .ThenBy(scope => scope.DisplayName, StringComparer.Ordinal);
            foreach (var scope in orderedScopes)
            {
                if (string.IsNullOrWhiteSpace(scope.Id))
                {
                    diagnostics.Add(
                        "CBG1001",
                        ContentDiagnosticSeverity.Error,
                        "A content scope has an empty stable ID.",
                        suggestion: "Use a persistent source identifier such as an asset GUID.");
                    continue;
                }

                if (!scopes.TryGetValue(scope.Id, out var existing))
                {
                    scopes.Add(scope.Id, scope);
                    continue;
                }

                if (!ScopeEquals(existing, scope))
                {
                    diagnostics.Add(
                        "CBG1002",
                        ContentDiagnosticSeverity.Error,
                        $"Scope ID '{scope.Id}' has incompatible definitions.",
                        scope.Id,
                        suggestion: "Give distinct sources distinct stable IDs or make their scope metadata identical.");
                }
            }

            return scopes;
        }

        private static Dictionary<string, MutableAssetNode> MergeExplicitAssets(
            IReadOnlyList<ContentBuildGraphContributionContext> contexts,
            IReadOnlyDictionary<string, ContentScopeDefinition> scopes,
            DiagnosticCollector diagnostics)
        {
            var nodes = new Dictionary<string, MutableAssetNode>(StringComparer.Ordinal);
            var contributions = contexts.SelectMany(context => context.Assets)
                .OrderBy(asset => asset.AssetId, StringComparer.Ordinal)
                .ThenBy(asset => asset.ScopeId, StringComparer.Ordinal)
                .ThenBy(asset => asset.AssetPath, StringComparer.Ordinal)
                .ToArray();
            foreach (var contribution in contributions)
            {
                if (string.IsNullOrWhiteSpace(contribution.ScopeId) ||
                    !scopes.ContainsKey(contribution.ScopeId))
                {
                    diagnostics.Add(
                        "CBG1101",
                        ContentDiagnosticSeverity.Error,
                        $"Asset '{contribution.AssetId}' refers to unknown scope '{contribution.ScopeId}'.",
                        contribution.ScopeId,
                        contribution.AssetId,
                        "Contribute the scope before contributing its assets.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(contribution.AssetId))
                {
                    diagnostics.Add(
                        "CBG1102",
                        ContentDiagnosticSeverity.Error,
                        $"Scope '{contribution.ScopeId}' contains an asset with an empty stable ID.",
                        contribution.ScopeId,
                        suggestion: "Resolve the asset to a Unity GUID or a deterministic generated-asset ID.");
                    continue;
                }

                if (!nodes.TryGetValue(contribution.AssetId, out var node))
                {
                    node = MutableAssetNode.FromContribution(contribution);
                    nodes.Add(contribution.AssetId, node);
                }
                else
                {
                    MergeContribution(node, contribution, diagnostics);
                }
            }

            DetectAddressConflicts(nodes.Values, diagnostics);
            DetectExplicitPathConflicts(nodes.Values, diagnostics);
            return nodes;
        }

        private static void MergeContribution(
            MutableAssetNode node,
            ContentAssetContribution contribution,
            DiagnosticCollector diagnostics)
        {
            if (!string.IsNullOrEmpty(node.AssetPath) &&
                !string.IsNullOrEmpty(contribution.AssetPath) &&
                !string.Equals(node.AssetPath, contribution.AssetPath, StringComparison.Ordinal))
            {
                diagnostics.Add(
                    "CBG1103",
                    ContentDiagnosticSeverity.Error,
                    $"Asset ID '{node.AssetId}' maps to both '{node.AssetPath}' and '{contribution.AssetPath}'.",
                    contribution.ScopeId,
                    node.AssetId,
                    "Stable asset IDs must map to one canonical asset path.");
            }
            else if (string.IsNullOrEmpty(node.AssetPath))
            {
                node.AssetPath = contribution.AssetPath;
            }

            if (!string.IsNullOrEmpty(node.Address) &&
                !string.IsNullOrEmpty(contribution.Address) &&
                !string.Equals(node.Address, contribution.Address, StringComparison.Ordinal))
            {
                diagnostics.Add(
                    "CBG1104",
                    ContentDiagnosticSeverity.Error,
                    $"Asset '{node.AssetId}' has conflicting addresses '{node.Address}' and '{contribution.Address}'.",
                    contribution.ScopeId,
                    node.AssetId,
                    "Use one runtime address per stable asset.");
            }
            else if (string.IsNullOrEmpty(node.Address))
            {
                node.Address = contribution.Address;
            }

            if (!string.IsNullOrEmpty(node.TypeName) &&
                !string.IsNullOrEmpty(contribution.TypeName) &&
                !string.Equals(node.TypeName, contribution.TypeName, StringComparison.Ordinal))
            {
                diagnostics.Add(
                    "CBG1105",
                    ContentDiagnosticSeverity.Error,
                    $"Asset '{node.AssetId}' has conflicting types '{node.TypeName}' and '{contribution.TypeName}'.",
                    contribution.ScopeId,
                    node.AssetId);
            }
            else if (string.IsNullOrEmpty(node.TypeName))
            {
                node.TypeName = contribution.TypeName;
            }

            node.IsExplicit = true;
            node.ExplicitScopeIds.Add(contribution.ScopeId);
            node.UsageScopeIds.Add(contribution.ScopeId);
            node.Labels.UnionWith(contribution.Labels);
            if (contribution.LocationHint != ContentLocation.Unspecified)
            {
                node.LocationHints.Add(contribution.LocationHint);
            }

            if (contribution.OwnershipHint != ContentOwnership.Unspecified)
            {
                node.OwnershipHints.Add(contribution.OwnershipHint);
            }

            if (!string.IsNullOrWhiteSpace(contribution.PackingHint))
            {
                node.PackingHints.Add(contribution.PackingHint);
            }
        }

        private static void DetectAddressConflicts(
            IEnumerable<MutableAssetNode> nodes,
            DiagnosticCollector diagnostics)
        {
            foreach (var group in nodes.Where(node => !string.IsNullOrEmpty(node.Address))
                         .GroupBy(node => node.Address, StringComparer.Ordinal)
                         .Where(group => group.Select(node => node.AssetId)
                             .Distinct(StringComparer.Ordinal)
                             .Skip(1)
                             .Any())
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var assetIds = group.Select(node => node.AssetId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                diagnostics.Add(
                    "CBG1106",
                    ContentDiagnosticSeverity.Error,
                    $"Address '{group.Key}' maps to multiple assets: {string.Join(", ", assetIds)}.",
                    assetId: assetIds[0],
                    suggestion: "Assign unique addresses or define an explicit project-level override policy.");
            }
        }

        private static void DetectExplicitPathConflicts(
            IEnumerable<MutableAssetNode> nodes,
            DiagnosticCollector diagnostics)
        {
            foreach (var group in nodes
                         .Where(node => !string.IsNullOrEmpty(node.AssetPath))
                         .GroupBy(node => node.AssetPath, StringComparer.Ordinal)
                         .Where(group => group.Select(node => node.AssetId)
                             .Distinct(StringComparer.Ordinal)
                             .Skip(1)
                             .Any())
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var assetIds = group.Select(node => node.AssetId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                diagnostics.Add(
                    "CBG1107",
                    ContentDiagnosticSeverity.Error,
                    $"Asset path '{group.Key}' is contributed with multiple asset IDs: " +
                    $"{string.Join(", ", assetIds)}. Sub-assets are not supported by the Addressables backend.",
                    assetId: assetIds[0],
                    suggestion:
                    "Contribute the exact main-asset GUID once, or promote each sub-asset to a standalone asset.");
            }
        }

        private static HashSet<ContentDependencyEdgeKey> ResolveDependencies(
            IDictionary<string, MutableAssetNode> nodes,
            IReadOnlyDictionary<string, ContentScopeDefinition> scopes,
            IContentAssetDependencyResolver resolver,
            DiagnosticCollector diagnostics)
        {
            var edges = new HashSet<ContentDependencyEdgeKey>();
            var dependencyCache = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var resolutionCache = new Dictionary<string, ResolvedContentAsset>(StringComparer.Ordinal);
            var pending = new Queue<ScopeAssetPair>();
            foreach (var node in nodes.Values.OrderBy(value => value.AssetId, StringComparer.Ordinal))
            {
                foreach (var scopeId in node.ExplicitScopeIds.OrderBy(value => value, StringComparer.Ordinal))
                {
                    pending.Enqueue(new ScopeAssetPair(scopeId, node.AssetId));
                }
            }

            var visited = new HashSet<ScopeAssetPair>();
            while (pending.Count > 0)
            {
                var pair = pending.Dequeue();
                if (!visited.Add(pair) || !nodes.TryGetValue(pair.AssetId, out var source))
                {
                    continue;
                }

                source.UsageScopeIds.Add(pair.ScopeId);
                if (string.IsNullOrEmpty(source.AssetPath))
                {
                    diagnostics.Add(
                        "CBG1201",
                        ContentDiagnosticSeverity.Error,
                        $"Asset '{source.AssetId}' has no resolvable AssetDatabase path.",
                        pair.ScopeId,
                        source.AssetId,
                        "Repair or remove the missing source reference.");
                    continue;
                }

                if (!resolutionCache.TryGetValue(source.AssetPath, out var sourceResolution))
                {
                    try
                    {
                        sourceResolution = resolver.Resolve(source.AssetPath);
                    }
                    catch (Exception exception)
                    {
                        diagnostics.Add(
                            "CBG1203",
                            ContentDiagnosticSeverity.Error,
                            $"Asset resolution failed for '{source.AssetPath}': {exception.Message}",
                            pair.ScopeId,
                            source.AssetId);
                        continue;
                    }

                    resolutionCache[source.AssetPath] = sourceResolution;
                }

                if (sourceResolution == null || !sourceResolution.IsValid ||
                    string.IsNullOrWhiteSpace(sourceResolution.AssetId))
                {
                    diagnostics.Add(
                        "CBG1204",
                        ContentDiagnosticSeverity.Error,
                        $"Asset '{source.AssetPath}' is missing or invalid.",
                        pair.ScopeId,
                        source.AssetId,
                        "Restore the asset or remove the broken source reference.");
                    continue;
                }

                if (!string.Equals(source.AssetId, sourceResolution.AssetId, StringComparison.Ordinal))
                {
                    diagnostics.Add(
                        "CBG1206",
                        ContentDiagnosticSeverity.Error,
                        $"Asset ID '{source.AssetId}' does not match path '{source.AssetPath}' " +
                        $"(resolved ID '{sourceResolution.AssetId}').",
                        pair.ScopeId,
                        source.AssetId,
                        "Use the exact AssetDatabase GUID for the main asset. " +
                        "Sub-assets must be promoted to standalone assets before contribution.");
                }

                if (string.IsNullOrEmpty(source.TypeName))
                {
                    source.TypeName = sourceResolution.TypeName;
                }

                if (!dependencyCache.TryGetValue(source.AssetPath, out var dependencyPaths))
                {
                    try
                    {
                        dependencyPaths = (resolver.GetDirectDependencies(source.AssetPath) ??
                                           Array.Empty<string>())
                            .Select(ContentBuildGraphCollections.NormalizePath)
                            .Where(path => !string.IsNullOrEmpty(path) &&
                                           !string.Equals(path, source.AssetPath, StringComparison.Ordinal))
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(path => path, StringComparer.Ordinal)
                            .ToArray();
                        dependencyCache[source.AssetPath] = dependencyPaths;
                    }
                    catch (Exception exception)
                    {
                        diagnostics.Add(
                            "CBG1202",
                            ContentDiagnosticSeverity.Error,
                            $"Dependency resolution failed for '{source.AssetPath}': {exception.Message}",
                            pair.ScopeId,
                            source.AssetId,
                            "Inspect the asset import state and dependency resolver.");
                        continue;
                    }
                }

                for (var i = 0; i < dependencyPaths.Count; i++)
                {
                    var dependencyPath = dependencyPaths[i];
                    if (!resolutionCache.TryGetValue(dependencyPath, out var resolved))
                    {
                        try
                        {
                            resolved = resolver.Resolve(dependencyPath);
                        }
                        catch (Exception exception)
                        {
                            diagnostics.Add(
                                "CBG1203",
                                ContentDiagnosticSeverity.Error,
                                $"Asset resolution failed for '{dependencyPath}': {exception.Message}",
                                pair.ScopeId,
                                source.AssetId);
                            continue;
                        }

                        resolutionCache[dependencyPath] = resolved;
                    }

                    if (resolved == null || !resolved.IsValid || string.IsNullOrWhiteSpace(resolved.AssetId))
                    {
                        diagnostics.Add(
                            "CBG1204",
                            ContentDiagnosticSeverity.Error,
                            $"Dependency '{dependencyPath}' referenced by '{source.AssetPath}' is missing or invalid.",
                            pair.ScopeId,
                            source.AssetId,
                            "Restore the dependency or remove the broken reference.");
                        continue;
                    }

                    if (!nodes.TryGetValue(resolved.AssetId, out var dependency))
                    {
                        dependency = MutableAssetNode.FromResolvedAsset(resolved);
                        nodes.Add(resolved.AssetId, dependency);
                    }
                    else if (!string.IsNullOrEmpty(dependency.AssetPath) &&
                             !string.Equals(dependency.AssetPath, resolved.AssetPath, StringComparison.Ordinal))
                    {
                        diagnostics.Add(
                            "CBG1205",
                            ContentDiagnosticSeverity.Error,
                            $"Resolved asset ID '{resolved.AssetId}' maps to incompatible dependency paths.",
                            pair.ScopeId,
                            resolved.AssetId);
                    }

                    dependency.UsageScopeIds.Add(pair.ScopeId);
                    edges.Add(new ContentDependencyEdgeKey(source.AssetId, dependency.AssetId));
                    pending.Enqueue(new ScopeAssetPair(pair.ScopeId, dependency.AssetId));
                }
            }

            return edges;
        }

        private static ContentAssetNode[] PlanNodes(
            IReadOnlyDictionary<string, MutableAssetNode> nodes,
            IReadOnlyDictionary<string, ContentScopeDefinition> scopes,
            DiagnosticCollector diagnostics)
        {
            var planned = new List<ContentAssetNode>(nodes.Count);
            foreach (var node in nodes.Values.OrderBy(value => value.AssetId, StringComparer.Ordinal))
            {
                if (node.ExplicitScopeIds.Count > 1)
                {
                    diagnostics.Add(
                        "CBG1301",
                        ContentDiagnosticSeverity.Info,
                        $"Asset '{node.AssetId}' is explicit in multiple scopes and will be promoted to Shared: " +
                        $"{string.Join(", ", node.ExplicitScopeIds.OrderBy(value => value, StringComparer.Ordinal))}.",
                        assetId: node.AssetId,
                        suggestion: "Keep the stable asset/address consistent; conflicting contributions are rejected separately.");
                }

                if (node.OwnershipHints.Count > 1)
                {
                    diagnostics.Add(
                        "CBG1302",
                        ContentDiagnosticSeverity.Error,
                        $"Asset '{node.AssetId}' has incompatible ownership hints: " +
                        $"{string.Join(", ", node.OwnershipHints.OrderBy(value => value))}.",
                        assetId: node.AssetId);
                }

                if (node.LocationHints.Count > 1)
                {
                    diagnostics.Add(
                        "CBG1303",
                        ContentDiagnosticSeverity.Error,
                        $"Asset '{node.AssetId}' has incompatible location hints: " +
                        $"{string.Join(", ", node.LocationHints.OrderBy(value => value))}.",
                        assetId: node.AssetId);
                }

                var ownership = ResolveOwnership(node);
                var ownerScopeId = ResolveOwnerScope(node, ownership);
                var partitionId = ResolvePartitionId(ownership, ownerScopeId);
                var location = ResolveLocation(node, scopes);
                var sharedCandidate = node.UsageScopeIds.Count > 1 &&
                                      (!node.IsExplicit || ownership == ContentOwnership.Shared);
                planned.Add(new ContentAssetNode(
                    node.AssetId,
                    node.AssetPath,
                    node.Address,
                    ContentBuildGraphCollections.CopyStrings(node.Labels),
                    node.TypeName,
                    node.IsExplicit,
                    ContentBuildGraphCollections.CopyStrings(node.ExplicitScopeIds),
                    ContentBuildGraphCollections.CopyStrings(node.UsageScopeIds),
                    location,
                    ownership,
                    ownerScopeId,
                    partitionId,
                    sharedCandidate,
                    ContentBuildGraphCollections.CopyStrings(node.PackingHints)));
            }

            return planned.ToArray();
        }

        private static ContentOwnership ResolveOwnership(MutableAssetNode node)
        {
            if (node.OwnershipHints.Count == 1)
            {
                var hint = node.OwnershipHints.First();
                if (hint != ContentOwnership.Scope)
                {
                    return hint;
                }
            }

            if (node.ExplicitScopeIds.Count > 1 || node.UsageScopeIds.Count > 1)
            {
                return ContentOwnership.Shared;
            }

            return ContentOwnership.Scope;
        }

        private static string ResolveOwnerScope(MutableAssetNode node, ContentOwnership ownership)
        {
            if (ownership != ContentOwnership.Scope) return string.Empty;
            if (node.ExplicitScopeIds.Count == 1) return node.ExplicitScopeIds.First();
            return node.UsageScopeIds.OrderBy(value => value, StringComparer.Ordinal).FirstOrDefault() ??
                   string.Empty;
        }

        private static string ResolvePartitionId(ContentOwnership ownership, string ownerScopeId)
        {
            return ownership switch
            {
                ContentOwnership.Scope => string.IsNullOrEmpty(ownerScopeId)
                    ? "scope:unresolved"
                    : $"scope:{ownerScopeId}",
                ContentOwnership.Shared => "shared",
                ContentOwnership.BuiltIn => "built-in",
                ContentOwnership.Metadata => "metadata",
                ContentOwnership.Excluded => "excluded",
                _ => "unresolved"
            };
        }

        private static ContentLocation ResolveLocation(
            MutableAssetNode node,
            IReadOnlyDictionary<string, ContentScopeDefinition> scopes)
        {
            if (node.LocationHints.Count == 1)
            {
                return node.LocationHints.First();
            }

            if (node.LocationHints.Count > 1)
            {
                return ContentLocation.Unspecified;
            }

            var scopeLocations = node.UsageScopeIds
                .Where(scopes.ContainsKey)
                .Select(scopeId => scopes[scopeId].DefaultLocation)
                .Where(location => location != ContentLocation.Unspecified)
                .Distinct()
                .ToArray();
            return scopeLocations.Length == 1 ? scopeLocations[0] : ContentLocation.Unspecified;
        }

        private static bool ScopeEquals(ContentScopeDefinition left, ContentScopeDefinition right)
        {
            return string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) &&
                   string.Equals(left.Version, right.Version, StringComparison.Ordinal) &&
                   left.Enabled == right.Enabled &&
                   left.DefaultLocation == right.DefaultLocation &&
                   left.Properties.Count == right.Properties.Count &&
                   left.Properties.All(pair =>
                       right.Properties.TryGetValue(pair.Key, out var value) &&
                       string.Equals(pair.Value, value, StringComparison.Ordinal));
        }

        private sealed class MutableAssetNode
        {
            private MutableAssetNode(string assetId)
            {
                AssetId = assetId;
            }

            public string AssetId { get; }

            public string AssetPath { get; set; } = string.Empty;

            public string Address { get; set; } = string.Empty;

            public string TypeName { get; set; } = string.Empty;

            public bool IsExplicit { get; set; }

            public HashSet<string> Labels { get; } = new(StringComparer.Ordinal);

            public HashSet<string> ExplicitScopeIds { get; } = new(StringComparer.Ordinal);

            public HashSet<string> UsageScopeIds { get; } = new(StringComparer.Ordinal);

            public HashSet<ContentLocation> LocationHints { get; } = new();

            public HashSet<ContentOwnership> OwnershipHints { get; } = new();

            public HashSet<string> PackingHints { get; } = new(StringComparer.Ordinal);

            public static MutableAssetNode FromContribution(ContentAssetContribution contribution)
            {
                var node = new MutableAssetNode(contribution.AssetId);
                MergeContribution(node, contribution, new DiagnosticCollector());
                return node;
            }

            public static MutableAssetNode FromResolvedAsset(ResolvedContentAsset asset)
            {
                return new MutableAssetNode(asset.AssetId)
                {
                    AssetPath = asset.AssetPath,
                    TypeName = asset.TypeName
                };
            }
        }

        private readonly struct ScopeAssetPair : IEquatable<ScopeAssetPair>
        {
            public ScopeAssetPair(string scopeId, string assetId)
            {
                ScopeId = scopeId;
                AssetId = assetId;
            }

            public string ScopeId { get; }

            public string AssetId { get; }

            public bool Equals(ScopeAssetPair other)
            {
                return string.Equals(ScopeId, other.ScopeId, StringComparison.Ordinal) &&
                       string.Equals(AssetId, other.AssetId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is ScopeAssetPair other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((ScopeId != null ? StringComparer.Ordinal.GetHashCode(ScopeId) : 0) * 397) ^
                           (AssetId != null ? StringComparer.Ordinal.GetHashCode(AssetId) : 0);
                }
            }
        }

        private readonly struct ContentDependencyEdgeKey : IEquatable<ContentDependencyEdgeKey>
        {
            public ContentDependencyEdgeKey(string sourceAssetId, string dependencyAssetId)
            {
                SourceAssetId = sourceAssetId;
                DependencyAssetId = dependencyAssetId;
            }

            public string SourceAssetId { get; }

            public string DependencyAssetId { get; }

            public bool Equals(ContentDependencyEdgeKey other)
            {
                return string.Equals(SourceAssetId, other.SourceAssetId, StringComparison.Ordinal) &&
                       string.Equals(DependencyAssetId, other.DependencyAssetId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is ContentDependencyEdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((SourceAssetId != null
                               ? StringComparer.Ordinal.GetHashCode(SourceAssetId)
                               : 0) * 397) ^
                           (DependencyAssetId != null
                               ? StringComparer.Ordinal.GetHashCode(DependencyAssetId)
                               : 0);
                }
            }

            public static implicit operator ContentDependencyEdge(ContentDependencyEdgeKey edge)
            {
                return new ContentDependencyEdge(edge.SourceAssetId, edge.DependencyAssetId);
            }
        }

        private sealed class DiagnosticCollector
        {
            private readonly Dictionary<string, ContentBuildDiagnostic> _diagnostics =
                new(StringComparer.Ordinal);

            public void Add(
                string code,
                ContentDiagnosticSeverity severity,
                string message,
                string scopeId = null,
                string assetId = null,
                string suggestion = null)
            {
                var diagnostic = new ContentBuildDiagnostic(
                    code,
                    severity,
                    message,
                    scopeId,
                    assetId,
                    suggestion);
                var key = string.Join(
                    "\u001f",
                    diagnostic.Code,
                    diagnostic.Severity,
                    diagnostic.ScopeId,
                    diagnostic.AssetId,
                    diagnostic.Message,
                    diagnostic.Suggestion);
                _diagnostics[key] = diagnostic;
            }

            public ContentBuildDiagnostic[] ToArray()
            {
                return _diagnostics.Values
                    .OrderByDescending(diagnostic => diagnostic.Severity)
                    .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                    .ThenBy(diagnostic => diagnostic.ScopeId, StringComparer.Ordinal)
                    .ThenBy(diagnostic => diagnostic.AssetId, StringComparer.Ordinal)
                    .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
                    .ToArray();
            }
        }
    }
}
