using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using UnityEngine;
namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Executable function library for CeresAPI
    /// </summary>
    [CeresGroup("Ceres")]
    public partial class CeresExecutableLibrary: ExecutableFunctionLibrary
    {
        [ExecutableFunction, CeresLabel("Set LogLevel")]
        public static void Flow_SetLogLevel(LogType logType)
        {
            CeresAPI.LogLevel = logType;
        }
        
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Get LogLevel")]
        public static LogType Flow_GetLogLevel()
        {
            return CeresAPI.LogLevel;
        }
    }
}