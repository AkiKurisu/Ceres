using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using UnityEngine;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Provides Ceres runtime utility functions that can be called from Flow graphs.
    /// </summary>
    [CeresGroup("Ceres")]
    public partial class CeresExecutableLibrary: ExecutableFunctionLibrary
    {
        /// <summary>
        /// Sets the minimum Ceres log level used by runtime logging.
        /// </summary>
        /// <param name="logType">The Unity log type to use as the current Ceres log level.</param>
        [ExecutableFunction, CeresLabel("Set LogLevel")]
        public static void Flow_SetLogLevel(LogType logType)
        {
            CeresLogger.LogLevel = logType;
        }
        
        /// <summary>
        /// Gets the current Ceres runtime log level.
        /// </summary>
        /// <returns>The current Unity log type used as the Ceres log level.</returns>
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Get LogLevel")]
        public static LogType Flow_GetLogLevel()
        {
            return CeresLogger.LogLevel;
        }
    }
}
