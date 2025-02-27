using System;
using Ceres.Annotations;
using UnityEngine;
namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Logs message to the Unity Console
    /// </summary>
    [Serializable]
    [CeresGroup("Utilities/Debug")]
    [CeresLabel("Debug Log")]
    public sealed class FlowNode_DebugLog: FlowNode
    {
        [InputPort]
        public LogType logType = LogType.Log;
        
        [InputPort, HideInGraphEditor]
        public CeresPort<object> message;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            Debug.unityLogger.Log(logType, message.Value, executionContext.Context);
        }
    }
    
    /// <summary>
    /// Logs string message to the Unity Console.
    /// </summary>
    [Serializable]
    [CeresGroup("Utilities/Debug")]
    [CeresLabel("Log String")]
    public sealed class FlowNode_DebugLogString: FlowNode
    {
        [InputPort]
        public LogType logType = LogType.Log;
        
        [InputPort, CeresLabel("In String")]
        public CeresPort<string> inString;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            Debug.unityLogger.Log(logType, message: inString.Value, executionContext.Context);
        }
    }
}