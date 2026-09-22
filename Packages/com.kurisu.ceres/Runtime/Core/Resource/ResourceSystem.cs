using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ceres.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
using System.Reflection;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;
#else
using System.Text;
#endif
using UObject = UnityEngine.Object;

namespace Ceres.Resource
{
    /// <summary>
    /// Exception thrown when request resource address is invalid
    /// </summary>
    public class InvalidResourceRequestException : Exception
    {
        public string InvalidAddress { get; }

        public InvalidResourceRequestException(string address, string message) : base(message) { InvalidAddress = address; }
    }

    /// <summary>
    /// Resource system that loads resource by address and label based on Addressables.
    /// </summary>
    public static class ResourceSystem
    {
        /// <summary>
        /// Dynamic load path placeholder for content catalog
        /// </summary>
        public const string DynamicLoadPath = "{DYNAMIC_LOCAL_PATH}";

        /// <summary>
        /// Options for merging the results of requests.
        /// If keys (A, B) mapped to results ([1,2,4],[3,4,5])...
        ///  - UseFirst (or None) takes the results from the first key
        ///  -- [1,2,4]
        ///  - Union takes results of each key and collects items that matched any key.
        ///  -- [1,2,3,4,5]
        ///  - Intersection takes results of each key, and collects items that matched every key.
        ///  -- [4]
        /// </summary>
        /// <remarks>
        /// Aligned with <see cref="Addressables.MergeMode"/>
        /// </remarks>
        public enum MergeMode
        {
            /// <summary>
            /// Use to indicate that no merge should occur. The first set of results will be used.
            /// </summary>
            None = 0,

            /// <summary>
            /// Use to indicate that the merge should take the first set of results.
            /// </summary>
            UseFirst = 0,

            /// <summary>
            /// Use to indicate that the merge should take the union of the results.
            /// </summary>
            Union,

            /// <summary>
            /// Use to indicate that the merge should take the intersection of the results.
            /// </summary>
            Intersection
        }

        private const byte AssetLoadOperation = 0;

        private const byte InstantiateOperation = 1;

        /// <summary>
        /// Start from 1 since 0 is always invalid handle
        /// </summary>
        private static uint _version = 1;

        private static readonly Dictionary<int, ResourceHandle> InstanceIDMap = new();

        private static readonly SparseArray<AsyncOperationStructure> Operations = new(10, int.MaxValue);

        private struct AsyncOperationStructure
        {
            public AsyncOperationHandle AsyncOperationHandle;

            public ResourceHandle ResourceHandle;
        }

        #region Asset Load
        /// <summary>
        /// Ensure resource location exists. Throws <see cref="InvalidResourceRequestException"/> if not found.
        /// </summary>
        /// <param name="key"></param>
        /// <typeparam name="TAsset"></typeparam>
        public static void EnsureAssetExists<TAsset>(object key)
        {
            var location = Addressables.LoadResourceLocationsAsync(key, typeof(TAsset));
            location.WaitForCompletion();
            if (location.Status != AsyncOperationStatus.Succeeded || location.Result.Count == 0)
            {
                string stringValue;
                if (key is IEnumerable<string> list) stringValue = $"[{string.Join(",", list)}]";
                else stringValue = key.ToString();
                throw new InvalidResourceRequestException(stringValue, $"Address {stringValue} not valid for loading {typeof(TAsset)} asset");
            }
        }

        /// <summary>
        /// Ensure resource location exists. Throws <see cref="InvalidResourceRequestException"/> if not found.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="mergeMode"></param>
        /// <typeparam name="TAsset"></typeparam>
        public static void EnsureAssetExists<TAsset>(IEnumerable key, MergeMode mergeMode)
        {
            var location = Addressables.LoadResourceLocationsAsync(key, (Addressables.MergeMode)mergeMode, typeof(TAsset));
            location.WaitForCompletion();
            if (location.Status != AsyncOperationStatus.Succeeded || location.Result.Count == 0)
            {
                string stringValue;
                if (key is IEnumerable<string> list) stringValue = $"[{string.Join(",", list)}]";
                else stringValue = key.ToString();
                throw new InvalidResourceRequestException(stringValue, $"Address {stringValue} not valid for loading {typeof(TAsset)} asset");
            }
        }

        /// <summary>
        /// Ensure resource location exists asynchronously. Throws <see cref="InvalidResourceRequestException"/> if not found.
        /// </summary>
        /// <param name="key"></param>
        /// <typeparam name="TAsset"></typeparam>
        /// <returns></returns>
        public static async UniTask EnsureAssetExistsAsync<TAsset>(object key)
        {
            var location = Addressables.LoadResourceLocationsAsync(key, typeof(TAsset));
            await location.ToUniTask();
            if (location.Status != AsyncOperationStatus.Succeeded || location.Result.Count == 0)
            {
                string stringValue;
                if (key is IEnumerable<string> list) stringValue = $"[{string.Join(',', list)}]";
                else stringValue = key.ToString();
                throw new InvalidResourceRequestException(stringValue, $"Address {stringValue} not valid for loading {typeof(TAsset)} asset");
            }
        }

        /// <summary>
        /// Ensure resource location exists asynchronously. Throws <see cref="InvalidResourceRequestException"/> if not found.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="mergeMode"></param>
        /// <typeparam name="TAsset"></typeparam>
        /// <returns></returns>
        public static async UniTask EnsureAssetExistsAsync<TAsset>(IEnumerable key, MergeMode mergeMode)
        {
            var location = Addressables.LoadResourceLocationsAsync(key, (Addressables.MergeMode)mergeMode, typeof(TAsset));
            await location.ToUniTask();
            if (location.Status != AsyncOperationStatus.Succeeded || location.Result.Count == 0)
            {
                string stringValue;
                if (key is IEnumerable<string> list) stringValue = $"[{string.Join(',', list)}]";
                else stringValue = key.ToString();
                throw new InvalidResourceRequestException(stringValue, $"Address {stringValue} not valid for loading {typeof(TAsset)} asset");
            }
        }

        /// <summary>
        /// Load asset async
        /// </summary>
        /// <param name="address"></param>
        /// <param name="callBack"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static ResourceHandle<T> LoadAssetAsync<T>(string address, Action<T> callBack = null)
        {
            var handle = Addressables.LoadAssetAsync<T>(address);
            if (callBack != null)
            {
                if (handle.IsDone)
                    callBack(handle.Result);
                else
                    handle.Completed += h => callBack(h.Result);
            }
            return CreateHandle(handle, AssetLoadOperation);
        }
        #endregion Asset Load

        #region Instantiate

        /// <summary>
        /// Instantiate a single <see cref="GameObject"/> async
        /// </summary>
        /// <param name="address">The key of the location of the Object to instantiate.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        /// <param name="callback">Callback on instantiate complete</param>
        /// <returns></returns>
        public static ResourceHandle<GameObject> InstantiateAsync(string address,
            Transform parent = null,
            Action<GameObject> callback = null)
        {
            var handle = Addressables.InstantiateAsync(address, parent);
            var resourceHandle = CreateHandle(handle, InstantiateOperation);
            if (handle.IsDone)
                OnHandleOnCompleted(handle);
            else
                handle.Completed += OnHandleOnCompleted;
            return resourceHandle;

            void OnHandleOnCompleted(AsyncOperationHandle<GameObject> operationHandle)
            {
                InstanceIDMap.Add(operationHandle.Result.GetInstanceID(), resourceHandle);
                callback?.Invoke(operationHandle.Result);
            }
        }
        #endregion

        #region Release
        /// <summary>
        /// Release resource
        /// </summary>
        /// <param name="handle"></param>
        /// <typeparam name="T"></typeparam>
        public static void Release<T>(ResourceHandle<T> handle)
        {
            if (!handle.IsValid()) return;
            if (handle.OperationType == InstantiateOperation)
                ReleaseInstance(handle.Result as GameObject);
            else
                ReleaseAsset(handle);
        }

        /// <summary>
        /// Release resource
        /// </summary>
        /// <param name="handle"></param>
        public static void Release(ResourceHandle handle)
        {
            if (!handle.IsValid()) return;
            if (handle.OperationType == InstantiateOperation)
                ReleaseInstance(handle.Result as GameObject);
            else
                ReleaseAsset(handle);
        }

        /// <summary>
        /// Release Asset, should align with <see cref="LoadAssetAsync{T}"/>
        /// </summary>
        /// <param name="handle"></param>
        public static void ReleaseAsset(ResourceHandle handle)
        {
            if (!handle.IsValid()) return;
            if (handle.InternalHandle.IsValid())
            {
                Addressables.Release(handle.InternalHandle);
            }
            ReleaseHandleInternal(handle);
        }

        /// <summary>
        /// Release GameObject Instance, should align with <see cref="InstantiateAsync"/>
        /// </summary>
        /// <param name="gameObject"></param>
        public static void ReleaseInstance(GameObject gameObject)
        {
            if (!gameObject) return;
            
            if (InstanceIDMap.TryGetValue(gameObject.GetInstanceID(), out var handle))
            {
                if (!handle.IsValid()) return;
                ReleaseHandleInternal(handle);
            }
            
            Addressables.ReleaseInstance(gameObject);
        }

        private static void ReleaseHandleInternal(ResourceHandle handle)
        {
            Operations.RemoveAt(handle.Index);
            _version++;
        }
        #endregion

        #region  Multi Assets Load
        public static ResourceHandle<IList<T>> LoadAssetsAsync<T>(object key, Action<IList<T>> callBack = null)
        {
            var handle = Addressables.LoadAssetsAsync<T>(key, null);
            if (callBack != null)
            {
                if (handle.IsDone)
                    callBack(handle.Result);
                else
                    handle.Completed += h => callBack(h.Result);
            }
            return CreateHandle(handle, AssetLoadOperation);
        }

        public static ResourceHandle<IList<T>> LoadAssetsAsync<T>(IEnumerable key, MergeMode mode, Action<IList<T>> callBack = null)
        {
            var handle = Addressables.LoadAssetsAsync<T>(key, null, (Addressables.MergeMode)mode);
            if (callBack != null)
            {
                if (handle.IsDone)
                    callBack(handle.Result);
                else
                    handle.Completed += h => callBack(h.Result);
            }
            return CreateHandle(handle, AssetLoadOperation);
        }
        #endregion

        #region Integration
        private static ResourceHandle<T> CreateHandle<T>(AsyncOperationHandle<T> asyncOperationHandle, byte operation)
        {
            var index = Operations.AddUninitialized();
            var handle = new ResourceHandle<T>(_version, index, operation);
            Operations[index] = new AsyncOperationStructure
            {
                AsyncOperationHandle = asyncOperationHandle,
                ResourceHandle = handle
            };
            return handle;
        }

        internal static AsyncOperationHandle<T> CastOperationHandle<T>(uint version, int index)
        {
            return CastOperationHandle(version, index).Convert<T>();
        }

        internal static AsyncOperationHandle CastOperationHandle(uint version, int index)
        {
            if (Operations.IsAllocated(index))
            {
                if (Operations[index].ResourceHandle.Version == version)
                    return Operations[index].AsyncOperationHandle;
            }

            return default;
        }

        private static bool IsValid(uint version, int index)
        {
            return Operations.IsAllocated(index) && Operations[index].ResourceHandle.Version == version;
        }
        #endregion Integration

        #region Extensions
        public static UniTask<T>.Awaiter GetAwaiter<T>(this ResourceHandle<T> handle)
        {
            return handle.InternalHandle.GetAwaiter();
        }

        public static UniTask.Awaiter GetAwaiter(this ResourceHandle handle)
        {
            return handle.InternalHandle.GetAwaiter();
        }

        public static UniTask<T> ToUniTask<T>(this ResourceHandle<T> handle)
        {
            return handle.InternalHandle.ToUniTask();
        }

        public static UniTask ToUniTask(this ResourceHandle handle)
        {
            return handle.InternalHandle.ToUniTask();
        }

        public static UniTask<T> WithCancellation<T>(this ResourceHandle<T> handle, CancellationToken cancellationToken, bool cancelImmediately = false, bool autoReleaseWhenCanceled = false)
        {
            return handle.InternalHandle.WithCancellation(cancellationToken, cancelImmediately, autoReleaseWhenCanceled);
        }

        public static UniTask WithCancellation(this ResourceHandle handle, CancellationToken cancellationToken, bool cancelImmediately = false, bool autoReleaseWhenCanceled = false)
        {
            return handle.InternalHandle.WithCancellation(cancellationToken, cancelImmediately, autoReleaseWhenCanceled);
        }

        /// <summary>
        /// Whether internal operation is valid
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public static bool IsValid(this ResourceHandle handle)
        {
            return IsValid(handle.Version, handle.Index);
        }

        /// <summary>
        /// Whether internal operation is valid
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public static bool IsValid<T>(this ResourceHandle<T> handle)
        {
            return IsValid(handle.Version, handle.Index);
        }

        /// <summary>
        /// Whether internal operation is done
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public static bool IsDone(this ResourceHandle handle)
        {
            return IsValid(handle.Version, handle.Index) && handle.InternalHandle.IsDone;
        }

        /// <summary>
        /// Whether internal operation is done
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public static bool IsDone<T>(this ResourceHandle<T> handle)
        {
            return IsValid(handle.Version, handle.Index) && handle.InternalHandle.IsDone;
        }

        /// <summary>
        /// Load asset async by <see cref="AssetReferenceT{T}"/> and convert to <see cref="ResourceHandle{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetReferenceT"></param>
        /// <returns></returns>
        public static ResourceHandle<T> ToResourceHandle<T>(this AssetReferenceT<T> assetReferenceT) where T : UObject
        {
            return CreateHandle(assetReferenceT.LoadAssetAsync(), AssetLoadOperation);
        }
        #endregion Extensions

        #region Content Catalog
        public static string GetCatalogExtension()
        {
#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
            return ".bin";
#else
            return ".json";
#endif
        }

        private static bool TryFindCatalogPath(string path, out string catalogPath)
        {
            if (File.Exists(path) && Path.GetExtension(path) == GetCatalogExtension())
            {
                catalogPath = path;
                return true;
            }
            if (!Directory.Exists(path))
            {
                catalogPath = null;
                return false;
            }

            catalogPath = Path.Combine(path, $"catalog{GetCatalogExtension()}");
            return File.Exists(catalogPath);
        }
        
        /// <summary>
        /// Load Addressables content catalog from path.
        /// </summary>
        /// <param name="path">Can be folder path or catalog path</param>
        /// <returns></returns>
        public static bool LoadCatalog(string path)
        {
            if (!TryFindCatalogPath(path, out var catalogPath))
            {
                Debug.LogError($"[Resource System] No catalog file found in {path}");
                return false;
            }
            
            path = catalogPath.Replace(@"\", "/");
            string actualPath = Path.GetDirectoryName(path)!.Replace(@"\", "/");

            try
            {
#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
                ProcessBinaryCatalog(path, actualPath);
#else
                ProcessJsonCatalog(path, actualPath);
#endif
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Resource System] Unexpected error during process catalog {path}: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Load Addressables content catalog asynchronously from path.
        /// </summary>
        /// <param name="path">Can be folder path or catalog path</param>
        /// <returns></returns>
        public static async UniTask<bool> LoadCatalogAsync(string path)
        {
            if (!TryFindCatalogPath(path, out var catalogPath))
            {
                Debug.LogError($"[Resource System] No catalog file found in {path}");
                return false;
            }
            
            path = catalogPath.Replace(@"\", "/");
            string actualPath = Path.GetDirectoryName(path)!.Replace(@"\", "/");

            try
            {
#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
                await ProcessBinaryCatalogAsync(path, actualPath);
#else
                await ProcessJsonCatalogAsync(path, actualPath);
#endif
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Resource System] Unexpected error during process catalog {path}: {e.Message}");
                return false;
            }
        }

#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
        private static void ProcessBinaryCatalog(string path, string actualPath)
        {
            // Load the binary catalog
            var data = File.ReadAllBytes(path);
            var reader = new BinaryStorageBuffer.Reader(data, 1024, 1024, new ContentCatalogData.Serializer().WithInternalIdResolvingDisabled());
            var catalogData = reader.ReadObject<ContentCatalogData>(0, out _, false);

            var modifiedEntries = CreateModifiedBinaryCatalogEntries(catalogData, internalId => ResolveDynamicCatalogInternalId(internalId, actualPath));

            // Create new catalog with modified data
            var newCatalog = new ContentCatalogData(catalogData.ProviderId)
            {
                BuildResultHash = catalogData.BuildResultHash,
                InstanceProviderData = catalogData.InstanceProviderData,
                SceneProviderData = catalogData.SceneProviderData,
                ResourceProviderData = catalogData.ResourceProviderData
            };
            new ContentCatalogDataWrapper(newCatalog).SetData(modifiedEntries);

            // Serialize and save
            var wr = new BinaryStorageBuffer.Writer(0, new ContentCatalogData.Serializer());
            wr.WriteObject(newCatalog, false);
            File.WriteAllBytes(path, wr.SerializeToByteArray());
            try
            {
                Debug.Log($"[Resource System] Load binary content catalog {path}");
                var handle = Addressables.LoadContentCatalogAsync(path);
                handle.WaitForCompletion();
                EnsureCatalogLoadSucceeded(handle, path);
            }
            finally
            {
                File.WriteAllBytes(path, data);
            }
        }

        private static async Task ProcessBinaryCatalogAsync(string path, string actualPath)
        {
            // Load the binary catalog
            var data = await File.ReadAllBytesAsync(path);
            var reader = new BinaryStorageBuffer.Reader(data, 1024, 1024, new ContentCatalogData.Serializer().WithInternalIdResolvingDisabled());
            var catalogData = reader.ReadObject<ContentCatalogData>(0, out _, false);

            var modifiedEntries = CreateModifiedBinaryCatalogEntries(catalogData, internalId => ResolveDynamicCatalogInternalId(internalId, actualPath));

            // Create new catalog with modified data
            var newCatalog = new ContentCatalogData(catalogData.ProviderId)
            {
                BuildResultHash = catalogData.BuildResultHash,
                InstanceProviderData = catalogData.InstanceProviderData,
                SceneProviderData = catalogData.SceneProviderData,
                ResourceProviderData = catalogData.ResourceProviderData
            };
            new ContentCatalogDataWrapper(newCatalog).SetData(modifiedEntries);

            // Serialize and save
            var wr = new BinaryStorageBuffer.Writer(0, new ContentCatalogData.Serializer());
            wr.WriteObject(newCatalog, false);
            await File.WriteAllBytesAsync(path, wr.SerializeToByteArray());
            try
            {
                Debug.Log($"[Resource System] Load binary content catalog {path}");
                var handle = Addressables.LoadContentCatalogAsync(path);
                await UniTask.WaitUntil(() => handle.IsDone);
                EnsureCatalogLoadSucceeded(handle, path);
            }
            finally
            {
                await File.WriteAllBytesAsync(path, data);
            }
        }

        private static List<ContentCatalogDataEntry> CreateModifiedBinaryCatalogEntries(ContentCatalogData catalogData, Func<string, string> internalIdResolver)
        {
            var locator = catalogData.CreateCustomLocator();
            var locationToKeys = new Dictionary<CatalogLocationKey, (IResourceLocation, HashSet<object>)>();
            foreach (var key in locator.Keys)
            {
                if (!locator.Locate(key, typeof(object), out var locs))
                {
                    continue;
                }

                foreach (var loc in locs)
                {
                    var locationKey = CatalogLocationKey.Create(loc);
                    if (!locationToKeys.TryGetValue(locationKey, out var locKeys))
                    {
                        locationToKeys.Add(locationKey, locKeys = (loc, new HashSet<object>()));
                    }

                    locKeys.Item2.Add(key);
                }
            }

            var modifiedEntries = new List<ContentCatalogDataEntry>();
            foreach (var kvp in locationToKeys)
            {
                var loc = kvp.Value.Item1;
                var modifiedInternalId = internalIdResolver(loc.InternalId);
                List<object> deps = null;
                if (loc.HasDependencies)
                {
                    deps = new List<object>();
                    foreach (var dependency in loc.Dependencies)
                    {
                        deps.Add(dependency.PrimaryKey);
                    }
                }

                modifiedEntries.Add(new ContentCatalogDataEntry(
                    loc.ResourceType,
                    modifiedInternalId,
                    loc.ProviderId,
                    kvp.Value.Item2,
                    deps,
                    loc.Data
                ));
            }

            return modifiedEntries;
        }

        private readonly struct CatalogLocationKey : IEquatable<CatalogLocationKey>
        {
            private readonly string _primaryKey;
            private readonly string _internalId;
            private readonly string _providerId;
            private readonly Type _resourceType;
            private readonly int _dependencyHashCode;

            private CatalogLocationKey(IResourceLocation location)
            {
                _primaryKey = location.PrimaryKey;
                _internalId = location.InternalId;
                _providerId = location.ProviderId;
                _resourceType = location.ResourceType;
                _dependencyHashCode = location.DependencyHashCode;
            }

            public static CatalogLocationKey Create(IResourceLocation location)
            {
                return new CatalogLocationKey(location);
            }

            public bool Equals(CatalogLocationKey other)
            {
                return string.Equals(_primaryKey, other._primaryKey, StringComparison.Ordinal)
                       && string.Equals(_internalId, other._internalId, StringComparison.Ordinal)
                       && string.Equals(_providerId, other._providerId, StringComparison.Ordinal)
                       && Equals(_resourceType, other._resourceType)
                       && _dependencyHashCode == other._dependencyHashCode;
            }

            public override bool Equals(object obj)
            {
                return obj is CatalogLocationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_primaryKey ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_internalId ?? string.Empty);
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_providerId ?? string.Empty);
                    hash = hash * 31 + (_resourceType != null ? _resourceType.GetHashCode() : 0);
                    hash = hash * 31 + _dependencyHashCode;
                    return hash;
                }
            }
        }
#else
        private static void ProcessJsonCatalog(string path, string actualPath)
        {
            string contentCatalog = File.ReadAllText(path, Encoding.UTF8);
            string modifiedCatalog = ResolveDynamicCatalogContent(contentCatalog, actualPath);
            File.WriteAllText(path, modifiedCatalog, Encoding.UTF8);
            try
            {
                Debug.Log($"[Resource System] Load json content catalog {path}");
                var handle = Addressables.LoadContentCatalogAsync(path);
                handle.WaitForCompletion();
                EnsureCatalogLoadSucceeded(handle, path);
            }
            finally
            {
                File.WriteAllText(path, contentCatalog, Encoding.UTF8);
            }
        }
        
        private static async Task ProcessJsonCatalogAsync(string path, string actualPath)
        {
            string contentCatalog = await File.ReadAllTextAsync(path, Encoding.UTF8);
            string modifiedCatalog = ResolveDynamicCatalogContent(contentCatalog, actualPath);
            await File.WriteAllTextAsync(path, modifiedCatalog, Encoding.UTF8);
            try
            {
                Debug.Log($"[Resource System] Load json content catalog {path}");
                var handle = Addressables.LoadContentCatalogAsync(path);
                await UniTask.WaitUntil(() => handle.IsDone);
                EnsureCatalogLoadSucceeded(handle, path);
            }
            finally
            {
                await File.WriteAllTextAsync(path, contentCatalog, Encoding.UTF8);
            }
        }
#endif

        private static string ResolveDynamicCatalogInternalId(string internalId, string actualPath)
        {
            if (string.IsNullOrEmpty(internalId))
            {
                return internalId;
            }

            return internalId.Replace(DynamicLoadPath, NormalizeCatalogPath(actualPath)).Replace('\\', '/');
        }

        private static string ResolveDynamicCatalogContent(string contentCatalog, string actualPath)
        {
            string normalizedActualPath = NormalizeCatalogPath(actualPath);
            return contentCatalog
                .Replace(DynamicLoadPath + @"\\", normalizedActualPath + "/")
                .Replace(DynamicLoadPath + @"\/", normalizedActualPath + "/")
                .Replace(DynamicLoadPath + "/", normalizedActualPath + "/")
                .Replace(DynamicLoadPath, normalizedActualPath);
        }

        private static string NormalizeCatalogPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/').TrimEnd('/');
        }

        private static void EnsureCatalogLoadSucceeded<T>(AsyncOperationHandle<T> handle, string path)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                return;
            }

            if (handle.OperationException != null)
            {
                Debug.LogException(handle.OperationException);
            }

            throw new InvalidOperationException($"Addressables failed to load content catalog '{path}'. Status: {handle.Status}. {handle.OperationException?.Message ?? "No operation exception was provided."}", handle.OperationException);
        }

#if (UNITY_6000_0_OR_NEWER && !ENABLE_JSON_CATALOG)
        private readonly struct ContentCatalogDataWrapper
        {
            private static readonly FieldInfo EntriesFieldInfo;

            private readonly ContentCatalogData _catalog;
            
            static ContentCatalogDataWrapper()
            {
                EntriesFieldInfo = typeof(ContentCatalogData).GetField("m_Entries", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            public ContentCatalogDataWrapper(ContentCatalogData catalogData)
            {
                _catalog = catalogData;
            }

            public void SetData(IList<ContentCatalogDataEntry> entries)
            {
                EntriesFieldInfo.SetValue(_catalog, entries);
            }
        }
#endif
        #endregion Content Catalog
    }
}
