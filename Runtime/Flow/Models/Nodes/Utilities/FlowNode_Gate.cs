using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Flow Control")]
    [CeresLabel("Gate")]
    [NodeInfo("Lets execution pass only while the gate is open; Open, Close, and Toggle inputs control the state.")]
    public class FlowNode_Gate : ForwardNode
    {
        [InputPort, CeresLabel("Open")]
        public NodePort open;

        [InputPort, CeresLabel("Close")]
        public NodePort close;

        [InputPort, CeresLabel("Toggle")]
        public NodePort toggle;

        [InputPort, CeresLabel("Start Closed")]
        public CeresPort<bool> startClosed = new(false);

        [OutputPort(false), CeresLabel("Exit")]
        public NodePort exit;

        [NonSerialized]
        private bool _initialized;

        [NonSerialized]
        private bool _isOpen;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            if (!_initialized)
            {
                _initialized = true;
                _isOpen = !startClosed.Value;
            }

            switch (executionContext.CurrentInputPortId)
            {
                case nameof(open):
                    _isOpen = true;
                    return UniTask.CompletedTask;
                case nameof(close):
                    _isOpen = false;
                    return UniTask.CompletedTask;
                case nameof(toggle):
                    _isOpen = !_isOpen;
                    return UniTask.CompletedTask;
            }

            if (_isOpen)
            {
                executionContext.SetNext(exit);
            }

            return UniTask.CompletedTask;
        }
    }
}
