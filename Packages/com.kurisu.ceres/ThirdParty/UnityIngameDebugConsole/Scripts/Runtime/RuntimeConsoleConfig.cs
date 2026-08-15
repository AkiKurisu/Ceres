using System;
using Ceres.Configs;

namespace Ceres.RuntimeConsole
{
    [Serializable]
    [ConfigPath("Ceres.RuntimeConsole")]
    public class RuntimeConsoleConfig: Config<RuntimeConsoleConfig>
    {
        public bool enableConsoleInReleaseBuild;
    }
}