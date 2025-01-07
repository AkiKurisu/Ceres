using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using UnityEngine;
using UnityEngine.Scripting;
namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Executable function library for ceres
    /// </summary>
    [Preserve]
    public class CeresExecutableFunctionLibrary: ExecutableFunctionLibrary
    {
        [ExecutableFunction, CeresLabel("Set LogLevel")]
        public static void Flow_CeresGraphSetLogLevel(LogType logType)
        {
            CeresGraph.LogLevel = logType;
        }
        
        [ExecutableFunction, CeresLabel("Get LogLevel")]
        public static LogType Flow_CeresGraphGetLogLevel()
        {
            return CeresGraph.LogLevel;
        }
    }
}