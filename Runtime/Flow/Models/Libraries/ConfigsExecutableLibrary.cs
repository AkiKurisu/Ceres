using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Chris.Configs;

namespace Ceres.Graph.Flow.Utilities
{
    [CeresGroup("Configs")]
    public partial class ConfigsExecutableLibrary: ExecutableFunctionLibrary
    {
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Save Config")]
        public static void Flow_SaveConfig(ConfigBase config)
        {
            config.Save();
        }
    }
}