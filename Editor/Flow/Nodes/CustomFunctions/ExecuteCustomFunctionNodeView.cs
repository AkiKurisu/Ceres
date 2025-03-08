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
        private bool UseLocalFunction { get; set; }
        
        private FlowGraphFunction FlowGraphFunction { get; set; }

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
            if (functionNode.functionAsset)
            {
                SetFlowGraphFunction(FlowGraphFunctionRegistry.Get().FindFlowGraphFunctionFromAsset(functionNode.functionAsset));
            }
            else
            {
                SetLocalFunction(functionNode.functionName);
            }
            base.SetNodeInstance(ceresNode);
        }

        public void SetLocalFunction(string localFunctionName)
        {
            UseLocalFunction = true;
            FunctionName = localFunctionName;
            NodeElement.title = localFunctionName + CeresNode.MakeSubtitle("Local Function");
            if (GraphView.TryGetSharedVariable(localFunctionName, out var variable) && variable is LocalFunction customFunction)
            {
                LocalFunction = customFunction;
                var inputParameters = FlowGraphEditorWindow.ResolveFunctionInputParameters(customFunction);
                for (int i = 0; i < inputParameters.Length; i++)
                {
                    var portView = FindPortView($"input{i + 1}");
                    portView.SetDisplayName(inputParameters[i].parameterName);
                }
                GraphView.Blackboard.RegisterCallback<VariableChangeEvent>(OnVariableChange);
                NodeElement.RegisterCallback<MouseDownEvent>(OnClickLocalFunction);
            }
        }

        private void OnClickLocalFunction(MouseDownEvent evt)
        {
            if (!UseLocalFunction || LocalFunction == null) return;
            if (evt.target != NodeElement || evt.clickCount < 2) return;
            
            FlowGraphEditorWindow.OpenSubgraphView(FunctionName);
        }
        
        private void OnClickFlowGraphFunction(MouseDownEvent evt)
        {
            if (UseLocalFunction || FlowGraphFunction == null || !FlowGraphFunction.Asset) return;
            if (evt.target != NodeElement || evt.clickCount < 2) return;
            
            FlowGraphEditorWindow.Show(FlowGraphFunction.Asset);
        }

        public void SetFlowGraphFunction(FlowGraphFunction flowGraphFunction)
        {
            UseLocalFunction = false;
            FlowGraphFunction = flowGraphFunction;
            if (flowGraphFunction.ContainerType == null)
            {
                NodeElement.title = flowGraphFunction.Asset.name;
            }
            else
            {
                NodeElement.title = flowGraphFunction.Asset.name + CeresNode.GetTargetSubtitle(flowGraphFunction.ContainerType);
            }
            FunctionName = flowGraphFunction.Asset.name;
            for (int i = 0; i < flowGraphFunction.InputParameterNames.Length; i++)
            {
                var portView = FindPortView($"input{i + 1}");
                portView.SetDisplayName(flowGraphFunction.InputParameterNames[i]);
            }
            NodeElement.RegisterCallback<MouseDownEvent>(OnClickFlowGraphFunction);
        }

        private void OnVariableChange(VariableChangeEvent evt)
        {
            if (NodeElement.panel == null) return;
            if (evt.Variable != LocalFunction) return;
            if (evt.ChangeType == VariableChangeType.Name)
            {
                FunctionName = evt.Variable.Name;
                NodeElement.title = FunctionName + CeresNode.MakeSubtitle("Local Function");
            }
            else if(evt.ChangeType == VariableChangeType.Remove)
            {
                CeresLogger.LogWarning($"Local function {evt.Variable.Name} was deleted, which may cause an error in the referenced ExecuteCustomFunctionNode during runtime. Please remove the corresponding node.");
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
            if (!UseLocalFunction || LocalFunction == null) return;
            
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Rename function", _ =>
            {
                GraphView.Blackboard.EditVariable(FunctionName);
            }));
        }

        public override void Validate(FlowGraphValidator validator)
        {
            base.Validate(validator);
            if (UseLocalFunction)
            {
                if (LocalFunction == null)
                {
                    validator.MarkAsInvalid(this, $"Can not find local function named {FunctionName}");
                    return;
                }
            }
            else
            {
                if (!FlowGraphFunction.Asset)
                {
                    validator.MarkAsInvalid(this, $"Flow graph function named {FunctionName} is missing");
                    return;
                }
            }

            Type returnType;
            Type[] inputTypes;
            if (UseLocalFunction)
            {
                (returnType, inputTypes) = FlowGraphEditorWindow.ResolveFunctionTypes(LocalFunction);
            }
            else
            {
                /* Function structure may be changed since we can edit multi graph at same time */
                var currentFunction = FlowGraphFunctionRegistry.Get()
                    .FindFlowGraphFunctionFromAsset(FlowGraphFunction.Asset);
                (returnType, inputTypes) = (currentFunction.ReturnType, currentFunction.InputTypes);
            }
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
            if (UseLocalFunction)
            {
                instance.functionName = LocalFunction.Name;
            }
            else
            {
                instance.functionAsset = FlowGraphFunction.Asset;
            }
            return instance;
        }
    }
}