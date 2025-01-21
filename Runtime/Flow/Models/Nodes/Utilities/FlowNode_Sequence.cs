using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities")]
    [CeresLabel("Sequence")]
    public class FlowNode_Sequence : ForwardNode, ISerializationCallbackReceiver, IPortArrayNode
    {
        [OutputPort(false), CeresLabel("Then"), CeresMetadata("DefaultLength = 2")]
        public NodePort[] outputs;

        [HideInGraphEditor]
        public int outputCount;
        
        protected sealed override async UniTask Execute(ExecutionContext executionContext)
        {
            foreach (var output in outputs)
            {
                var next = output.GetT<ExecutableNode>();
                if(next == null) continue;
                await executionContext.Forward(output.GetT<ExecutableNode>());
            }
        }


        public void OnBeforeSerialize()
        {
            
        }

        public void OnAfterDeserialize()
        {
            outputs = new NodePort[outputCount];
            for (int i = 0; i < outputCount; i++)
            {
                outputs[i] = new NodePort();
            }
        }

        public int GetPortArraySize()
        {
            return outputCount;
        }

        public string GetPortArrayFieldName()
        {
            return nameof(outputs);
        }

        public void SetPortArraySize(int newSize)
        {
            outputCount = newSize;
        }
    }
}
