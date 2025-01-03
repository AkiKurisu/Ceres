using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UObject = UnityEngine.Object;
namespace Ceres.Graph.Flow
{
    [Serializable]
    [CeresMetadata("style = ExecutableNode")]
    public abstract class ExecutableNode : CeresNode
    {
        protected CeresGraph Graph { get; private set; }
        
        protected abstract UniTask Execute(ExecutionContext executionContext);
        
        public async UniTask ExecuteNode(ExecutionContext executionContext)
        {
            Graph = executionContext.Graph;
            await Execute(executionContext);
        }
        
        protected TValue GetTargetOrDefault<TValue>(CeresPort<TValue> inputPort, ExecutionContext context)
        {
            bool isNull;
            if( inputPort.Value is UObject uObject)
            {
                isNull = !uObject;
            }
            else
            {
                isNull = inputPort.Value != null;
            }
            
            if (isNull && context.Context is TValue tmpTarget)
            {
                return tmpTarget;
            }
            return inputPort.Value;
        }
    }
    
    /// <summary>
    /// Base class for executable nodes with parent node input, used in forward execution path
    /// </summary>
    [Serializable]
    [CeresMetadata("style = ForwardNode")]
    public abstract class ForwardNode : ExecutableNode
    {
        /// <summary>
        /// Dependency node port
        /// </summary>
        [InputPort, CeresLabel("")]
        public NodePort input;
    }

    public interface IPropertyNode
    {
        void SetPropertyName(string propertyName);

        string GetPropertyName();
    }
    
    /// <summary>
    /// Base class for executable nodes that contained graph property without execution
    /// </summary>
    [Serializable]
    [CeresMetadata("style = PropertyNode", "path = Dependency")]
    public abstract class PropertyNode : ExecutableNode, IPropertyNode
    {
        [HideInGraphEditor]
        public string propertyName;

        public void SetPropertyName(string inPropertyName)
        {
            propertyName = inPropertyName;
        }

        public string GetPropertyName()
        {
            return propertyName;
        }
    }
    
    /// <summary>
    /// Base class for nodes in an execution flow
    /// </summary>
    [Serializable]
    [CeresMetadata("style = FunctionNode")]
    public abstract class FlowNode : ForwardNode
    {
        [OutputPort(false), CeresLabel("")]
        public NodePort exec;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            LocalExecute(executionContext);
            executionContext.SetNext(exec.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }
        
        /// <summary>
        /// Do node local execution, sync only
        /// </summary>
        /// <param name="executionContext"></param>
        protected virtual void LocalExecute(ExecutionContext executionContext)
        {
            
        }
    }
    
    [Serializable]
    [NodeGroup(Ceres.Annotations.NodeGroup.Hidden)]
    [CeresLabel(InvalidNode.NodeLabel)]
    [NodeInfo(InvalidNode.NodeInfo)]
    public class InvalidExecutableNode : FlowNode
    {
        [Multiline]
        public string nodeType;
        
        [Multiline]
        public string serializedData;
    }
}
