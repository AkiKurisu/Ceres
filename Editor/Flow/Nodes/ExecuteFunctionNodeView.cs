using System;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Utilities;
using Chris.Serialization;
using UnityEngine;
using UnityEngine.Assertions;
namespace Ceres.Editor.Graph.Flow
{
    public abstract class ExecuteFunctionNodeView : ExecutableNodeView
    {
        protected MethodInfo MethodInfo { get; private set; }
        
        protected string MethodName { get; private set; }

        protected bool IsStatic { get; private set; }
        
        protected bool IsScriptMethod { get; private set; }
        
        protected bool DisplayTarget { get; private set; }
        
        protected bool IsSelfTarget { get; private set; }
        
        public void SetMethodInfo(MethodInfo methodInfo)
        {
            Assert.IsNotNull(methodInfo);
            MethodInfo = methodInfo;
            MethodName = methodInfo.Name;
            IsStatic = methodInfo.IsStatic;
            IsScriptMethod = ExecutableFunctionRegistry.IsScriptMethod(methodInfo);
            DisplayTarget = ExecutableFunctionRegistry.CanDisplayTarget(methodInfo);
            IsSelfTarget = ExecutableFunctionRegistry.IsSelfTarget(methodInfo);
            SetNodeElementTitle(ExecutableFunctionRegistry.GetFunctionName(methodInfo));
            FillMethodParametersPorts(methodInfo);
            if (ExecutableFunctionRegistry.IsNeedResolveReturnType(methodInfo))
            {
                ResolveMethodReturnPort(methodInfo);
            }
            if (IsStatic)
            {
                NodeElement.AddToClassList("ConstNode");
            }
        }

        private void ResolveMethodReturnPort(MethodInfo methodInfo)
        {
            var resolveParameter = ExecutableFunctionRegistry.GetResolveReturnTypeParameter(methodInfo);
            var portView = FindPortViewWithDisplayName(CeresLabel.GetLabel(resolveParameter.Name));
            var returnPortView = FindPortView("output");
            var currentType = (portView.FieldResolver.Value as SerializedTypeBase)?.GetObjectType();
            returnPortView.SetPortDisplayType(currentType ?? returnPortView.PortData.GetValueType());
            portView.FieldResolver.RegisterValueChangeCallback(x =>
            {
                var type = ((SerializedTypeBase)x).GetObjectType();
                returnPortView.SetPortDisplayType(type ?? returnPortView.PortData.GetValueType());
            });
        }

        private void SetNodeElementTitle(string functionTitle)
        {
            var targetType = NodeType.GetGenericArguments()[0];
            if (MethodInfo != null)
            {
                targetType = ExecutableFunctionRegistry.GetTargetType(MethodInfo) ?? NodeType.GetGenericArguments()[0];
            }
            if(!IsStatic || DisplayTarget)
            {
                functionTitle += CeresNode.GetTargetSubtitle(targetType);
            }
            NodeElement.title = functionTitle;
            
            var tooltipText = NodeInfo.GetInfo(NodeType);
            tooltipText += CeresNode.GetTargetSubtitle(targetType, false);
            NodeElement.tooltip = tooltipText;
        }
        
        public sealed override void SetNodeInstance(CeresNode ceresNode)
        {
            var eventNode =(FlowNode_ExecuteFunction)ceresNode;
            base.SetNodeInstance(ceresNode);
            var methodInfo = eventNode.GetExecuteFunction(NodeType.GetGenericArguments()[0]);
            if (methodInfo == null)
            {
                Debug.LogWarning($"[Ceres] {eventNode.methodName} is not an executable function of {NodeType.GetGenericArguments()[0].Name}");
                MethodName = eventNode.methodName;
                SetNodeElementTitle(eventNode.methodName);
                return;
            }
            SetMethodInfo(methodInfo);
        }
        
        public override ExecutableNode CompileNode()
        {
            var instance = (FlowNode_ExecuteFunction)base.CompileNode();
            instance.methodName = MethodName;
            instance.isStatic = IsStatic;
            instance.isScriptMethod = IsScriptMethod;
            instance.isSelfTarget = IsSelfTarget;
            return instance;
        }

        protected abstract void FillMethodParametersPorts(MethodInfo methodInfo);
    }

    [CustomNodeView(typeof(FlowNode_ExecuteFunctionUber), true)]
    public sealed class FlowNode_ExecuteFunctionUberNodeView: ExecuteFunctionNodeView
    {
        public FlowNode_ExecuteFunctionUberNodeView(Type type, CeresGraphView graphView)
        {
            Initialize(type, graphView);
            SetupNodeElement(new ExecutableNodeElement(this));
            FillDefaultNodeProperties();
            FillDefaultNodePorts();
        }
        
        protected override void FillMethodParametersPorts(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                return;
            }
            var inputField = NodeType.GetField("inputs", BindingFlags.Instance | BindingFlags.Public);
            var outputField = NodeType.GetField("outputs", BindingFlags.Instance | BindingFlags.Public);
            var parameters = methodInfo!.GetParameters();
            for(int i = 0; i < parameters.Length; ++i)
            { 
                var portData = new CeresPortData()
                {
                    /* Remap to actual property */
                    propertyName = "inputs",
                    type = SerializedType.ToString(parameters[i].ParameterType),
                    connections = Array.Empty<PortConnectionData>(),
                    arrayIndex = i
                };
                AddPortView(PortViewFactory.CreateInstance(inputField, parameters[i], this, portData));
            }
            
            if(IsStatic)
            {
                FindPortView("target").HidePort();
            }
            if (DisplayTarget)
            {
                FindPortView("inputs", 0).SetPortDisplayName("Target");
            }
            if (IsSelfTarget)
            {
                FindPortView("inputs", 0).SetPortTooltip(" [Default is Self]");
            }
            
            var output = methodInfo.ReturnParameter;
            if (output!.ParameterType == typeof(void)) return;
            
            var outputPortData = new CeresPortData()
            {
                /* Remap to actual property */
                propertyName = "outputs",
                type = SerializedType.ToString(output.ParameterType),
                connections = Array.Empty<PortConnectionData>(),
                arrayIndex = 0
            };
            AddPortView(PortViewFactory.CreateInstance(outputField, output, this, outputPortData));
        }
    }
    
    [CustomNodeView(typeof(FlowNode_ExecuteFunctionVoid), true)]
    public sealed class FlowNode_ExecuteFunctionVoidNodeView: ExecuteFunctionNodeView
    {
        public FlowNode_ExecuteFunctionVoidNodeView(Type type, CeresGraphView graphView)
        {
            Initialize(type, graphView);
            SetupNodeElement(new ExecutableNodeElement(this));
            FillDefaultNodeProperties();
            FillDefaultNodePorts();
        }
        

        protected override void FillMethodParametersPorts(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                return;
            }
            var parameters = methodInfo.GetParameters();
            var arguments = NodeType.GetGenericArguments()[1..];
            for (int i = 0; i < arguments.Length; i++)
            {
                var portView = FindPortView($"input{i + 1}");
                portView?.SetPortDisplayName(parameters[i].Name);
            }
            
            if(IsStatic)
            {
                FindPortView("target").HidePort();
            }
            if (DisplayTarget)
            {
                FindPortView("input1").SetPortDisplayName("Target");
            }
            if (IsSelfTarget)
            {
                FindPortView("input1").SetPortTooltip(" [Default is Self]");
            }
        }
    }
    
    [CustomNodeView(typeof(FlowNode_ExecuteFunctionReturn), true)]
    public sealed class FlowNode_ExecuteFunctionReturnNodeView: ExecuteFunctionNodeView
    {
        public FlowNode_ExecuteFunctionReturnNodeView(Type type, CeresGraphView graphView)
        {
            Initialize(type, graphView);
            SetupNodeElement(new ExecutableNodeElement(this));
            FillDefaultNodeProperties();
            FillDefaultNodePorts();
        }
        
        protected override void FillMethodParametersPorts(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                return;
            }
            var parameters = methodInfo.GetParameters();
            var arguments = NodeType.GetGenericArguments()[1..];
            for (int i = 0; i < arguments.Length; i++)
            {
                var portView = FindPortView($"input{i + 1}");
                portView?.SetPortDisplayName(parameters[i].Name);
            }
            
            if(IsStatic)
            {
                FindPortView("target").HidePort();
            }
            if (DisplayTarget)
            {
                FindPortView("input1").SetPortDisplayName("Target");
            }
            if (IsSelfTarget)
            {
                FindPortView("input1").SetPortTooltip(" [Default is Self]");
            }
        }
    }
}