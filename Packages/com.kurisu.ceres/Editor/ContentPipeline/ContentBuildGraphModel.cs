using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ceres.ContentPipeline
{
    public enum ContentLocation
    {
        Unspecified,
        Local,
        Remote
    }

    public enum ContentOwnership
    {
        Unspecified,
        Scope,
        Shared,
        BuiltIn,
        Metadata,
        Excluded
    }

    public enum ContentDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class ContentScopeDefinition
    {
        public ContentScopeDefinition(
            string id,
            string displayName,
            string version = null,
            bool enabled = true,
            ContentLocation defaultLocation = ContentLocation.Unspecified,
            IReadOnlyDictionary<string, string> properties = null)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Version = version ?? string.Empty;
            Enabled = enabled;
            DefaultLocation = defaultLocation;
            Properties = ContentBuildGraphCollections.CopyProperties(properties);
        }

        public string Id { get; }

        public string DisplayName { get; }

        public string Version { get; }

        public bool Enabled { get; }

        public ContentLocation DefaultLocation { get; }

        public IReadOnlyDictionary<string, string> Properties { get; }
    }

    public sealed class ContentAssetContribution
    {
        public ContentAssetContribution(
            string scopeId,
            string assetId,
            string assetPath,
            string address = null,
            IEnumerable<string> labels = null,
            string typeName = null,
            ContentLocation locationHint = ContentLocation.Unspecified,
            ContentOwnership ownershipHint = ContentOwnership.Unspecified,
            string packingHint = null)
        {
            ScopeId = scopeId ?? string.Empty;
            AssetId = assetId ?? string.Empty;
            AssetPath = ContentBuildGraphCollections.NormalizePath(assetPath);
            Address = address ?? string.Empty;
            Labels = ContentBuildGraphCollections.CopyStrings(labels);
            TypeName = typeName ?? string.Empty;
            LocationHint = locationHint;
            OwnershipHint = ownershipHint;
            PackingHint = packingHint ?? string.Empty;
        }

        public string ScopeId { get; }

        public string AssetId { get; }

        public string AssetPath { get; }

        public string Address { get; }

        public IReadOnlyList<string> Labels { get; }

        public string TypeName { get; }

        public ContentLocation LocationHint { get; }

        public ContentOwnership OwnershipHint { get; }

        public string PackingHint { get; }
    }

    public interface IContentBuildGraphContributor
    {
        string Id { get; }

        void Contribute(ContentBuildGraphContributionContext context);
    }

    public sealed class ContentBuildGraphContributionContext
    {
        private readonly List<ContentScopeDefinition> _scopes = new();
        private readonly List<ContentAssetContribution> _assets = new();

        internal ContentBuildGraphContributionContext(string contributorId)
        {
            ContributorId = contributorId;
        }

        public string ContributorId { get; }

        internal IReadOnlyList<ContentScopeDefinition> Scopes => _scopes;

        internal IReadOnlyList<ContentAssetContribution> Assets => _assets;

        public void AddScope(ContentScopeDefinition scope)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            _scopes.Add(scope);
        }

        public void AddAsset(ContentAssetContribution asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            _assets.Add(asset);
        }
    }

    public sealed class ResolvedContentAsset
    {
        public ResolvedContentAsset(string assetId, string assetPath, string typeName, bool isValid = true)
        {
            AssetId = assetId ?? string.Empty;
            AssetPath = ContentBuildGraphCollections.NormalizePath(assetPath);
            TypeName = typeName ?? string.Empty;
            IsValid = isValid;
        }

        public string AssetId { get; }

        public string AssetPath { get; }

        public string TypeName { get; }

        public bool IsValid { get; }
    }

    public interface IContentAssetDependencyResolver
    {
        ResolvedContentAsset Resolve(string assetPath);

        IReadOnlyList<string> GetDirectDependencies(string assetPath);
    }

    public sealed class ContentBuildDiagnostic
    {
        public ContentBuildDiagnostic(
            string code,
            ContentDiagnosticSeverity severity,
            string message,
            string scopeId = null,
            string assetId = null,
            string suggestion = null)
        {
            Code = code ?? string.Empty;
            Severity = severity;
            Message = message ?? string.Empty;
            ScopeId = scopeId ?? string.Empty;
            AssetId = assetId ?? string.Empty;
            Suggestion = suggestion ?? string.Empty;
        }

        public string Code { get; }

        public ContentDiagnosticSeverity Severity { get; }

        public string Message { get; }

        public string ScopeId { get; }

        public string AssetId { get; }

        public string Suggestion { get; }
    }

    public sealed class ContentAssetNode
    {
        internal ContentAssetNode(
            string assetId,
            string assetPath,
            string address,
            IReadOnlyList<string> labels,
            string typeName,
            bool isExplicit,
            IReadOnlyList<string> explicitScopeIds,
            IReadOnlyList<string> usageScopeIds,
            ContentLocation location,
            ContentOwnership ownership,
            string ownerScopeId,
            string partitionId,
            bool isSharedCandidate,
            IReadOnlyList<string> packingHints)
        {
            AssetId = assetId;
            AssetPath = assetPath;
            Address = address;
            Labels = labels;
            TypeName = typeName;
            IsExplicit = isExplicit;
            ExplicitScopeIds = explicitScopeIds;
            UsageScopeIds = usageScopeIds;
            Location = location;
            Ownership = ownership;
            OwnerScopeId = ownerScopeId;
            PartitionId = partitionId;
            IsSharedCandidate = isSharedCandidate;
            PackingHints = packingHints;
        }

        public string AssetId { get; }

        public string AssetPath { get; }

        public string Address { get; }

        public IReadOnlyList<string> Labels { get; }

        public string TypeName { get; }

        public bool IsExplicit { get; }

        public IReadOnlyList<string> ExplicitScopeIds { get; }

        public IReadOnlyList<string> UsageScopeIds { get; }

        public ContentLocation Location { get; }

        public ContentOwnership Ownership { get; }

        public string OwnerScopeId { get; }

        public string PartitionId { get; }

        public bool IsSharedCandidate { get; }

        public IReadOnlyList<string> PackingHints { get; }
    }

    public sealed class ContentDependencyEdge
    {
        public ContentDependencyEdge(string sourceAssetId, string dependencyAssetId)
        {
            SourceAssetId = sourceAssetId ?? string.Empty;
            DependencyAssetId = dependencyAssetId ?? string.Empty;
        }

        public string SourceAssetId { get; }

        public string DependencyAssetId { get; }
    }

    public sealed class ContentBuildGraph
    {
        private readonly Dictionary<string, ContentAssetNode> _nodesById;
        private readonly Dictionary<string, string[]> _forwardEdges;
        private readonly Dictionary<string, string[]> _reverseEdges;

        internal ContentBuildGraph(
            IReadOnlyList<ContentScopeDefinition> scopes,
            IReadOnlyList<ContentAssetNode> assets,
            IReadOnlyList<ContentDependencyEdge> edges,
            IReadOnlyList<ContentBuildDiagnostic> diagnostics)
        {
            Scopes = Array.AsReadOnly(scopes.ToArray());
            Assets = Array.AsReadOnly(assets.ToArray());
            Edges = Array.AsReadOnly(edges.ToArray());
            Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            _nodesById = Assets.ToDictionary(node => node.AssetId, StringComparer.Ordinal);
            _forwardEdges = Edges
                .GroupBy(edge => edge.SourceAssetId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(edge => edge.DependencyAssetId)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    StringComparer.Ordinal);
            _reverseEdges = Edges
                .GroupBy(edge => edge.DependencyAssetId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(edge => edge.SourceAssetId)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    StringComparer.Ordinal);
            Fingerprint = ContentBuildGraphFingerprint.Compute(Scopes, Assets, Edges, Diagnostics);
        }

        public IReadOnlyList<ContentScopeDefinition> Scopes { get; }

        public IReadOnlyList<ContentAssetNode> Assets { get; }

        public IReadOnlyList<ContentDependencyEdge> Edges { get; }

        public IReadOnlyList<ContentBuildDiagnostic> Diagnostics { get; }

        public string Fingerprint { get; }

        public bool IsBuildable => Diagnostics.All(diagnostic =>
            diagnostic.Severity != ContentDiagnosticSeverity.Error);

        public IReadOnlyList<ContentAssetNode> GetForwardDependencyClosure(string scopeId)
        {
            if (string.IsNullOrEmpty(scopeId)) return Array.Empty<ContentAssetNode>();

            return Array.AsReadOnly(Assets
                .Where(node => node.UsageScopeIds.Contains(scopeId, StringComparer.Ordinal))
                .OrderBy(node => node.AssetId, StringComparer.Ordinal)
                .ToArray());
        }

        public IReadOnlyList<ContentAssetNode> GetReverseImpactClosure(string scopeId)
        {
            if (string.IsNullOrEmpty(scopeId)) return Array.Empty<ContentAssetNode>();

            var pending = new Queue<string>(Assets
                .Where(node => node.ExplicitScopeIds.Contains(scopeId, StringComparer.Ordinal))
                .Select(node => node.AssetId)
                .OrderBy(value => value, StringComparer.Ordinal));
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (pending.Count > 0)
            {
                var assetId = pending.Dequeue();
                if (!visited.Add(assetId) || !_reverseEdges.TryGetValue(assetId, out var parents))
                {
                    continue;
                }

                for (var i = 0; i < parents.Length; i++)
                {
                    pending.Enqueue(parents[i]);
                }
            }

            return Array.AsReadOnly(visited.Where(_nodesById.ContainsKey)
                .Select(assetId => _nodesById[assetId])
                .OrderBy(node => node.AssetId, StringComparer.Ordinal)
                .ToArray());
        }

        public IReadOnlyList<string> GetImpactedScopes(string scopeId)
        {
            return Array.AsReadOnly(GetReverseImpactClosure(scopeId)
                .SelectMany(node => node.UsageScopeIds)
                .Append(scopeId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
        }

        public IReadOnlyList<ContentAssetNode> GetDirectDependencies(string assetId)
        {
            if (string.IsNullOrEmpty(assetId) || !_forwardEdges.TryGetValue(assetId, out var dependencies))
            {
                return Array.Empty<ContentAssetNode>();
            }

            return Array.AsReadOnly(dependencies.Where(_nodesById.ContainsKey)
                .Select(dependencyId => _nodesById[dependencyId])
                .ToArray());
        }
    }

    public sealed class ContentBuildGraphContributorException : Exception
    {
        public ContentBuildGraphContributorException(string contributorId, Exception innerException)
            : base($"Content build graph contributor '{contributorId}' failed.", innerException)
        {
            ContributorId = contributorId;
        }

        public string ContributorId { get; }
    }

    internal static class ContentBuildGraphCollections
    {
        public static IReadOnlyList<string> CopyStrings(IEnumerable<string> values)
        {
            return Array.AsReadOnly((values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
        }

        public static IReadOnlyDictionary<string, string> CopyProperties(
            IReadOnlyDictionary<string, string> properties)
        {
            var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (properties != null)
            {
                foreach (var pair in properties)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key))
                    {
                        copy[pair.Key.Trim()] = pair.Value ?? string.Empty;
                    }
                }
            }

            return new ReadOnlyDictionary<string, string>(copy);
        }

        public static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').Trim();
        }
    }
}
