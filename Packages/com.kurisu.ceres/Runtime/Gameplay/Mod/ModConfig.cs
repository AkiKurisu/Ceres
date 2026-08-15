using System.Collections.Generic;
using Ceres.Configs;
using Ceres.Serialization;

namespace Ceres.Gameplay.Mod
{
    /// <summary>
    /// Configuration of mod importer
    /// </summary>
    [ConfigPath("Ceres.Mod")]
    [PreferJsonConvert]
    public class ModConfig: Config<ModConfig>
    {
        /// <summary>
        /// Mod loading path
        /// </summary>
        public string LoadingPath { get; set; } = ModAPI.LoadingPath;
        
        /// <summary>
        /// API version for mod validation
        /// </summary>
        public string ApiVersion { get; set; } = ModAPI.DefaultAPIVersion;
        
        /// <summary>
        /// All mod states
        /// </summary>
        public List<ModState> States { get; set; } = new();
        
        public ModStatus GetModState(ModInfo modInfo)
        {
            if (TryGetModState(modInfo, out var modStateInfo))
            {
                if (modStateInfo.status == ModStatus.Delete)
                {
                    States.Remove(modStateInfo);
                }
                return modStateInfo.status;
            }
            States.Add(new ModState
            {
                fullName = modInfo.FullName,
                status = ModStatus.Enabled
            });
            return ModStatus.Enabled;
        }
        
        public bool IsModActivated(ModInfo modInfo)
        {
            if (TryGetModState(modInfo, out var modStateInfo))
                return modStateInfo.status == ModStatus.Enabled;
            States.Add(new ModState
            {
                fullName = modInfo.FullName,
                status = ModStatus.Enabled
            });
            return true;
        }
        
        public void DeleteMod(ModInfo modInfo, bool force = false)
        {
            if (TryGetModState(modInfo, out var modStateInfo))
            {
                if (force) States.Remove(modStateInfo);
                else modStateInfo.status = ModStatus.Delete;
            }
        }
        
        public void SetModEnabled(ModInfo modInfo, bool isEnabled)
        {
            if (TryGetModState(modInfo, out var modStateInfo))
            {
                modStateInfo.status = isEnabled ? ModStatus.Enabled : ModStatus.Disabled;
            }
        }
        
        public bool TryGetModState(ModInfo modInfo, out ModState modState)
        {
            foreach (var stateInfo in States)
            {
                if (stateInfo.fullName == modInfo.FullName)
                {
                    modState = stateInfo;
                    return true;
                }
            }
            modState = null;
            return false;
        }
    }
}