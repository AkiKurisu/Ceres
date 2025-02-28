using System;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.CustomFunctions;

namespace Ceres.Editor.Graph.Flow.CustomFunctions
{
    public abstract class ExecuteCustomFunctionNodeView : ExecutableNodeView
    {
        protected CustomFunction CustomFunction { get; private set; }
        
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
            NodeElement.title = functionName;
            FunctionName = functionName;
            if (GraphView.TryGetSharedVariable(functionName, out var variable) && variable is CustomFunction customFunction)
            {
                CustomFunction = customFunction;
                var inputParameters = FlowGraphEditorWindow.ResolveFunctionInputParameters(customFunction);
                for (int i = 0; i < inputParameters.Length; i++)
                {
                    var portView = FindPortView($"input{i + 1}");
                    portView.SetDisplayName(inputParameters[i].parameterName);
                }
                GraphView.Blackboard.RegisterCallback<VariableChangeEvent>(OnVariableChange);
            }
        }

        private void OnVariableChange(VariableChangeEvent evt)
        {
            if (evt.Variable != CustomFunction) return;
            if (evt.ChangeType == VariableChangeType.Name)
            {
                FunctionName = NodeElement.title = evt.Variable.Name;
            }
            else if(evt.ChangeType == VariableChangeType.Remove)
            {
                CeresLogger.LogWarning($"The function {evt.Variable.Name} was deleted, which may cause an error in the referenced ExecuteCustomFunctionNode during runtime. Please remove the corresponding node.");
                GraphView.ClearSelection();
                GraphView.schedule.Execute(FrameNode).ExecuteLater(200);
                /* Free reference */
                CustomFunction = null;
            }
        }
        
        private void FrameNode()
        {
            GraphView.AddToSelection(NodeElement);
            GraphView.FrameSelection();
        }

        public override void Validate(FlowGraphValidator validator)
        {
            base.Validate(validator);
            if (CustomFunction == null)
            {
                validator.MarkAsInvalid(this, $"Can not find custom function {FunctionName}");
            }
        }

        public override ExecutableNode CompileNode()
        {
            var instance = (FlowNode_ExecuteCustomFunction)base.CompileNode();
            instance.functionName = CustomFunction.Name;
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