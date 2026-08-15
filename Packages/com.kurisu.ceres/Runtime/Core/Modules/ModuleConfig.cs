using System;
using System.Collections.Generic;
using Ceres.Configs;
using Ceres.Serialization;

namespace Ceres.Modules
{
    /// <summary>
    /// Config for <see cref="RuntimeModule"/>
    /// </summary>
    [PreferJsonConvert]
    [ConfigPath("Ceres.Modules")]
    public class ModuleConfig: Config<ModuleConfig>
    {
        /// <summary>
        /// Explicit list of <see cref="RuntimeModule"/> types to load. Used by <see cref="ModuleLoader"/> at runtime
        /// to avoid assembly scanning, which is unreliable under IL2CPP.
        /// </summary>
        public SerializedType<RuntimeModule>[] Modules { get; set; } = Array.Empty<SerializedType<RuntimeModule>>();
        
        /// <summary>
        /// Contains module additional data that can be changed during runtime
        /// </summary>
        public Dictionary<string, string> MetaData { get; set; } = new();
    }
}