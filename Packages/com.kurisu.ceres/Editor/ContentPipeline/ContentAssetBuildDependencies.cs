using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using SbpContentPipeline = UnityEditor.Build.Pipeline.ContentPipeline;

namespace Ceres.ContentPipeline
{
    internal sealed class ContentAssetBuildDependencies
    {
        private readonly Dictionary<string, string> _hashes = new(StringComparer.Ordinal);
        private readonly Dictionary<GUID, string> _assetIds = new();

        public bool HasEntries => _hashes.Count > 0;

        public static ContentAssetBuildDependencies Create(
            ContentBuildGraph graph,
            IReadOnlyDictionary<string, string> hashes)
        {
            var dependencies = new ContentAssetBuildDependencies();
            if (hashes == null || hashes.Count == 0) return dependencies;

            var assets = graph.Assets.ToDictionary(node => node.AssetId, StringComparer.Ordinal);
            foreach (var pair in hashes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || !assets.TryGetValue(pair.Key, out var node))
                    throw new ArgumentException($"Unknown asset build dependency ID '{pair.Key}'.", nameof(hashes));
                if (string.IsNullOrWhiteSpace(pair.Value))
                    throw new ArgumentException($"Asset build dependency hash for '{pair.Key}' is empty.", nameof(hashes));

                var assetGuid = AssetDatabase.AssetPathToGUID(node.AssetPath);
                if (!GUID.TryParse(assetGuid, out var guid) || guid.Empty())
                    throw new ArgumentException($"Asset '{pair.Key}' has no Unity asset GUID.", nameof(hashes));

                dependencies._hashes.Add(pair.Key, pair.Value);
                dependencies._assetIds.Add(guid, pair.Key);
            }
            return dependencies;
        }

        public string ApplyToFingerprint(string assetId, string fingerprint)
        {
            return _hashes.TryGetValue(assetId, out var hash)
                ? ContentPipelineHash.Sha256(string.Join("\n", fingerprint, "asset-build-dependency", hash))
                : fingerprint;
        }

        public IDisposable OverridePostPackingCallback()
        {
            return _hashes.Count == 0 ? null : new PackingCallbackScope(this);
        }

        private void ApplyToWriteOperations(IWriteData writeData)
        {
            foreach (var operation in writeData.WriteOperations)
            {
                var assetIds = writeData.FileToObjects[operation.Command.internalName]
                    .Where(identifier => _assetIds.ContainsKey(identifier.guid))
                    .Select(identifier => _assetIds[identifier.guid])
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(assetId => assetId, StringComparer.Ordinal)
                    .ToArray();
                if (assetIds.Length == 0) continue;

                var hashes = assetIds
                    .Select(assetId => HashingMethods.Calculate(assetId, _hashes[assetId]).ToHash128())
                    .ToArray();
                operation.DependencyHash = HashingMethods.Calculate(operation.DependencyHash, hashes).ToHash128();
            }
        }

        private sealed class PackingCallbackScope : IDisposable
        {
            private readonly BuildCallbacks _callbacks;
            private readonly Func<IBuildParameters, IDependencyData, IWriteData, ReturnCode> _previous;
            private readonly ContentAssetBuildDependencies _dependencies;

            public PackingCallbackScope(ContentAssetBuildDependencies dependencies)
            {
                _dependencies = dependencies;
                _callbacks = SbpContentPipeline.BuildCallbacks;
                _previous = _callbacks.PostPackingCallback;
                _callbacks.PostPackingCallback = PostPacking;
            }

            private ReturnCode PostPacking(
                IBuildParameters parameters,
                IDependencyData dependencyData,
                IWriteData writeData)
            {
                var result = _previous?.Invoke(parameters, dependencyData, writeData) ?? ReturnCode.Success;
                if (result >= ReturnCode.Success) _dependencies.ApplyToWriteOperations(writeData);
                return result;
            }

            public void Dispose()
            {
                _callbacks.PostPackingCallback = _previous;
            }
        }
    }
}
