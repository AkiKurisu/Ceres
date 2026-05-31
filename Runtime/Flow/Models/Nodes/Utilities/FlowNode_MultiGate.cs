using System;
using System.Collections.Generic;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Flow Control")]
    [CeresLabel("MultiGate")]
    [NodeInfo("Routes each trigger to one output in sequence or random order, with optional looping and reset support.")]
    [CeresMetadata("style = ForwardNode", "ResolverOnly")]
    public class FlowNode_MultiGate : ForwardNode, ISerializationCallbackReceiver, IPortArrayNode
    {
        [InputPort, CeresLabel("Reset")]
        public NodePort reset;

        [InputPort, CeresLabel("Start Index")]
        public CeresPort<int> startIndex = new(0);

        [InputPort, CeresLabel("Loop")]
        public CeresPort<bool> loop = new(false);

        [InputPort, CeresLabel("Random")]
        public CeresPort<bool> random = new(false);

        [OutputPort(false), CeresLabel("Out"), CeresMetadata("DefaultLength = 2")]
        public NodePort[] outputs;

        [HideInGraphEditor]
        public int outputCount;

        [NonSerialized]
        private int _nextIndex;

        [NonSerialized]
        private List<int> _remaining = new();

        [NonSerialized]
        private bool _randomInitialized;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            if (executionContext.CurrentInputPortId == nameof(reset))
            {
                ResetState();
                return UniTask.CompletedTask;
            }

            if (outputs == null || outputs.Length == 0)
            {
                return UniTask.CompletedTask;
            }

            var index = random.Value ? NextRandomIndex() : NextSequentialIndex();
            if (index >= 0 && index < outputs.Length)
            {
                executionContext.SetNext(outputs[index]);
            }
            return UniTask.CompletedTask;
        }

        private int NextSequentialIndex()
        {
            if (_nextIndex < 0 || _nextIndex >= outputs.Length)
            {
                _nextIndex = Mathf.Clamp(startIndex.Value, 0, outputs.Length - 1);
            }

            var result = _nextIndex;
            _nextIndex++;
            if (_nextIndex >= outputs.Length)
            {
                _nextIndex = loop.Value ? 0 : outputs.Length;
            }

            return result >= outputs.Length ? -1 : result;
        }

        private int NextRandomIndex()
        {
            _remaining ??= new List<int>();
            if (_remaining.Count == 0)
            {
                if (_randomInitialized && !loop.Value)
                {
                    return -1;
                }
                RefillRemaining();
                _randomInitialized = true;
            }

            var pick = UnityEngine.Random.Range(0, _remaining.Count);
            var result = _remaining[pick];
            _remaining.RemoveAt(pick);
            return result;
        }

        private void ResetState()
        {
            _nextIndex = Mathf.Max(0, startIndex.Value);
            _remaining ??= new List<int>();
            _remaining.Clear();
            _randomInitialized = true;
            RefillRemaining();
        }

        private void RefillRemaining()
        {
            if (outputs == null) return;
            for (var i = 0; i < outputs.Length; i++)
            {
                _remaining.Add(i);
            }
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            outputs = new NodePort[outputCount];
            for (var i = 0; i < outputCount; i++)
            {
                outputs[i] = new NodePort();
            }
        }

        public int GetPortArrayLength()
        {
            return outputCount;
        }

        public string GetPortArrayFieldName()
        {
            return nameof(outputs);
        }

        public void SetPortArrayLength(int newLength)
        {
            outputCount = newLength;
        }
    }
}
