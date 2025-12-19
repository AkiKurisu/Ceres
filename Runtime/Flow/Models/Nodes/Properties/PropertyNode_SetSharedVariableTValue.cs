using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
namespace Ceres.Graph.Flow.Properties
{
    [Serializable]
    [CeresGroup("Hidden")]
    [CeresLabel("Set {0}")]
    [CeresMetadata("style = PropertyNode", "path = Forward")]
    public sealed class PropertyNode_SetSharedVariableTValue<T, TVariableValue, TInValue>: PropertyNode_SharedVariableValue 
        where T: SharedVariable<TVariableValue>
        where TInValue: TVariableValue
    {
        /// <summary>
        /// Dependency node port
        /// </summary>
        [InputPort, CeresLabel("")]
        public NodePort input;

        [InputPort, CeresLabel("Value")]
        public CeresPort<TInValue> inputValue;
        
        [OutputPort(false), CeresLabel("")]
        public NodePort exec;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            if (executionContext.Graph.Blackboard.GetSharedVariable(propertyName) is T variable) 
                variable.Value = inputValue.Value;
            executionContext.SetNext(exec.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        } 
    }
}
