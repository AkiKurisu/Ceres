using System;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Utilities;
using Ceres.Utilities;
using Chris.Serialization;
using UnityEngine.Assertions;
namespace Ceres.Editor.Graph.Flow
{
    public abstract class ExecuteFunctionNodeView : ExecutableNodeView
    {
        protected MethodInfo MethodInfo { get; private set; }
        
        protected string MethodName { get; private set; }
        
        protected int ParameterCount { get; private set; }

        protected bool IsStatic { get; private set; }
        
        protected bool IsScriptMethod { get; private set; }
        
        protected bool ExecuteInDependency { get; private set; }
        
        protected bool DisplayTarget { get; private set; }
        
        protected bool StaticIsSelfTarget { get; private set; }
        
        protected bool InstanceIsSelfTarget { get; private set; }
        
        protected Type ScriptTargetType { get; private set; }
        
        protected bool IsNeedResolveReturnType { get; private set; }
        
        protected ParameterInfo ResolveReturnTypeParameter { get; private set; }
        
        public void SetMethodInfo(MethodInfo methodInfo)
        {
            Assert.IsNotNull(methodInfo);
            MethodInfo = methodInfo;
            MethodName = methodInfo.Name;
            IsStatic = methodInfo.IsStatic;
            var attribute = ExecutableReflection.GetFunction(methodInfo).Attribute;
            IsScriptMethod = attribute.IsScriptMethod;
            ExecuteInDependency = attribute.ExecuteInDependency;
            DisplayTarget = attribute.DisplayTarget;
            StaticIsSelfTarget = attribute.IsSelfTarget && IsStatic;
            InstanceIsSelfTarget = GraphView.GetContainerType().IsAssignableTo(methodInfo.DeclaringType) && !IsStatic;
            ScriptTargetType = attribute.ScriptTargetType;
            IsNeedResolveReturnType = attribute.IsNeedResolveReturnType;
            ParameterCount = methodInfo.GetParameters().Length;
            SetNodeElementTitle(ExecutableFunction.GetFunctionName(methodInfo));
            FillMethodParametersPorts(methodInfo);
            
            if (IsNeedResolveReturnType)
            {
                ResolveReturnTypeParameter = attribute.ResolveReturnTypeParameter;
                RegisterMethodReturnPortValueChange();
                TryResolveMethodReturnType();
            }
            
            if (IsStatic)
            {
                NodeElement.AddToClassList("ConstNode");
                FindPortView("target").HidePort();
            }
            else if (InstanceIsSelfTarget)
            {
                InitializeTargetPortView("target");
            }
            
            if (ExecuteInDependency)
            {
                FindPortView("input").HidePort();
                FindPortView("exec").HidePort();
            }
        }

        private void RegisterMethodReturnPortValueChange()
        {
            var portView = FindPortViewWithDisplayName(CeresLabel.GetLabel(ResolveReturnTypeParameter.Name));
            var returnPortView = FindPortView("output");
            portView.FieldResolver.RegisterValueChangeCallback(x =>
            {
                var type = ((SerializedTypeBase)x).GetObjectType();
                returnPortView.SetDisplayType(type ?? returnPortView.PortData.GetValueType());
            });
        }

        protected void InitializeTargetPortView(string propertyName)
        {
            var portView = FindPortView(propertyName);
            portView.SetDisplayName("Self");
            portView.SetTooltip(" [Default is Self]");
            portView.FieldResolver?.RegisterValueChangeCallback(_ =>
            {
                RefreshTargetPortDisplayName();
            });
            portView.PortElement.RegisterCallback<PortConnectionChangeEvent>(_ =>
            {
                RefreshTargetPortDisplayName();
            });
            return;

            void RefreshTargetPortDisplayName()
            {
                var hasConnectionOrValue = portView.FieldResolver?.Value != null || portView.PortElement.connected;
                portView.SetDisplayName(hasConnectionOrValue ? "Target" : "Self");
            }
        }

        private void TryResolveMethodReturnType()
        {
            if (!IsNeedResolveReturnType) return;
            var portView = FindPortViewWithDisplayName(CeresLabel.GetLabel(ResolveReturnTypeParameter.Name));
            var returnPortView = FindPortView("output");
            var currentType = (portView.FieldResolver.Value as SerializedTypeBase)?.GetObjectType();
            returnPortView.SetDisplayType(currentType ?? returnPortView.PortData.GetValueType());
        }

        private void SetNodeElementTitle(string functionTitle)
        {
            var targetType = NodeType.GetGenericArguments()[0];
            if (MethodInfo != null)
            {
                targetType = ScriptTargetType ?? NodeType.GetGenericArguments()[0];
            }
            if(!IsStatic || DisplayTarget)
            {
                functionTitle += CeresNode.GetTargetSubtitle(targetType);
            }
            NodeElement.title = functionTitle;
            
            var tooltipText = GetDefaultTooltip();
            tooltipText += CeresNode.GetTargetSubtitle(targetType, false);
            NodeElement.tooltip = tooltipText;
        }
        
        public sealed override void SetNodeInstance(CeresNode ceresNode)
        {
            var functionNode =(FlowNode_ExecuteFunction)ceresNode;
            var methodInfo = functionNode.GetMethodInfo(NodeType.GetGenericArguments()[0]);
            if (methodInfo != null)
            {
                /* Validate arguments length is aligned */
                int parametersLength = methodInfo.GetParameters().Length;
                bool hasReturn = methodInfo.ReturnType != typeof(void);
                int argumentsLength = NodeType.GetGenericArguments().Length - (hasReturn ? 2 : 1);
                if (parametersLength != argumentsLength)
                {
                    methodInfo = null;
                    CeresAPI.LogWarning($"{functionNode.methodName} expect {parametersLength} arguments but get {argumentsLength}");
                }
            }
            else
            {
                CeresAPI.LogWarning($"{functionNode.methodName} is not an executable function of {NodeType.GetGenericArguments()[0].Name}");
            }
            
            if (methodInfo == null)
            {
                MethodName = functionNode.methodName;
                IsStatic = functionNode.isStatic;
                IsScriptMethod = functionNode.isScriptMethod;
                StaticIsSelfTarget = functionNode.isSelfTarget && IsStatic;
                InstanceIsSelfTarget = functionNode.isSelfTarget && !IsStatic;
                ExecuteInDependency = functionNode.executeInDependency;
                ParameterCount = functionNode.parameterCount;
                SetNodeElementTitle(functionNode.methodName);
            }
            else
            {
                SetMethodInfo(methodInfo);
            }
            base.SetNodeInstance(ceresNode);
            TryResolveMethodReturnType();
        }
        
        public override ExecutableNode CompileNode()
        {
            var instance = (FlowNode_ExecuteFunction)base.CompileNode();
            instance.methodName = MethodName;
            instance.isStatic = IsStatic;
            instance.isScriptMethod = IsScriptMethod;
            instance.isSelfTarget = StaticIsSelfTarget || InstanceIsSelfTarget;
            instance.executeInDependency = ExecuteInDependency;
            instance.parameterCount = ParameterCount;
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
            
            if (DisplayTarget)
            {
                FindPortView("inputs").SetDisplayName("Target");
            }
            if (StaticIsSelfTarget)
            {
                InitializeTargetPortView("inputs");
            }
            
            var output = methodInfo.ReturnParameter;
            if (output!.ParameterType == typeof(void)) return;
            
            var outputPortData = new CeresPortData
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
                portView?.SetDisplayDataFromParameterInfo(parameters[i]);
            }
            
            if (DisplayTarget)
            {
                FindPortView("input1").SetDisplayName("Target");
            }
            if (StaticIsSelfTarget)
            {
                InitializeTargetPortView("input1");
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
                portView?.SetDisplayDataFromParameterInfo(parameters[i]);
            }
            
            if (DisplayTarget)
            {
                FindPortView("input1").SetDisplayName("Target");
            }
            if (StaticIsSelfTarget)
            {
                InitializeTargetPortView("input1");
            }
        }
    }
}