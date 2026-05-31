using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities")]
    [CeresLabel("Switch on Int")]
    [NodeInfo("Routes execution to the output matching the selected integer case, or Default when enabled.")]
    public class FlowNode_SwitchInt : ForwardNode,
        ISerializationCallbackReceiver, IReadOnlyPortArrayNode
    {
        [Serializable]
        public struct Settings
        {
            public int[] conditions;

            public bool hasDefault;
        }

        [InputPort, CeresLabel("Selection")]
        public CeresPort<int> sourceValue;

        [OutputPort(false)]
        public NodePort[] outputs;

        [OutputPort(false), CeresLabel("Default")]
        public NodePort defaultOutput;

        [HideInGraphEditor]
        public Settings settings;

        protected sealed override UniTask Execute(ExecutionContext executionContext)
        {
            for (var i = 0; i < settings.conditions.Length; i++)
            {
                if (settings.conditions[i] == sourceValue.Value)
                {
                    executionContext.SetNext(outputs[i]);
                    return UniTask.CompletedTask;
                }
            }

            if (settings.hasDefault)
            {
                executionContext.SetNext(defaultOutput);
            }
            return UniTask.CompletedTask;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            outputs = new NodePort[GetPortArrayLength()];
            for (var i = 0; i < outputs.Length; i++)
            {
                outputs[i] = new NodePort();
            }
        }

        public int GetPortArrayLength()
        {
            return settings.conditions?.Length ?? 0;
        }

        public string GetPortArrayFieldName()
        {
            return nameof(outputs);
        }
    }
}
