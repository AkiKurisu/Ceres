using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UObject = UnityEngine.Object;
namespace Ceres.Graph.Flow.Properties
{
    [Serializable]
    [CeresGroup("Hidden")]
    [CeresLabel("{0}")]
    [NodeInfo("Outputs the graph context as the selected self reference type.")]
    public class PropertyNode_GetSelfTReference<TTarget>: PropertyNode where TTarget: UObject
    {
        [OutputPort, CeresLabel("Value")]
        public CeresPort<TTarget> outputValue;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            outputValue.Value = (TTarget)executionContext.Context;
            return UniTask.CompletedTask;
        }
    }
}
