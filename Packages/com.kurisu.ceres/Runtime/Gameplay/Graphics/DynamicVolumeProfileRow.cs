#if URP_INSTALL
using System;
using System.Collections.Generic;
using Ceres.DataDriven;
using Ceres.Pool;
using Ceres.Resource;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ceres.Gameplay.Graphics
{
    /// <summary>
    /// Define platform options for Dynamic Volume
    /// </summary>
    public enum DynamicVolumePlatform
    {
        Windows,
        Mobile,
        Console
    }
    
    [Serializable, AddressableDataTable(address: DynamicVolumeProfileTableManager.TableKey)]
    public class DynamicVolumeProfileRow: IDataTableRow
    {
        [AssetReferenceConstraint(group: "Volumes")]
        public SoftAssetReference<VolumeProfile> windowsProfile;
        
        [AssetReferenceConstraint(group: "Volumes")]
        public SoftAssetReference<VolumeProfile> mobileProfile;
                
        [AssetReferenceConstraint(group: "Volumes")]
        public SoftAssetReference<VolumeProfile> consoleProfile;

        [Tooltip("A value which determines which Volume is being used when Volumes have an equal amount of influence on the Scene. " +
                 "Volumes with a higher priority will override lower ones.")]
        public int priority;

        public SoftAssetReference<VolumeProfile> GetVolumeProfile(DynamicVolumePlatform? overridePlatform = null)
        {
            DynamicVolumePlatform platform;

            if (overridePlatform.HasValue)
            {
                platform = overridePlatform.Value;
            }
            else
            {
#if UNITY_ANDROID || UNITY_IOS
                platform = DynamicVolumePlatform.Mobile;
#elif UNITY_XBOXONE || UNITY_PS5
                platform = DynamicVolumePlatform.Console;
#else
                platform = DynamicVolumePlatform.Windows;
#endif
            }

            SoftAssetReference<VolumeProfile> profileReference = windowsProfile;
            switch (platform)
            {
                case DynamicVolumePlatform.Windows:
                    profileReference = windowsProfile;
                    break;
                case DynamicVolumePlatform.Mobile:
                    profileReference = mobileProfile;
                    break;
                case DynamicVolumePlatform.Console:
                    profileReference = consoleProfile;
                    break;
            }

            if (profileReference == null || string.IsNullOrEmpty(profileReference.Address))
            {
                profileReference = windowsProfile;
            }

            return profileReference;
        }
    }
    
    public class DynamicVolumeProfileTableManager : DataTableManager<DynamicVolumeProfileTableManager>
    {
        public DynamicVolumeProfileTableManager(object _) : base(_)
        {
        }

        public const string TableKey = "DynamicVolumeProfileTable";

        private readonly Dictionary<string, DynamicVolumeProfileRow> _profileRows = new();
        
        protected sealed override async UniTask Initialize(bool sync)
        {
            await InitializeSingleTable(TableKey, sync);
            var dt = GetDataTable(TableKey);
            var rows = dt.GetRowMap();
            if (sync)
            {
                foreach (var row in rows)
                {
                    var profileRow = (DynamicVolumeProfileRow)row.Value;
                    _profileRows[row.Key] = profileRow;
#if UNITY_ANDROID || UNITY_IOS
                    profileRow.mobileProfile.LoadAsync().WaitForCompletion();
#elif UNITY_XBOXONE || UNITY_PS5
                    profileRow.consoleProfile.LoadAsync().WaitForCompletion();
#else
                    profileRow.windowsProfile.LoadAsync().WaitForCompletion();
#endif
                }
                return;
            }
            
            using var parallel = UniParallel.Get();
            foreach (var row in rows)
            {
                var profileRow = (DynamicVolumeProfileRow)row.Value;
                _profileRows[row.Key] = profileRow;
#if UNITY_ANDROID || UNITY_IOS
                parallel.Add(profileRow.mobileProfile.LoadAsync().ToUniTask());
#elif UNITY_XBOXONE || UNITY_PS5
                parallel.Add(profileRow.consoleProfile.LoadAsync().ToUniTask());
#else
                parallel.Add(profileRow.windowsProfile.LoadAsync().ToUniTask());
#endif
            }
            await parallel;
        }
        
        public VolumeProfile GetProfile(string volumeType, DynamicVolumePlatform? overridePlatform = null)
        {
            if (_profileRows.TryGetValue(volumeType, out var row))
            {
                return row.GetVolumeProfile(overridePlatform).LoadAsync().Result;
            }

            return null;
        }
        
        public float GetPriority(string volumeType)
        {
            if (_profileRows.TryGetValue(volumeType, out var row))
            {
                return row.priority;
            }

            return 0;
        }
    }
}
#endif