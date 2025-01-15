using System;
using Ceres.Annotations;
using UObject = UnityEngine.Object;
namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Unity")]
    [CeresLabel("Instantiate {0}")]
    public class FlowNode_InstantiateT<TObject>: FlowNode where TObject: UObject
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TObject> original;
                
        [OutputPort, CeresLabel("Result")]
        public CeresPort<TObject> resultValue;

        protected override void LocalExecute(ExecutionContext executionContext)
        {
            resultValue.Value = UObject.Instantiate(original.Value);
        }
    }
}