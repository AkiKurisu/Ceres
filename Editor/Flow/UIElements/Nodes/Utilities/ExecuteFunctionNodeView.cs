using System;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Utilities;
using Ceres.Utilities;
using Chris.Serialization;
using Unity.CodeEditor;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph.Flow.Utilities
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
            InstanceIsSelfTarget = !IsStatic && GraphView.GetContainerType().IsAssignableTo(methodInfo.DeclaringType);
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
                InitializeSelfTargetPortView("target");
            }
            else
            {
                FindPortView("target").Flags |= CeresPortViewFlags.ValidateConnection;
            }
            
            if (ExecuteInDependency)
            {
                FindPortView("input").HidePort();
                FindPortView("exec").HidePort();
            }
            
            NodeElement.RegisterCallback<MouseDownEvent>(OnClickExecuteFunction);
        }

        private void OnClickExecuteFunction(MouseDownEvent evt)
        {
            if (MethodInfo == null) return;
            if (evt.clickCount < 2) return;
            
            var (filePath, lineNumber) = ExecutableReflectionEditorUtils.GetExecutableFunctionFileInfo(MethodInfo);
            if (string.IsNullOrEmpty(filePath)) return;

            CodeEditor.Editor.CurrentCodeEditor.OpenProject(filePath, lineNumber);
            evt.StopImmediatePropagation();
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

        /// <summary>
        /// Notify port view to treat compiled port can be unconnected which will pass self reference at runtime
        /// </summary>
        /// <param name="propertyName">Port view bound property name</param>
        protected void InitializeSelfTargetPortView(string propertyName)
        {
            var portView = FindPortView(propertyName);
            Assert.IsNotNull(portView);
            /* Validate self target in editor first */
            if (!GraphView.GetContainerType().IsAssignableTo(portView.Binding.DisplayType.Value))
            {
                /* Target should be connected in order to prevent null reference exception */
                portView.Flags |= CeresPortViewFlags.ValidateConnection;
                return;
            }
            
            portView.Flags &= ~CeresPortViewFlags.ValidateConnection;
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
            
            var tooltipText = GetFunctionNodeTooltip();
            tooltipText += CeresNode.GetTargetSubtitle(targetType, false);
            SetTooltip(tooltipText);
        }

        private string GetFunctionNodeTooltip()
        {
            if (MethodInfo == null) return GetDefaultTooltip();
            
            var tooltip = ExecutableReflectionEditorUtils.GetExecutableFunctionXmlDocumentation(MethodInfo);
            return string.IsNullOrEmpty(tooltip) ? GetDefaultTooltip() : tooltip;
        }
        
        public sealed override void SetNodeInstance(CeresNode ceresNode)
        {
            var functionNode =(FlowNode_ExecuteFunction)ceresNode;
            MethodInfo methodInfo;
            try
            {
                methodInfo = functionNode.GetMethodInfo(NodeType.GetGenericArguments()[0]);
            }
            catch (InvalidExecutableFunctionException)
            {
                methodInfo = null;
            }
            
            if (methodInfo != null)
            {
                /* Validate arguments length is aligned */
                int parametersLength = methodInfo.GetParameters().Length;
                bool hasReturn = methodInfo.ReturnType != typeof(void);
                int argumentsLength = NodeType.GetGenericArguments().Length - (hasReturn ? 2 : 1);
                if (parametersLength != argumentsLength)
                {
                    methodInfo = null;
                    CeresLogger.LogWarning($"{functionNode.methodName} expect {parametersLength} arguments but get {argumentsLength}");
                }
            }
            else
            {
                CeresLogger.LogWarning($"{functionNode.methodName} is not an executable function of {NodeType.GetGenericArguments()[0].Name}");
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
                SetNodeElementTitle(functionNode.methodName + " <color=#FFE000>[Invalid Function]</size></color>");
                NodeElement.tooltip = $"The presence of this node indicates that the executable function {functionNode.methodName} bound to this node is invalid now.";
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

        public override void Validate(FlowGraphValidator validator)
        {
            base.Validate(validator);
            if (MethodInfo == null)
            {
                validator.MarkAsInvalid(this, $"{MethodName} is not an executable function of {NodeType.GetGenericArguments()[0].Name}");
            }
        }

        protected abstract void FillMethodParametersPorts(MethodInfo methodInfo);
        
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            if (MethodInfo == null) return;
            var (filePath, lineNumber) = ExecutableReflectionEditorUtils.GetExecutableFunctionFileInfo(MethodInfo);
            if (string.IsNullOrEmpty(filePath)) return;
            
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Open in IDE", _ =>
            {
                CodeEditor.Editor.CurrentCodeEditor.OpenProject(filePath, lineNumber);
            }));
            evt.menu.AppendSeparator();
        }
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
                var portData = new CeresPortData
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
                var targetView = FindPortView("inputs");
                targetView.SetDisplayName("Target");
                targetView.Flags |= CeresPortViewFlags.ValidateConnection;
            }
            if (StaticIsSelfTarget)
            {
                InitializeSelfTargetPortView("inputs");
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
                var targetView = FindPortView("input1");
                targetView.SetDisplayName("Target");
                targetView.Flags |= CeresPortViewFlags.ValidateConnection;
            }
            if (StaticIsSelfTarget)
            {
                InitializeSelfTargetPortView("input1");
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
                var targetView = FindPortView("input1");
                targetView.SetDisplayName("Target");
                targetView.Flags |= CeresPortViewFlags.ValidateConnection;
            }
            if (StaticIsSelfTarget)
            {
                InitializeSelfTargetPortView("input1");
            }
        }
    }
}