using System.Collections.Generic;

namespace Ceres.Editor.Installer
{
    /// <summary>
    /// Configuration for Ceres dependencies
    /// </summary>
    public static class DependencyConfig
    {
        public static readonly List<DependencyInfo> Dependencies = new()
        {
            new DependencyInfo(
                "com.cysharp.unitask",
                "UniTask",
                "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
                "UniTask - Provides Ceres async/await support"
            ),
            new DependencyInfo(
                "com.kurisu.chris",
                "Chris",
                "https://github.com/AkiKurisu/Chris.git",
                "Chris - Provides Ceres core functionality"
            )
        };
    }
}

