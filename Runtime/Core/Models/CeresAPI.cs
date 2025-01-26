using System;
using UnityEngine;
namespace Ceres
{
    /// <summary>
    /// Main api of Ceres
    /// </summary>
    public static class CeresAPI
    {
        public readonly struct LogLevelAutoScope: IDisposable
        {
            private readonly LogType _logType;

            public LogLevelAutoScope(LogType scopeLogLevel)
            {
                _logType = LogLevel;
                LogLevel = scopeLogLevel;
            }

            public void Dispose()
            {
                LogLevel = _logType;
            }
        }
        
        /// <summary>
        /// The global <see cref="LogType"/> in Ceres
        /// </summary>
        public static LogType LogLevel { get; set; } = LogType.Log;
        
        /// <summary>
        /// Whether to log <see cref="UnityEngine.Object"/> relink details
        /// </summary>
        public static bool LogUObjectRelink { get; set; }

        /// <summary>
        /// Create a log scope in specific level
        /// </summary>
        /// <param name="logLevel"></param>
        /// <returns></returns>
        public static LogLevelAutoScope LogScope(LogType logLevel)
        {
            return new LogLevelAutoScope(logLevel);
        }
        
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