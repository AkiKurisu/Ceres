using System;
using Ceres.Annotations;
using UnityEngine;
using UObject = UnityEngine.Object;
namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Unity")]
    [CeresLabel("Instantiate {0}")]
    public class FlowNode_InstantiateT<TObject>: FlowNode where TObject: UObject
    {
        [InputPort]
        public CeresPort<TObject> original;
        
        [InputPort]
        public CeresPort<Vector3> position;
        
        [InputPort]
        public CeresPort<Quaternion> rotation;
        
        [InputPort]
        public CeresPort<Transform> parent;
        
        [InputPort]
        public CeresPort<bool> worldPositionStays;
                
        [OutputPort, CeresLabel("Result")]
        public CeresPort<TObject> resultValue;

        protected override void LocalExecute(ExecutionContext executionContext)
        {
            if (worldPositionStays.Value)
            {
                resultValue.Value = UObject.Instantiate(original.Value, parent.Value, worldPositionStays.Value);
            }
            else
            {
                resultValue.Value = UObject.Instantiate(original.Value, position.Value, rotation.Value, parent.Value);
            }
        }
    }
}