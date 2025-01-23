using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Ceres.Graph.Flow.Utilities
{
    public abstract class FlowNode_SwitchEnum : ForwardNode
    {
    }

    [Serializable]
    [CeresGroup("Utilities")]
    [CeresLabel("Switch on {0}")]
    public class FlowNode_SwitchEnumT<TEnum>: FlowNode_SwitchEnum, 
        ISerializationCallbackReceiver, IReadOnlyPortArrayNode
        where TEnum: Enum
    {
        [InputPort, CeresLabel("Source")]
        public CeresPort<TEnum> sourceValue;

        [OutputPort(false)]
        public NodePort[] outputs;
        
        protected sealed override UniTask Execute(ExecutionContext executionContext)
        {
            var index = sourceValue.Value.GetHashCode();
            index = Math.Min(index, outputs.Length - 1);
            executionContext.SetNext(outputs[index].GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }

        public void OnBeforeSerialize()
        {
            
        }

        public void OnAfterDeserialize()
        {
            outputs = new NodePort[GetPortArrayLength()];
            for (int i = 0; i < outputs.Length; i++)
            {
                outputs[i] = new NodePort();
            }
        }

        public int GetPortArrayLength()
        {
            return Enum.GetValues(typeof(TEnum)).Length;
        }

        public string GetPortArrayFieldName()
        {
            return nameof(outputs);
        }
    }
}
