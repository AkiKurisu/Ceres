using System;
using Ceres.Configs;

namespace Ceres.Gameplay
{
    /// <summary>
    /// Gameplay config
    /// </summary>
    [Serializable]
    [ConfigPath("Ceres.Gameplay")]
    public class GameplayConfig: Config<GameplayConfig>
    {
        /// <summary>
        /// Whether to enable per-actor remote update.
        /// </summary>
        public bool enableRemoteUpdate;
        
        /// <summary>
        /// Whether to ensure that world subsystem is initialized before getting the system instance.
        /// </summary>
        public bool subsystemForceInitializeBeforeGet = true;
    }
}