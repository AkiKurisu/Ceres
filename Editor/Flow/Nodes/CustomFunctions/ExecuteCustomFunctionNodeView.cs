using System;
using System.Linq;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.CustomFunctions;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph.Flow.CustomFunctions
{
    [CustomNodeView(typeof(FlowNode_ExecuteCustomFunction), true)]
    public sealed class ExecuteCustomFunctionNodeView : ExecutableNodeView
    {
        private LocalFunction LocalFunction { get; set; }

        private string FunctionName { get; set; }

        public ExecuteCustomFunctionNodeView(Type type, CeresGraphView graphView)
        {
            Initialize(type, graphView);
            SetupNodeElement(new ExecutableNodeElement(this));
            FillDefaultNodeProperties();
            FillDefaultNodePorts();
        }
        
        public override void SetNodeInstance(CeresNode ceresNode)
        {
            var functionNode =(FlowNode_ExecuteCustomFunction)ceresNode;
            SetFunctionName(functionNode.functionName);
            base.SetNodeInstance(ceresNode);
        }

        public void SetFunctionName(string functionName)
        {
            NodeElement.title = functionName;
            FunctionName = functionName;
            if (GraphView.TryGetSharedVariable(functionName, out var variable) && variable is LocalFunction customFunction)
            {
                LocalFunction = customFunction;
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
            if (NodeElement.panel == null) return;
            if (evt.Variable != LocalFunction) return;
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
                LocalFunction = null;
            }
        }
        
        private void FrameNode()
        {
            GraphView.AddToSelection(NodeElement);
            GraphView.FrameSelection();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            if (LocalFunction == null) return;
            
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Rename function", _ =>
            {
                GraphView.Blackboard.EditVariable(FunctionName);
            }));
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Edit function", _ =>
            {
                FlowGraphEditorWindow.OpenSubgraphView(FunctionName);
            }));
        }

        public override void Validate(FlowGraphValidator validator)
        {
            base.Validate(validator);
            if (LocalFunction == null)
            {
                validator.MarkAsInvalid(this, $"Can not find function {FunctionName}");
                return;
            }
            
            var (returnType, inputTypes) = FlowGraphEditorWindow.ResolveFunctionTypes(LocalFunction);
            var definitionType = ExecutableNodeReflectionHelper.PredictCustomFunctionNodeType(returnType, inputTypes);
            Type targetNodeType;
            if (returnType == typeof(void))
            {
                targetNodeType = definitionType.MakeGenericType(inputTypes);
            }
            else
            {
                targetNodeType = definitionType.MakeGenericType(inputTypes.Append(returnType).ToArray());
            }
            if (targetNodeType != NodeType)
            {
                validator.MarkAsInvalid(this, $"Parameters of function {FunctionName} do not match");
            }
        }

        public override ExecutableNode CompileNode()
        {
            var instance = (FlowNode_ExecuteCustomFunction)base.CompileNode();
            instance.functionName = LocalFunction.Name;
            return instance;
        }
    }
}