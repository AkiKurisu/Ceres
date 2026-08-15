using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Ceres.ContentPipeline
{
    /// <summary>
    /// Serializable description of one explicit AssetDatabase-backed content location.
    /// </summary>
    [Serializable]
    public sealed class ContentAssetDatabaseLocationRecord
    {
        public string assetId = string.Empty;
        public string assetGuid = string.Empty;
        public string assetPath = string.Empty;
        public string address = string.Empty;
        public string[] labels = Array.Empty<string>();
        public string typeName = string.Empty;
    }

    /// <summary>
    /// Serializable projection of the explicit Unity assets in a content build graph.
    /// </summary>
    [Serializable]
    public sealed class ContentAssetDatabaseLocationManifest
    {
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;
        public string graphFingerprint = string.Empty;
        public ContentAssetDatabaseLocationRecord[] locations =
            Array.Empty<ContentAssetDatabaseLocationRecord>();

        /// <summary>
        /// Projects a validated build graph without retaining the graph or its dependency nodes.
        /// </summary>
        public static ContentAssetDatabaseLocationManifest FromGraph(ContentBuildGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (!graph.IsBuildable)
            {
                var errors = graph.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == ContentDiagnosticSeverity.Error)
                    .Take(5)
                    .Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}");
                throw new InvalidOperationException(
                    "Cannot create an AssetDatabase location manifest from an invalid content build graph." +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, errors));
            }

            var records = graph.Assets
                .Where(node => node.IsExplicit &&
                               node.Ownership is not ContentOwnership.BuiltIn and
                               not ContentOwnership.Excluded)
                .OrderBy(node => node.AssetId, StringComparer.Ordinal)
                .Select(node => new ContentAssetDatabaseLocationRecord
                {
                    assetId = node.AssetId,
                    assetGuid = AssetDatabase.AssetPathToGUID(node.AssetPath),
                    assetPath = node.AssetPath,
                    address = node.Address,
                    labels = node.Labels.ToArray(),
                    typeName = node.TypeName
                })
                .ToArray();
            var manifest = new ContentAssetDatabaseLocationManifest
            {
                graphFingerprint = graph.Fingerprint,
                locations = records
            };
            manifest.Validate();
            return manifest;
        }

        /// <summary>
        /// Deserializes and validates a manifest produced by <see cref="ToJson"/>.
        /// </summary>
        public static ContentAssetDatabaseLocationManifest FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Location manifest JSON is empty.", nameof(json));

            var manifest = JsonUtility.FromJson<ContentAssetDatabaseLocationManifest>(json);
            if (manifest == null)
                throw new InvalidOperationException("Location manifest JSON could not be deserialized.");
            manifest.locations ??= Array.Empty<ContentAssetDatabaseLocationRecord>();
            for (var i = 0; i < manifest.locations.Length; i++)
            {
                if (manifest.locations[i] != null)
                {
                    manifest.locations[i].labels ??= Array.Empty<string>();
                }
            }

            manifest.Validate();
            return manifest;
        }

        /// <summary>
        /// Serializes this manifest after validating its required fields.
        /// </summary>
        public string ToJson(bool prettyPrint = true)
        {
            Validate();
            return JsonUtility.ToJson(this, prettyPrint);
        }

        /// <summary>
        /// Validates the manifest format and stable identity fields.
        /// </summary>
        public void Validate()
        {
            if (formatVersion != CurrentFormatVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported AssetDatabase location manifest format {formatVersion}; " +
                    $"expected {CurrentFormatVersion}.");
            }

            if (string.IsNullOrWhiteSpace(graphFingerprint))
                throw new InvalidOperationException(
                    "Location manifest has no graph fingerprint.");

            if (locations == null)
                throw new InvalidOperationException("Location manifest has no location array.");

            var assetIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < locations.Length; i++)
            {
                var record = locations[i] ??
                             throw new InvalidOperationException($"Location record {i} is null.");
                record.labels ??= Array.Empty<string>();
                if (string.IsNullOrWhiteSpace(record.assetId))
                    throw new InvalidOperationException($"Location record {i} has no asset ID.");
                if (!assetIds.Add(record.assetId))
                    throw new InvalidOperationException(
                        $"Location manifest contains duplicate asset ID '{record.assetId}'.");
                if (string.IsNullOrWhiteSpace(record.assetGuid))
                    throw new InvalidOperationException(
                        $"Location record '{record.assetId}' has no AssetDatabase GUID.");
                if (string.IsNullOrWhiteSpace(record.assetPath))
                    throw new InvalidOperationException(
                        $"Location record '{record.assetId}' has no AssetDatabase path.");
            }
        }
    }

    /// <summary>
    /// Mounts the explicit assets in a content build graph as Editor AssetDatabase locations.
    /// </summary>
    public sealed class ContentBuildGraphAssetDatabaseMount : IDisposable
    {
        private static AssetDatabaseProvider _sharedOwnedProvider;
        private static int _sharedOwnedProviderLeaseCount;

        private readonly ResourceLocationMap _locator;
        private readonly AssetDatabaseProvider _ownedProvider;
        private bool _disposed;

        private ContentBuildGraphAssetDatabaseMount(
            ResourceLocationMap locator,
            AssetDatabaseProvider ownedProvider,
            int locationCount)
        {
            _locator = locator;
            _ownedProvider = ownedProvider;
            LocationCount = locationCount;
        }

        public string LocatorId => _locator.LocatorId;

        public int LocationCount { get; }

        /// <summary>
        /// Creates an in-memory locator for the explicit graph assets. Addressables must already be initialized.
        /// </summary>
        public static ContentBuildGraphAssetDatabaseMount Create(
            ContentBuildGraph graph,
            string locatorId)
        {
            return Create(ContentAssetDatabaseLocationManifest.FromGraph(graph), locatorId);
        }

        /// <summary>
        /// Creates an in-memory locator from a previously projected location manifest.
        /// Addressables must already be initialized.
        /// </summary>
        public static ContentBuildGraphAssetDatabaseMount Create(
            ContentAssetDatabaseLocationManifest manifest,
            string locatorId)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (string.IsNullOrWhiteSpace(locatorId))
                throw new ArgumentException("A non-empty locator ID is required.", nameof(locatorId));
            manifest.Validate();

            RemoveStaleLocators(locatorId);
            var provider = EnsureAssetDatabaseProvider(out var ownsProvider);
            try
            {
                var records = manifest.locations
                    .OrderBy(record => record.assetId, StringComparer.Ordinal)
                    .ToArray();
                var locator = new ResourceLocationMap(locatorId, records.Length * 3);
                var keyOwners = new Dictionary<string, string>(StringComparer.Ordinal);
                var locationCount = 0;

                foreach (var record in records)
                {
                    ValidateRecord(record);
                    var resourceType = ResolveResourceType(record);
                    var isScene = resourceType == typeof(SceneAsset);
                    var runtimeType = isScene ? typeof(SceneInstance) : resourceType;
                    var providerId = isScene
                        ? typeof(SceneProvider).FullName
                        : typeof(AssetDatabaseProvider).FullName;
                    var primaryKey = string.IsNullOrEmpty(record.address)
                        ? record.assetId
                        : record.address;
                    var location = new ResourceLocationBase(
                        primaryKey,
                        record.assetPath,
                        providerId,
                        runtimeType);

                    AddUniqueKey(locator, keyOwners, record.address, record.assetPath, location);
                    AddUniqueKey(locator, keyOwners, record.assetId, record.assetPath, location);
                    for (var i = 0; i < record.labels.Length; i++)
                    {
                        var label = record.labels[i];
                        if (!string.IsNullOrEmpty(label))
                        {
                            locator.Add(label, location);
                        }
                    }

                    ValidateExistingNonLabelKey(record.address, record.assetPath, locatorId);
                    ValidateExistingNonLabelKey(record.assetId, record.assetPath, locatorId);
                    locationCount++;
                }

                Addressables.AddResourceLocator(locator);
                return new ContentBuildGraphAssetDatabaseMount(
                    locator,
                    ownsProvider ? provider : null,
                    locationCount);
            }
            catch
            {
                if (ownsProvider)
                {
                    ReleaseOwnedProvider(provider);
                }

                throw;
            }
        }

        private static void ValidateRecord(ContentAssetDatabaseLocationRecord record)
        {
            if (string.IsNullOrEmpty(record.assetId))
                throw new InvalidOperationException("An explicit graph asset has no stable asset ID.");
            var currentGuid = string.IsNullOrEmpty(record.assetPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(record.assetPath);
            if (string.IsNullOrEmpty(currentGuid))
            {
                throw new InvalidOperationException(
                    $"Explicit asset '{record.assetId}' has an invalid AssetDatabase path " +
                    $"'{record.assetPath}'.");
            }

            if (!string.Equals(currentGuid, record.assetGuid, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Explicit asset '{record.assetId}' expected GUID '{record.assetGuid}' at " +
                    $"'{record.assetPath}', but the current AssetDatabase GUID is '{currentGuid}'.");
            }
        }

        private static Type ResolveResourceType(ContentAssetDatabaseLocationRecord record)
        {
            var resourceType = string.IsNullOrEmpty(record.typeName)
                ? null
                : Type.GetType(record.typeName, false);
            resourceType ??= AssetDatabase.GetMainAssetTypeAtPath(record.assetPath);
            if (resourceType == null)
            {
                throw new InvalidOperationException(
                    $"Explicit asset '{record.assetId}' at '{record.assetPath}' has no resolvable resource type.");
            }

            return resourceType;
        }

        private static void AddUniqueKey(
            ResourceLocationMap locator,
            IDictionary<string, string> keyOwners,
            string key,
            string assetPath,
            IResourceLocation location)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (keyOwners.TryGetValue(key, out var existingPath) &&
                !PathsEqual(existingPath, assetPath))
            {
                throw new InvalidOperationException(
                    $"Content key '{key}' maps to both '{existingPath}' and '{assetPath}'.");
            }

            if (!keyOwners.ContainsKey(key))
            {
                keyOwners.Add(key, assetPath);
                locator.Add(key, location);
            }
        }

        private static void ValidateExistingNonLabelKey(
            string key,
            string assetPath,
            string locatorId)
        {
            if (string.IsNullOrEmpty(key)) return;
            foreach (var locator in Addressables.ResourceLocators)
            {
                if (locator == null ||
                    string.Equals(locator.LocatorId, locatorId, StringComparison.Ordinal) ||
                    !locator.Locate(key, null, out var locations) ||
                    locations == null)
                {
                    continue;
                }

                foreach (var location in locations)
                {
                    if (location != null && !PathsEqual(location.InternalId, assetPath))
                    {
                        throw new InvalidOperationException(
                            $"Content key '{key}' already resolves to '{location.InternalId}' in locator " +
                            $"'{locator.LocatorId}', and cannot also resolve to '{assetPath}'.");
                    }
                }
            }
        }

        private static AssetDatabaseProvider EnsureAssetDatabaseProvider(out bool ownsProvider)
        {
            var providers = Addressables.ResourceManager.ResourceProviders;
            var providerId = typeof(AssetDatabaseProvider).FullName;
            var existing = providers.FirstOrDefault(provider =>
                string.Equals(provider.ProviderId, providerId, StringComparison.Ordinal));
            if (existing is AssetDatabaseProvider assetDatabaseProvider)
            {
                ownsProvider = ReferenceEquals(assetDatabaseProvider, _sharedOwnedProvider);
                if (ownsProvider)
                {
                    _sharedOwnedProviderLeaseCount++;
                }
                return assetDatabaseProvider;
            }

            if (existing != null)
            {
                throw new InvalidOperationException(
                    $"Resource provider ID '{providerId}' is already owned by incompatible type " +
                    $"'{existing.GetType().FullName}'.");
            }

            var created = new AssetDatabaseProvider(0f);
            providers.Add(created);
            _sharedOwnedProvider = created;
            _sharedOwnedProviderLeaseCount = 1;
            ownsProvider = true;
            return created;
        }

        private static void ReleaseOwnedProvider(AssetDatabaseProvider provider)
        {
            if (!ReferenceEquals(provider, _sharedOwnedProvider)) return;
            _sharedOwnedProviderLeaseCount = Math.Max(0, _sharedOwnedProviderLeaseCount - 1);
            if (_sharedOwnedProviderLeaseCount != 0) return;

            Addressables.ResourceManager.ResourceProviders.Remove(provider);
            _sharedOwnedProvider = null;
        }

        private static void RemoveStaleLocators(string locatorId)
        {
            var staleLocators = Addressables.ResourceLocators
                .Where(locator => locator != null &&
                                  string.Equals(locator.LocatorId, locatorId, StringComparison.Ordinal))
                .ToArray();
            for (var i = 0; i < staleLocators.Length; i++)
            {
                Addressables.RemoveResourceLocator(staleLocators[i]);
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                (left ?? string.Empty).Replace('\\', '/'),
                (right ?? string.Empty).Replace('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Addressables.RemoveResourceLocator(_locator);
            if (_ownedProvider != null)
            {
                ReleaseOwnedProvider(_ownedProvider);
            }
        }
    }
}
