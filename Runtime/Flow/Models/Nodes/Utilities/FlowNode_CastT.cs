using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Casting a value from type <see cref="TFrom"/> to type <see cref="TTo"/>.
    /// </summary>
    /// <typeparam name="TFrom"></typeparam>
    /// <typeparam name="TTo"></typeparam>
    [Serializable]
    [CeresGroup("Utilities")]
    [CeresLabel("Cast to {0}")]
    [CeresMetadata("style = ConstNode")]
    public class FlowNode_CastT<TFrom, TTo>: ForwardNode where TTo: TFrom
    {
        [OutputPort(false), CeresLabel("")]
        public NodePort exec;
        
        [InputPort, HideInGraphEditor, CeresLabel("Source")]
        public CeresPort<TFrom> sourceValue;
        
        [OutputPort(false), CeresLabel("Cast Failed")]
        public NodePort castFailed;
                
        [OutputPort, CeresLabel("Result")]
        public CeresPort<TTo> resultValue;

        protected sealed override UniTask Execute(ExecutionContext executionContext)
        {
            try
            {
                resultValue.Value = (TTo)sourceValue.Value;
                executionContext.SetNext(exec.GetT<ExecutableNode>());
            }
            catch (InvalidCastException)
            {
                executionContext.SetNext(castFailed.GetT<ExecutableNode>());
            }

            return UniTask.CompletedTask;
        }
    }
}