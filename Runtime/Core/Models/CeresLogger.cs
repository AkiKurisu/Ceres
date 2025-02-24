using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UDebug = UnityEngine.Debug;
using UAssert = UnityEngine.Assertions.Assert;

namespace Ceres
{
    /// <summary>
    /// Logger of Ceres
    /// </summary>
    public static class CeresLogger
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
        
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(string message)
        {
            if(LogLevel >= LogType.Warning)
                UDebug.LogWarning($"<color=#fcbe03>[Ceres]</color> {message}");
        }
        
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(string message)
        {
            if(LogLevel >= LogType.Log)
                UDebug.Log($"<color=#3aff48>[Ceres]</color> {message}");
        }

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(string message)
        {
            if(LogLevel >= LogType.Error)
                UDebug.LogError($"<color=#ff2f2f>[Ceres]</color> {message}");
        }
        
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Assert(bool condition, string message)
        {
            if (LogLevel >= LogType.Assert)
                UAssert.IsTrue(condition, $"<color=#ff2f2f>[Ceres]</color> {message}");
        }
    }
}