using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;

namespace Ceres.ContentPipeline
{
    internal sealed class ContentPackedBuild : BuildScriptPackedMode
    {
        private ContentBuildGraph _graph;
        private ContentBuildSnapshot _snapshot;
        private ContentArtifactManifest _baseline;
        private ContentBundlePartitionPlan _partitions;

        internal void ConfigureUpdate(
            ContentBuildGraph graph,
            ContentBuildSnapshot snapshot,
            ContentArtifactManifest baseline,
            ContentBundlePartitionPlan partitions)
        {
            _graph = graph;
            _snapshot = snapshot;
            _baseline = baseline;
            _partitions = partitions;
        }

        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput)
        {
            var previous = builderInput.PreviousContentState;
            try
            {
                if (previous != null && _baseline != null)
                    builderInput.PreviousContentState = ExcludeChangedState(previous);
                return base.BuildDataImplementation<TResult>(builderInput);
            }
            finally
            {
                builderInput.PreviousContentState = previous;
            }
        }

        private AddressablesContentState ExcludeChangedState(AddressablesContentState previous)
        {
            var baselineAssets = _baseline.assets.ToDictionary(asset => asset.id, StringComparer.Ordinal);
            var currentAssets = _snapshot.Assets.ToDictionary(asset => asset.id, StringComparer.Ordinal);
            var changed = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in baselineAssets.Keys.Union(currentAssets.Keys, StringComparer.Ordinal))
            {
                if (!baselineAssets.TryGetValue(id, out var oldAsset) ||
                    !currentAssets.TryGetValue(id, out var currentAsset) ||
                    !string.Equals(oldAsset.fingerprint, currentAsset.fingerprint, StringComparison.Ordinal))
                    changed.Add(id);
            }
            if (changed.Count == 0) return previous;

            var reverse = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var edge in _graph.Edges) AddReverse(reverse, edge.DependencyAssetId, edge.SourceAssetId);
            foreach (var asset in previous.cachedInfos)
            {
                foreach (var dependency in asset.dependencies)
                    AddReverse(reverse, dependency.guid.ToString(), asset.asset.guid.ToString());
            }

            foreach (var bundle in previous.cachedInfos.GroupBy(asset => asset.bundleFileId, StringComparer.Ordinal))
                ConnectPeers(reverse, bundle.Select(asset => asset.asset.guid.ToString()));
            foreach (var partition in _partitions.Partitions)
                ConnectPeers(reverse, partition.Nodes.Select(node => node.AssetId));

            var pending = new Queue<string>(changed.OrderBy(id => id, StringComparer.Ordinal));
            while (pending.Count > 0)
            {
                if (!reverse.TryGetValue(pending.Dequeue(), out var dependents)) continue;
                foreach (string dependent in dependents)
                    if (changed.Add(dependent)) pending.Enqueue(dependent);
            }

            var retained = previous.cachedInfos.Where(asset => !changed.Contains(asset.asset.guid.ToString())).ToArray();
            if (retained.Length == previous.cachedInfos.Length) return previous;
            // Addressables otherwise reverts freshly written bundles using Unity's asset hash alone.
            return new AddressablesContentState
            {
                playerVersion = previous.playerVersion,
                editorVersion = previous.editorVersion,
                remoteCatalogLoadPath = previous.remoteCatalogLoadPath,
                cachedBundles = previous.cachedBundles,
                cachedInfos = retained
            };
        }

        private static void ConnectPeers(Dictionary<string, HashSet<string>> reverse, IEnumerable<string> ids)
        {
            string first = null;
            foreach (string id in ids)
            {
                if (first == null) first = id;
                else
                {
                    AddReverse(reverse, first, id);
                    AddReverse(reverse, id, first);
                }
            }
        }

        private static void AddReverse(Dictionary<string, HashSet<string>> reverse, string dependency, string asset)
        {
            if (!reverse.TryGetValue(dependency, out var dependents))
            {
                dependents = new HashSet<string>(StringComparer.Ordinal);
                reverse.Add(dependency, dependents);
            }
            dependents.Add(asset);
        }
    }
}
