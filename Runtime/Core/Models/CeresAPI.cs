using UnityEngine;
namespace Ceres
{
    /// <summary>
    /// Main api of Ceres
    /// </summary>
    public static class CeresAPI
    {
        /// <summary>
        /// The global <see cref="LogType"/> in Ceres
        /// </summary>
        public static LogType LogLevel { get; set; } = LogType.Log;
        
        public static void LogWarning(string message)
        {
            if(LogLevel >= LogType.Warning)
                Debug.LogWarning($"<color=#fcbe03>[Ceres]</color> {message}");
        }
        
        public static void Log(string message)
        {
            if(LogLevel >= LogType.Log)
                Debug.Log($"<color=#3aff48>[Ceres]</color> {message}");
        }

        public static void LogError(string message)
        {
            if(LogLevel >= LogType.Error)
                Debug.LogError($"<color=#ff2f2f>[Ceres]</color> {message}");
        }
    }
}