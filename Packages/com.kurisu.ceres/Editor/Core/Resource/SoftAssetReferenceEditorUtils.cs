using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using System.Collections.Generic;
using Ceres.Serialization;
using UObject = UnityEngine.Object;

namespace Ceres.Resource.Editor
{
    public static class SoftAssetReferenceEditorUtils
    {
        private static readonly Dictionary<string, SoftObjectHandle> RefDic = new();

        static SoftAssetReferenceEditorUtils()
        {
            // Cleanup cache since SoftObjectHandle is not valid anymore
            GlobalObjectManager.OnGlobalObjectCleanup += () => RefDic.Clear();
        }

        /// <summary>
        /// Optimized fast api for load asset from guid in editor
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static UObject GetAssetFromGUID(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;

            if (!RefDic.TryGetValue(guid, out var handle))
            {
                var uObject = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), typeof(UObject));
                if (uObject)
                {
                    GlobalObjectManager.RegisterObject(uObject, ref handle);
                    RefDic[guid] = handle;
                    return uObject;
                }
                return null;
            }
            var cacheObject = handle.GetObject() as UObject;
            if (cacheObject) return cacheObject;

            GlobalObjectManager.UnregisterObject(handle);
            var newObject = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), typeof(UObject));
            if (newObject)
            {
                GlobalObjectManager.RegisterObject(newObject, ref handle);
                RefDic[guid] = handle;
            }
            return newObject;
        }

        /// <summary>
        /// Resolve the AssetDatabase path stored by a soft reference without loading the asset.
        /// </summary>
        public static bool TryGetAssetPath(SoftAssetReferenceBase softAssetReference, out string assetPath)
        {
            assetPath = softAssetReference == null
                ? string.Empty
                : AssetDatabase.GUIDToAssetPath(softAssetReference.Guid);
            return !string.IsNullOrEmpty(assetPath);
        }

        /// <summary>
        /// Get asset from soft asset reference, fallback to addressable system if GUID lookup fails
        /// </summary>
        /// <param name="softAssetReference">The soft asset reference to load</param>
        /// <returns>The loaded asset or null if not found</returns>
        public static UObject GetAsset(SoftAssetReferenceBase softAssetReference)
        {
            if (string.IsNullOrEmpty(softAssetReference.Address)) return null;
            var asset = GetAssetFromGUID(softAssetReference.Guid);
            if (!asset)
            {
                asset = ResourceSystem.LoadAssetAsync<UObject>(softAssetReference.Address).WaitForCompletion();
            }
            return asset;
        }

        /// <summary>
        /// Create a soft asset reference from object
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="groupName"></param>
        /// <param name="registerAddressable">Whether to create an Addressable entry when one does not exist.</param>
        /// <returns></returns>
        public static SoftAssetReference FromObject(
            UObject asset,
            string groupName = null,
            bool registerAddressable = true)
        {
            if (!asset)
            {
                return new SoftAssetReference();
            }
            var reference = new SoftAssetReference { Guid = asset.GetAssetGUID(), Locked = true };
            var existingEntry = asset.ToAddressableAssetEntry();
            if (existingEntry != null)
            {
                reference.Address = existingEntry.address;
            }
            else
            {
                reference.Address = AssetDatabase.GetAssetPath(asset);
                if (registerAddressable)
                {
                    AddressableAssetGroup assetGroup;
                    if (string.IsNullOrEmpty(groupName))
                        assetGroup = AddressableAssetSettingsDefaultObject.Settings.DefaultGroup;
                    else
                        assetGroup = ResourceEditorUtils.GetOrCreateAssetGroup(groupName);
                    var entry = assetGroup.AddAsset(asset);
                    assetGroup.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, false, true);
                    reference.Address = entry.address;
                }
            }
            return reference;
        }

        /// <summary>
        /// Create a generic soft asset reference from object with type safety
        /// </summary>
        /// <typeparam name="T">The type of the asset, must inherit from UnityEngine.Object</typeparam>
        /// <param name="asset">The asset to create reference from</param>
        /// <param name="groupName">Optional addressable group name, uses default if null</param>
        /// <returns>A typed soft asset reference</returns>
        public static SoftAssetReference<T> FromTObject<T>(
            T asset,
            string groupName = null,
            bool registerAddressable = true) where T : UObject
        {
            return FromObject(asset, groupName, registerAddressable);
        }

        /// <summary>
        /// Move <see cref="SoftAssetReference"/> asset to target <see cref="AddressableAssetGroup"/> safe in editor
        /// </summary>
        /// <param name="reference">The soft asset reference to move</param>
        /// <param name="group">The target addressable asset group</param>
        /// <param name="labels">Asset labels to assign to the moved asset</param>
        public static void MoveSoftReferenceObject(ref SoftAssetReference reference, AddressableAssetGroup group, params string[] labels)
        {
            var uObject = GetAssetFromGUID(reference.Guid);
            if (!uObject) return;

            var newEntry = group.AddAsset(uObject, labels);
            reference.Address = newEntry.address;
        }
    }
}
