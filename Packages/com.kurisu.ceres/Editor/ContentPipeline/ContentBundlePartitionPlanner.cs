using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ceres.ContentPipeline
{
    internal sealed class ContentBundleEstimatedContent
    {
        private readonly Dictionary<string, long> _pathSizes =
            new(StringComparer.OrdinalIgnoreCase);

        public long Bytes { get; private set; }

        public long GetOverlapBytes(ContentBundleEstimatedContent other)
        {
            if (other == null) return 0;
            long total = 0;
            foreach (var pair in other._pathSizes)
            {
                if (!_pathSizes.ContainsKey(pair.Key)) continue;
                total = AddSaturating(total, pair.Value);
            }

            return total;
        }

        public long GetIncrementalBytes(ContentBundleEstimatedContent other)
        {
            if (other == null) return 0;
            long total = 0;
            foreach (var pair in other._pathSizes)
            {
                if (_pathSizes.ContainsKey(pair.Key)) continue;
                total = AddSaturating(total, pair.Value);
            }

            return total;
        }

        public void UnionWith(ContentBundleEstimatedContent other)
        {
            if (other == null) return;
            foreach (var pair in other._pathSizes)
            {
                Add(pair.Key, pair.Value);
            }
        }

        public void Add(string path, long size)
        {
            if (string.IsNullOrWhiteSpace(path) || _pathSizes.ContainsKey(path)) return;
            var normalizedSize = Math.Max(0, size);
            _pathSizes.Add(path, normalizedSize);
            Bytes = AddSaturating(Bytes, normalizedSize);
        }

        private static long AddSaturating(long left, long right)
        {
            return left > long.MaxValue - right ? long.MaxValue : left + right;
        }
    }

    internal sealed class ContentBundlePartitionPlan
    {
        public ContentBundlePartitionPlan(
            ContentBundlePackingOptions options,
            IReadOnlyList<ContentBundlePartition> partitions)
        {
            Options = options;
            Partitions = partitions;
            EstimatedPartitionSourceBytes = partitions.Aggregate(
                0L,
                (total, partition) => AddSaturating(total, partition.EstimatedBytes));
            var unique = new ContentBundleEstimatedContent();
            foreach (var partition in partitions)
            {
                unique.UnionWith(partition.EstimatedContent);
            }

            EstimatedUniqueSourceBytes = unique.Bytes;
            EstimatedCrossPartitionDuplicateBytes = Math.Max(
                0,
                EstimatedPartitionSourceBytes - EstimatedUniqueSourceBytes);
        }

        public ContentBundlePackingOptions Options { get; }

        public IReadOnlyList<ContentBundlePartition> Partitions { get; }

        public long EstimatedUniqueSourceBytes { get; }

        public long EstimatedPartitionSourceBytes { get; }

        public long EstimatedCrossPartitionDuplicateBytes { get; }

        private static long AddSaturating(long left, long right)
        {
            return left > long.MaxValue - right ? long.MaxValue : left + right;
        }
    }

    internal sealed class ContentBundlePartition
    {
        public ContentBundlePartition(
            string id,
            string family,
            ContentLocation location,
            bool containsScenes,
            ContentBundleEstimatedContent estimatedContent,
            long previousActualBytes,
            IReadOnlyList<ContentAssetNode> nodes)
        {
            Id = id;
            Family = family;
            Location = location;
            ContainsScenes = containsScenes;
            EstimatedContent = estimatedContent ?? new ContentBundleEstimatedContent();
            PreviousActualBytes = previousActualBytes;
            Nodes = nodes;
            SourceScopeIds = nodes
                .SelectMany(node => node.UsageScopeIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public string Id { get; }

        public string Family { get; }

        public ContentLocation Location { get; }

        public bool ContainsScenes { get; }

        public long EstimatedBytes => EstimatedContent.Bytes;

        public ContentBundleEstimatedContent EstimatedContent { get; }

        public long PreviousActualBytes { get; }

        public IReadOnlyList<ContentAssetNode> Nodes { get; }

        public IReadOnlyList<string> SourceScopeIds { get; }
    }

    internal static class ContentBundlePartitionPlanner
    {
        public const string ClassifierVersion = "semantic-families-v1";
        private const long MinimumTargetBytes = 32L * 1024L * 1024L;
        private const long MaximumTargetBytes = 512L * 1024L * 1024L;

        public static ContentBundlePackingOptions Normalize(ContentBundlePackingOptions source)
        {
            source ??= new ContentBundlePackingOptions();
            var algorithm = source.Mode == ContentBundlePackingMode.LogicalPartitions
                ? ContentBundlePackingOptions.LogicalAlgorithmVersion
                : string.IsNullOrWhiteSpace(source.AlgorithmVersion)
                ? ContentBundlePackingOptions.DefaultAlgorithmVersion
                : source.AlgorithmVersion.Trim();
            var target = source.TargetBundleSizeBytes <= 0
                ? ContentBundlePackingOptions.DefaultTargetBundleSizeBytes
                : source.TargetBundleSizeBytes;
            if (source.Mode == ContentBundlePackingMode.SizeOptimized &&
                (target < MinimumTargetBytes || target > MaximumTargetBytes))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(source.TargetBundleSizeBytes),
                    target,
                    "Size-optimized bundle target must be between 32 MiB and 512 MiB.");
            }

            if (source.Mode == ContentBundlePackingMode.SizeOptimized &&
                !string.Equals(
                    algorithm,
                    ContentBundlePackingOptions.DefaultAlgorithmVersion,
                    StringComparison.Ordinal))
            {
                throw new NotSupportedException(
                    $"Unsupported size-optimized packing algorithm '{algorithm}'.");
            }

            return new ContentBundlePackingOptions
            {
                Mode = source.Mode,
                TargetBundleSizeBytes = target,
                AlgorithmVersion = algorithm
            };
        }

        public static ContentBundlePartitionPlan Plan(
            ContentBuildGraph graph,
            ContentBundlePackingOptions sourceOptions,
            ContentArtifactManifest previousManifest)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var options = Normalize(sourceOptions);
            var nodes = graph.Assets
                .Where(ShouldCreateExplicitEntry)
                .Where(node => !string.IsNullOrEmpty(node.AssetPath))
                .OrderBy(node => node.AssetId, StringComparer.Ordinal)
                .ToArray();
            if (options.Mode == ContentBundlePackingMode.LogicalPartitions)
            {
                return new ContentBundlePartitionPlan(options, PlanLogical(graph, nodes));
            }

            ValidatePreviousPlan(previousManifest, options);
            return new ContentBundlePartitionPlan(
                options,
                PlanSizeOptimized(graph, nodes, options, previousManifest));
        }

        private static IReadOnlyList<ContentBundlePartition> PlanLogical(
            ContentBuildGraph graph,
            IReadOnlyCollection<ContentAssetNode> nodes)
        {
            var pathSizeCache = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var scopeLocations = graph.Scopes.ToDictionary(
                scope => scope.Id,
                scope => scope.DefaultLocation,
                StringComparer.Ordinal);
            return nodes
                .Select(node => new LogicalCandidate(
                    node,
                    ResolveLocation(new[] { node }, scopeLocations)))
                .GroupBy(candidate => new LogicalPartitionKey(
                    candidate.Location,
                    GetLogicalPartitionId(candidate.Node)))
                .OrderBy(group => group.Key.StableId, StringComparer.Ordinal)
                .Select(group =>
                {
                    var members = group
                        .Select(candidate => candidate.Node)
                        .OrderBy(node => node.AssetId, StringComparer.Ordinal)
                        .ToArray();
                    var first = members[0];
                    return new ContentBundlePartition(
                        group.Key.StableId,
                        GetSemanticFamily(first),
                        group.Key.Location,
                        members.Any(IsScene),
                        EstimatePartitionContent(graph, members, pathSizeCache),
                        0,
                        members);
                })
                .ToArray();
        }

        private static IReadOnlyList<ContentBundlePartition> PlanSizeOptimized(
            ContentBuildGraph graph,
            IReadOnlyCollection<ContentAssetNode> nodes,
            ContentBundlePackingOptions options,
            ContentArtifactManifest previousManifest)
        {
            var pathSizeCache = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var scopeLocations = graph.Scopes.ToDictionary(
                scope => scope.Id,
                scope => scope.DefaultLocation,
                StringComparer.Ordinal);
            var candidateArray = nodes
                .Select(node => new Candidate(
                    node,
                    GetSemanticFamily(node),
                    ResolveLocation(new[] { node }, scopeLocations),
                    IsScene(node),
                    EstimateNodeContent(graph, node, pathSizeCache)))
                .ToArray();
            ValidateStableFamilyAssignments(graph, candidateArray, previousManifest);
            var candidates = candidateArray
                .GroupBy(
                    candidate => new FamilyKey(
                        candidate.Location,
                        candidate.Family,
                        candidate.ContainsScenes))
                .OrderBy(group => group.Key.StableKey, StringComparer.Ordinal);
            var previousByFamily = (previousManifest?.partitions ?? Array.Empty<ContentBundlePartitionSnapshot>())
                .GroupBy(
                    partition => new FamilyKey(
                        ParseLocation(partition.location),
                        partition.family,
                        partition.containsScenes))
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(partition => partition.id, StringComparer.Ordinal).ToArray());
            var output = new List<ContentBundlePartition>();
            foreach (var family in candidates)
            {
                previousByFamily.TryGetValue(family.Key, out var previous);
                var buckets = family.Key.ContainsScenes
                    ? PlanSceneFamily(family.Key, family, previous, options)
                    : family.Key.Family == "info"
                    ? PlanMetadataFamily(family.Key, family, previous, options)
                    : previous is { Length: > 0 }
                        ? PlanUpdatedFamily(family.Key, family, previous, options)
                        : PlanBaselineFamily(family.Key, family, options);
                output.AddRange(buckets.Select(bucket => bucket.ToPartition()));
            }

            return output.OrderBy(partition => partition.Id, StringComparer.Ordinal).ToArray();
        }

        private static void ValidateStableFamilyAssignments(
            ContentBuildGraph graph,
            IReadOnlyCollection<Candidate> candidates,
            ContentArtifactManifest previousManifest)
        {
            if (previousManifest == null) return;
            var previousFamilies = new Dictionary<string, FamilyKey>(StringComparer.Ordinal);
            foreach (var partition in previousManifest.partitions)
            {
                var key = new FamilyKey(
                    ParseLocation(partition.location),
                    partition.family,
                    partition.containsScenes);
                foreach (var assetId in partition.assetIds)
                {
                    previousFamilies.Add(assetId, key);
                }
            }

            foreach (var candidate in candidates)
            {
                if (!previousFamilies.TryGetValue(candidate.Node.AssetId, out var previous)) continue;
                var current = new FamilyKey(
                    candidate.Location,
                    candidate.Family,
                    candidate.ContainsScenes);
                if (previous.Equals(current)) continue;
                throw new InvalidOperationException(
                    $"Asset '{candidate.Node.AssetId}' changed bundle family from " +
                    $"'{previous.StableKey}' to '{current.StableKey}'. Build a new baseline.");
            }

            var currentAssetIds = new HashSet<string>(
                graph.Assets.Select(asset => asset.AssetId),
                StringComparer.Ordinal);
            var currentCandidateIds = new HashSet<string>(
                candidates.Select(candidate => candidate.Node.AssetId),
                StringComparer.Ordinal);
            foreach (var previousAssetId in previousFamilies.Keys)
            {
                if (!currentAssetIds.Contains(previousAssetId) ||
                    currentCandidateIds.Contains(previousAssetId))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Asset '{previousAssetId}' changed bundle packing eligibility. " +
                    "Build a new baseline.");
            }
        }

        private static IReadOnlyList<Bucket> PlanSceneFamily(
            FamilyKey key,
            IEnumerable<Candidate> source,
            IReadOnlyList<ContentBundlePartitionSnapshot> previous,
            ContentBundlePackingOptions options)
        {
            var previousIds = (previous ?? Array.Empty<ContentBundlePartitionSnapshot>())
                .SelectMany(partition => (partition.assetIds ?? Array.Empty<string>())
                    .Select(assetId => new { assetId, partition.id, partition.actualBytes }))
                .ToDictionary(value => value.assetId, StringComparer.Ordinal);
            return source.OrderBy(candidate => candidate.Node.AssetId, StringComparer.Ordinal)
                .Select(candidate =>
                {
                    var hasPrevious = previousIds.TryGetValue(candidate.Node.AssetId, out var snapshot);
                    var id = hasPrevious
                        ? snapshot.id
                        : CreateStablePartitionId(
                            key,
                            $"scene|{candidate.Node.AssetId}",
                            options.AlgorithmVersion);
                    return new Bucket(
                        id,
                        key,
                        new[] { candidate },
                        hasPrevious ? snapshot.actualBytes : 0);
                })
                .ToArray();
        }

        private static IReadOnlyList<Bucket> PlanMetadataFamily(
            FamilyKey key,
            IEnumerable<Candidate> source,
            IReadOnlyList<ContentBundlePartitionSnapshot> previous,
            ContentBundlePackingOptions options)
        {
            var members = source.OrderBy(candidate => candidate.Node.AssetId, StringComparer.Ordinal).ToArray();
            if (members.Length == 0) return Array.Empty<Bucket>();
            var id = previous?.FirstOrDefault()?.id;
            if (string.IsNullOrWhiteSpace(id))
            {
                id = CreateStablePartitionId(key, "all-metadata", options.AlgorithmVersion);
            }

            return new[] { new Bucket(id, key, members, PreviousActualBytes(previous, id)) };
        }

        private static IReadOnlyList<Bucket> PlanBaselineFamily(
            FamilyKey key,
            IEnumerable<Candidate> source,
            ContentBundlePackingOptions options)
        {
            var buckets = new List<MutableBucket>();
            foreach (var candidate in OrderForPacking(source))
            {
                var bucket = SelectBestBucket(buckets, candidate, options.TargetBundleSizeBytes);
                if (bucket == null)
                {
                    bucket = new MutableBucket(key);
                    buckets.Add(bucket);
                }

                bucket.Add(candidate);
            }

            return buckets.Select(value =>
            {
                var identity = string.Join(
                    "|",
                    value.Members.Select(member => member.Node.AssetId).OrderBy(id => id, StringComparer.Ordinal));
                return new Bucket(
                    CreateStablePartitionId(key, identity, options.AlgorithmVersion),
                    key,
                    value.Members,
                    0);
            }).ToArray();
        }

        private static IReadOnlyList<Bucket> PlanUpdatedFamily(
            FamilyKey key,
            IEnumerable<Candidate> source,
            IReadOnlyList<ContentBundlePartitionSnapshot> previous,
            ContentBundlePackingOptions options)
        {
            var remaining = source.ToDictionary(candidate => candidate.Node.AssetId, StringComparer.Ordinal);
            var buckets = new List<MutableBucket>();
            foreach (var snapshot in previous)
            {
                var bucket = new MutableBucket(key, snapshot.id, snapshot.actualBytes);
                foreach (var assetId in snapshot.assetIds ?? Array.Empty<string>())
                {
                    if (!remaining.Remove(assetId, out var candidate)) continue;
                    bucket.Add(candidate);
                }

                if (bucket.Members.Count > 0) buckets.Add(bucket);
            }

            foreach (var candidate in OrderForPacking(remaining.Values))
            {
                var bucket = SelectBestBucket(buckets, candidate, options.TargetBundleSizeBytes);
                if (bucket == null)
                {
                    bucket = new MutableBucket(
                        key,
                        CreateStablePartitionId(
                            key,
                            $"overflow|{candidate.Node.AssetId}",
                            options.AlgorithmVersion),
                        0);
                    buckets.Add(bucket);
                }

                bucket.Add(candidate);
            }

            return buckets.Select(value => new Bucket(
                    value.Id,
                    key,
                    value.Members,
                    value.PreviousActualBytes))
                .ToArray();
        }

        private static IEnumerable<Candidate> OrderForPacking(IEnumerable<Candidate> candidates)
        {
            return candidates
                .OrderByDescending(candidate => candidate.EstimatedBytes)
                .ThenBy(candidate => candidate.Node.AssetId, StringComparer.Ordinal);
        }

        private static MutableBucket SelectBestBucket(
            IReadOnlyList<MutableBucket> buckets,
            Candidate candidate,
            long target)
        {
            return buckets
                .Select((bucket, ordinal) => new
                {
                    Bucket = bucket,
                    Ordinal = ordinal,
                    IncrementalBytes = bucket.GetIncrementalBytes(candidate),
                    OverlapBytes = bucket.GetOverlapBytes(candidate)
                })
                .Where(value => value.Bucket.CanFit(value.IncrementalBytes, target))
                .OrderByDescending(value => value.OverlapBytes)
                .ThenBy(value => RemainingBytes(
                    value.Bucket.EstimatedBytes,
                    value.IncrementalBytes,
                    target))
                .ThenBy(value => value.Bucket.Id ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(value => value.Ordinal)
                .Select(value => value.Bucket)
                .FirstOrDefault();
        }

        private static long RemainingBytes(long occupied, long incremental, long target)
        {
            if (occupied >= target || incremental >= target - occupied) return 0;
            return target - occupied - incremental;
        }

        private static ContentBundleEstimatedContent EstimatePartitionContent(
            ContentBuildGraph graph,
            IReadOnlyCollection<ContentAssetNode> nodes,
            IDictionary<string, long> pathSizeCache)
        {
            var content = new ContentBundleEstimatedContent();
            foreach (var node in nodes)
            {
                content.UnionWith(EstimateNodeContent(graph, node, pathSizeCache));
            }

            return content;
        }

        private static ContentBundleEstimatedContent EstimateNodeContent(
            ContentBuildGraph graph,
            ContentAssetNode node,
            IDictionary<string, long> pathSizeCache)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectEstimatedPaths(graph, node, paths);
            var content = new ContentBundleEstimatedContent();
            foreach (var path in paths.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                if (!pathSizeCache.TryGetValue(path, out var size))
                {
                    size = ContentPipelineFileSystem.FileExists(path)
                        ? ContentPipelineFileSystem.GetFileLength(path)
                        : 0;
                    pathSizeCache[path] = size;
                }

                content.Add(path, size);
            }

            return content;
        }

        private static void CollectEstimatedPaths(
            ContentBuildGraph graph,
            ContentAssetNode root,
            ISet<string> paths)
        {
            var pending = new Queue<ContentAssetNode>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            pending.Enqueue(root);
            while (pending.Count > 0)
            {
                var node = pending.Dequeue();
                if (!visited.Add(node.AssetId)) continue;
                if (!string.IsNullOrWhiteSpace(node.AssetPath) &&
                    node.Ownership is not ContentOwnership.BuiltIn and not ContentOwnership.Excluded)
                {
                    paths.Add(node.AssetPath);
                }

                foreach (var dependency in graph.GetDirectDependencies(node.AssetId))
                {
                    if (dependency.Ownership is ContentOwnership.BuiltIn or ContentOwnership.Excluded)
                        continue;
                    if (IsInfrastructureNode(dependency))
                        continue;
                    if (!string.Equals(dependency.AssetId, root.AssetId, StringComparison.Ordinal) &&
                        ShouldCreateExplicitEntry(dependency))
                        continue;
                    pending.Enqueue(dependency);
                }
            }
        }

        internal static bool ShouldCreateExplicitEntry(ContentAssetNode node)
        {
            if (node.Ownership is ContentOwnership.BuiltIn or ContentOwnership.Excluded) return false;
            if (IsInfrastructureNode(node)) return false;
            return node.IsExplicit || node.Ownership == ContentOwnership.Shared;
        }

        private static bool IsInfrastructureNode(ContentAssetNode node)
        {
            return (node.TypeName?.EndsWith(".MonoScript", StringComparison.Ordinal) ?? false) ||
                   (node.AssetPath?.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ?? false) ||
                   string.Equals(
                       node.AssetPath,
                       "Resources/unity_builtin_extra",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string GetLogicalPartitionId(ContentAssetNode node)
        {
            if (node.Ownership == ContentOwnership.Shared)
            {
                return $"shared:{GetTypeFamily(node)}";
            }

            var packing = node.PackingHints.FirstOrDefault() ?? "content";
            return $"{node.PartitionId}:{packing}";
        }

        private static string GetSemanticFamily(ContentAssetNode node)
        {
            if (node.Ownership == ContentOwnership.Shared)
            {
                return $"shared-{GetTypeFamily(node)}";
            }

            var hint = node.PackingHints.FirstOrDefault();
            return string.IsNullOrWhiteSpace(hint) ? "content" : hint.Trim().ToLowerInvariant();
        }

        private static string GetTypeFamily(ContentAssetNode node)
        {
            var value = $"{node.TypeName}|{Path.GetExtension(node.AssetPath)}".ToLowerInvariant();
            if (value.Contains("shader")) return "shaders";
            if (value.Contains("material") || value.EndsWith("|.mat")) return "materials";
            if (value.Contains("texture") || value.Contains("|.png") || value.Contains("|.jpg") ||
                value.Contains("|.tga")) return "textures";
            if (value.Contains("animation") || value.Contains("|.anim") || value.Contains("|.controller"))
                return "animations";
            if (value.Contains("gameobject") || value.Contains("|.prefab")) return "prefabs";
            return "other";
        }

        private static bool IsScene(ContentAssetNode node)
        {
            return node.AssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                   node.TypeName.EndsWith(".SceneAsset", StringComparison.Ordinal);
        }

        private static ContentLocation ResolveLocation(
            IReadOnlyCollection<ContentAssetNode> nodes,
            IReadOnlyDictionary<string, ContentLocation> scopeLocations)
        {
            if (nodes.Any(node => node.Location == ContentLocation.Local)) return ContentLocation.Local;
            if (nodes.Any(node => node.Location == ContentLocation.Remote)) return ContentLocation.Remote;
            return nodes.SelectMany(node => node.UsageScopeIds)
                .Where(scopeLocations.ContainsKey)
                .Select(scopeId => scopeLocations[scopeId])
                .Contains(ContentLocation.Local)
                ? ContentLocation.Local
                : ContentLocation.Remote;
        }

        private static ContentLocation ParseLocation(string value)
        {
            return Enum.TryParse(value, out ContentLocation location) ? location : ContentLocation.Remote;
        }

        private static string CreateStablePartitionId(
            FamilyKey key,
            string identity,
            string algorithmVersion)
        {
            var seed = string.Join(
                "|",
                algorithmVersion,
                ClassifierVersion,
                key.StableKey,
                identity);
            return $"optimized:{ContentPipelineHash.Short(seed, 16)}:{key.Family}";
        }

        private static long PreviousActualBytes(
            IReadOnlyList<ContentBundlePartitionSnapshot> snapshots,
            string id)
        {
            return snapshots?.FirstOrDefault(snapshot =>
                string.Equals(snapshot.id, id, StringComparison.Ordinal))?.actualBytes ?? 0;
        }

        private static void ValidatePreviousPlan(
            ContentArtifactManifest previous,
            ContentBundlePackingOptions options)
        {
            if (previous == null) return;
            if (previous.partitions == null)
            {
                throw new InvalidDataException(
                    "The previous bundle packing plan has no partition list.");
            }
            if (!Enum.TryParse(previous.packing?.mode, out ContentBundlePackingMode mode) ||
                !Enum.IsDefined(typeof(ContentBundlePackingMode), mode) ||
                mode != options.Mode ||
                mode == ContentBundlePackingMode.SizeOptimized &&
                previous.packing.targetBundleSizeBytes != options.TargetBundleSizeBytes ||
                !string.Equals(
                    previous.packing.algorithmVersion,
                    options.AlgorithmVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previous.packing.classifierVersion,
                    ClassifierVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The previous bundle packing plan is incompatible; build a new baseline.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var assets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var partition in previous.partitions)
            {
                if (partition == null ||
                    string.IsNullOrWhiteSpace(partition.id) ||
                    !ids.Add(partition.id))
                {
                    throw new InvalidDataException(
                        "The previous bundle packing plan contains an empty or duplicate partition ID.");
                }

                if (string.IsNullOrWhiteSpace(partition.family))
                {
                    throw new InvalidDataException(
                        $"Packing partition '{partition.id}' has no semantic family.");
                }
                if (!Enum.TryParse(
                        partition.location,
                        false,
                        out ContentLocation location) ||
                    !Enum.IsDefined(typeof(ContentLocation), location) ||
                    partition.estimatedBytes < 0 ||
                    partition.actualBytes < 0 ||
                    partition.assetIds == null ||
                    partition.sourceScopes == null)
                {
                    throw new InvalidDataException(
                        $"Packing partition '{partition.id}' contains invalid state.");
                }

                foreach (var assetId in partition.assetIds)
                {
                    if (string.IsNullOrWhiteSpace(assetId) || !assets.Add(assetId))
                    {
                        throw new InvalidDataException(
                            $"Packing partition '{partition.id}' contains an empty or duplicate asset ID.");
                    }
                }
            }
        }

        private readonly struct FamilyKey : IEquatable<FamilyKey>
        {
            public FamilyKey(ContentLocation location, string family, bool containsScenes)
            {
                Location = location;
                Family = family ?? "content";
                ContainsScenes = containsScenes;
            }

            public ContentLocation Location { get; }

            public string Family { get; }

            public bool ContainsScenes { get; }

            public string StableKey => $"{Location}:{(ContainsScenes ? "scene" : "asset")}:{Family}";

            public bool Equals(FamilyKey other)
            {
                return Location == other.Location &&
                       ContainsScenes == other.ContainsScenes &&
                       string.Equals(Family, other.Family, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is FamilyKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = (int)Location;
                    hash = (hash * 397) ^ ContainsScenes.GetHashCode();
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Family);
                    return hash;
                }
            }
        }

        private readonly struct LogicalPartitionKey : IEquatable<LogicalPartitionKey>
        {
            public LogicalPartitionKey(ContentLocation location, string partitionId)
            {
                Location = location;
                PartitionId = partitionId ?? string.Empty;
            }

            public ContentLocation Location { get; }

            public string PartitionId { get; }

            public string StableId => $"{Location}:{PartitionId}";

            public bool Equals(LogicalPartitionKey other)
            {
                return Location == other.Location &&
                       string.Equals(PartitionId, other.PartitionId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is LogicalPartitionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)Location * 397) ^
                           StringComparer.Ordinal.GetHashCode(PartitionId);
                }
            }
        }

        private readonly struct LogicalCandidate
        {
            public LogicalCandidate(ContentAssetNode node, ContentLocation location)
            {
                Node = node;
                Location = location;
            }

            public ContentAssetNode Node { get; }

            public ContentLocation Location { get; }
        }

        private sealed class Candidate
        {
            public Candidate(
                ContentAssetNode node,
                string family,
                ContentLocation location,
                bool containsScenes,
                ContentBundleEstimatedContent estimatedContent)
            {
                Node = node;
                Family = family;
                Location = location;
                ContainsScenes = containsScenes;
                EstimatedContent = estimatedContent ?? new ContentBundleEstimatedContent();
            }

            public ContentAssetNode Node { get; }

            public string Family { get; }

            public ContentLocation Location { get; }

            public bool ContainsScenes { get; }

            public long EstimatedBytes => EstimatedContent.Bytes;

            public ContentBundleEstimatedContent EstimatedContent { get; }
        }

        private sealed class MutableBucket
        {
            public MutableBucket(FamilyKey key, string id = null, long previousActualBytes = 0)
            {
                Key = key;
                Id = id;
                PreviousActualBytes = previousActualBytes;
            }

            public FamilyKey Key { get; }

            public string Id { get; }

            public long PreviousActualBytes { get; }

            public List<Candidate> Members { get; } = new();

            public ContentBundleEstimatedContent EstimatedContent { get; } = new();

            public long EstimatedBytes => EstimatedContent.Bytes;

            public long GetOverlapBytes(Candidate candidate)
            {
                return EstimatedContent.GetOverlapBytes(candidate.EstimatedContent);
            }

            public long GetIncrementalBytes(Candidate candidate)
            {
                return EstimatedContent.GetIncrementalBytes(candidate.EstimatedContent);
            }

            public bool CanFit(long incrementalBytes, long target)
            {
                return Members.Count == 0 ||
                       incrementalBytes <= target - Math.Min(target, EstimatedBytes);
            }

            public void Add(Candidate candidate)
            {
                Members.Add(candidate);
                EstimatedContent.UnionWith(candidate.EstimatedContent);
            }
        }

        private sealed class Bucket
        {
            public Bucket(
                string id,
                FamilyKey key,
                IEnumerable<Candidate> members,
                long previousActualBytes)
            {
                Id = id;
                Key = key;
                Members = members.OrderBy(member => member.Node.AssetId, StringComparer.Ordinal).ToArray();
                PreviousActualBytes = previousActualBytes;
                EstimatedContent = new ContentBundleEstimatedContent();
                foreach (var member in Members)
                {
                    EstimatedContent.UnionWith(member.EstimatedContent);
                }
            }

            public string Id { get; }

            public FamilyKey Key { get; }

            public Candidate[] Members { get; }

            public long PreviousActualBytes { get; }

            public ContentBundleEstimatedContent EstimatedContent { get; }

            public ContentBundlePartition ToPartition()
            {
                return new ContentBundlePartition(
                    Id,
                    Key.Family,
                    Key.Location,
                    Key.ContainsScenes,
                    EstimatedContent,
                    PreviousActualBytes,
                    Members.Select(member => member.Node).ToArray());
            }
        }
    }
}
