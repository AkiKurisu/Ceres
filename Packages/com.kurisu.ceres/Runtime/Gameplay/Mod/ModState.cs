using System;

namespace Ceres.Gameplay.Mod
{
    /// <summary>
    /// Status of installed mod
    /// </summary>
    public enum ModStatus
    {
        /// <summary>
        /// Mod is loaded
        /// </summary>
        Enabled,
        /// <summary>
        /// Mod is not loaded
        /// </summary>
        Disabled,
        /// <summary>
        /// Mod pending to be deleted (will be deleted on next launch)
        /// </summary>
        Delete
    }
    
    [Serializable]
    public class ModState
    {
        public string fullName;
        
        public ModStatus status;
    }
}