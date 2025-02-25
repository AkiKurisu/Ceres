using System;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.CustomFunctions;

namespace Ceres.Editor.Graph.Flow.CustomFunctions
{
    public abstract class ExecuteCustomFunctionNodeView : ExecutableNodeView
    {
        protected string FunctionName { get; private set; }
        
        protected ExecuteCustomFunctionNodeView(Type type, CeresGraphView graphView)
        {
            Initialize(type, graphView);
            SetupNodeElement(new ExecutableNodeElement(this));
            FillDefaultNodeProperties();
            FillDefaultNodePorts();
        }
        
        public sealed override void SetNodeInstance(CeresNode ceresNode)
        {
            var functionNode =(FlowNode_ExecuteCustomFunction)ceresNode;
            SetFunctionName(functionNode.functionName);
            base.SetNodeInstance(ceresNode);
        }

        public void SetFunctionName(string functionName)
        {
            FunctionName = functionName;
            NodeElement.title = functionName;
        }
        
        public override ExecutableNode CompileNode()
        {
            var instance = (FlowNode_ExecuteCustomFunction)base.CompileNode();
            instance.functionName = FunctionName;
            return instance;
        }
    }
    
    [CustomNodeView(typeof(FlowNode_ExecuteCustomFunctionVoid), true)]
    public sealed class FlowNode_ExecuteCustomFunctionVoidNodeView: ExecuteCustomFunctionNodeView
    {
        public FlowNode_ExecuteCustomFunctionVoidNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
        }
    }
    
    [CustomNodeView(typeof(FlowNode_ExecuteCustomFunctionReturn), true)]
    public sealed class FlowNode_ExecuteCustomFunctionReturnNodeView: ExecuteCustomFunctionNodeView
    {
        public FlowNode_ExecuteCustomFunctionReturnNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
        }
    }
}