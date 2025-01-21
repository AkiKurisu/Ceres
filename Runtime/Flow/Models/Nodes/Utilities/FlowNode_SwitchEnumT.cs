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

        [OutputPort(false), CeresLabel("")]
        public NodePort[] outputs;
        
        protected sealed override UniTask Execute(ExecutionContext executionContext)
        {
            var index = sourceValue.Value.GetHashCode();
            executionContext.SetNext(outputs[index].GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }

        public void OnBeforeSerialize()
        {
            
        }

        public void OnAfterDeserialize()
        {
            outputs = new NodePort[Enum.GetValues(typeof(TEnum)).Length];
            for (int i = 0; i < outputs.Length; i++)
            {
                outputs[i] = new NodePort();
            }
        }

        public int GetPortArraySize()
        {
            return Enum.GetValues(typeof(TEnum)).Length;
        }

        public string GetPortArrayFieldName()
        {
            return nameof(outputs);
        }
    }
}
