using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Execute executable event, only support non-generic event
    /// </summary>
    [Serializable]
    [NodeGroup("Hidden")]
    public class FlowNode_ExecuteEvent: FlowNode
    {
        [HideInGraphEditor]
        public string eventName;
        
        protected override async UniTask Execute(ExecutionContext executionContext)
        {
            await executionContext.Forward((ExecutableNode)executionContext.Graph.FindNode(eventName)); 
            executionContext.SetNext(exec.GetT<ExecutableNode>());
        }
    }
}