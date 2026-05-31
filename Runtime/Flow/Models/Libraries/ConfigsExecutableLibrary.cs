using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Chris.Configs;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Provides config persistence helpers for Flow graphs.
    /// </summary>
    [CeresGroup("Configs")]
    public partial class ConfigsExecutableLibrary: ExecutableFunctionLibrary
    {
        /// <summary>
        /// Saves the target config asset through the config system.
        /// </summary>
        /// <param name="config">The config instance to save.</param>
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Save Config")]
        public static void Flow_SaveConfig(ConfigBase config)
        {
            config.Save();
        }
    }
}
