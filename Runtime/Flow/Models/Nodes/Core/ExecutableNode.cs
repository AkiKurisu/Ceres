using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Interface for node contains named property
    /// </summary>
    public interface IPropertyNode
    {
        void SetPropertyName(string propertyName);

        string GetPropertyName();
    }
    
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
    [CeresGroup(CeresGroup.Hidden)]
    [CeresLabel(InvalidNode.NodeLabel)]
    [NodeInfo(InvalidNode.NodeInfo)]
    public class InvalidExecutableNode : FlowNode
    {
        [Multiline]
        public string nodeType;
        
        [Multiline]
        public string serializedData;
    }
    
    [Serializable]
    [CeresGroup(CeresGroup.Hidden)]
    [CeresLabel(NodeLabel)]
    [NodeInfo(NodeInfo)]
    public class IllegalExecutableNode : FlowNode
    {
        [Multiline]
        public string nodeType;
        
        [Multiline]
        public string serializedData;
        
        public const string NodeInfo =
            "The presence of this node indicates that there are illegal properties that cause the node to fail to load correctly.";
        
        public const string NodeLabel =
            "<color=#FFE000><b>Illegal Propeties!</b></color>";
    }
}
