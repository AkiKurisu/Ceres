using System;
using Ceres.DataDriven;
using Ceres.Resource;
using UnityEngine;

namespace Ceres.Gameplay.Level
{
    public enum LoadLevelMode
    {
        Single,
        Additive
    }
    
    [Flags]
    public enum LoadLevelPolicy
    {
        Never = 0,
        PC = 1 << 0,
        Mobile = 1 << 1,
        Console = 1 << 2
    }
    
    [Serializable, AddressableDataTable(Labels = new[] { LevelSceneDataTableManager.TableLabel })]
    public class LevelSceneRow : IDataTableRow, IValidateRow
    {
        public string levelName;
        
        /// <summary>
        /// Soft reference (address) to the real scene asset
        /// </summary>
#if UNITY_EDITOR
        [AssetReferenceConstraint(typeof(UnityEditor.SceneAsset))]
#endif
        public SoftAssetReference reference;
        
        public LoadLevelMode loadMode;
        
        public LoadLevelPolicy loadPolicy = LoadLevelPolicy.PC | LoadLevelPolicy.Mobile | LoadLevelPolicy.Console;

        public string[] tags;
        
        public bool ValidateLoadPolicy()
        {
            if (loadPolicy == LoadLevelPolicy.Never) return false;
            if (Application.isMobilePlatform) return loadPolicy.HasFlag(LoadLevelPolicy.Mobile);
            if (Application.isConsolePlatform) return loadPolicy.HasFlag(LoadLevelPolicy.Console);
            return loadPolicy.HasFlag(LoadLevelPolicy.PC);
        }

        public bool ValidateRow(string rowId, out string reason)
        {
            if (!string.IsNullOrEmpty(reference.Address))
            {
                reason = null;
                return true;
            }

            reason = $"LevelSceneRow {rowId} missing a valid scene asset reference";
            return false;
        }
    }
}
