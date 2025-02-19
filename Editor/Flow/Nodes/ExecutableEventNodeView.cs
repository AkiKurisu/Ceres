using System;
using System.Linq;
using System.Reflection;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Chris.Serialization;
using UnityEditor.Experimental.GraphView;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph.Flow
{
    public abstract class ExecutableEventNodeView: ExecutableNodeView
    {
        protected MethodInfo MethodInfo { get; private set; }
        
        protected bool IsImplementable { get; private set; }
        
        protected string MethodName { get; private set; }
        
        private StringResolver _eventNameResolver;

        private bool _editMode;

        protected ExecutableEventNodeView(Type type, CeresGraphView graphView)
        {
            Initialize(type, graphView);
            SetupNodeElement(new ExecutableNodeElement(this));
            FillDefaultNodeProperties();
            FillNodeTitle();
            FillDefaultNodePorts();
        }

        protected virtual bool CanRename()
        {
            return true;
        }

        public override void SetNodeInstance(CeresNode ceresNode)
        {
            var eventNode = (ExecutableEvent)ceresNode;
            base.SetNodeInstance(eventNode);
            IsImplementable = eventNode.isImplementable;
            if (IsImplementable)
            {
                var methodInfo = GetContainerType().GetMethod(eventNode.eventName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (methodInfo != null)
                {
                    SetMethodInfo(methodInfo);
                    UpdateEventTitle();
                    return;
                }
                /* Change to normal execution event and validate event name */
                CeresLogger.LogWarning($"{eventNode.eventName} is not an implementable event of {GetContainerType().Name}");
                IsImplementable = false;
            }
            ValidateEventName();
            UpdateEventTitle();
        }

        private void ValidateEventName()
        {
            if (!CanRename()) return;
            
            var existEventNodes = GraphView.NodeViews
                .OfType<ExecutableEventNodeView>()
                .Except(new [] { this })
                .ToArray();
            var eventName = GetEventName();
            var newEventName = eventName;
            int i = 0;
            while (existEventNodes.Any(node => node.GetEventName() == newEventName))
            {
                newEventName = $"{eventName} {++i}";
            }

            if (newEventName != eventName)
            {
                _eventNameResolver.Value = newEventName;
            }
        }
        
        protected virtual void UpdateEventTitle()
        {
            var label = "Event " + GetEventName();
            if(IsImplementable)
            {
                label += CeresNode.GetTargetSubtitle(GetContainerType().Name);
            }
            NodeElement.title = label;
        }
        
        public void SetMethodInfo(MethodInfo methodInfo)
        {
            Assert.IsNotNull(methodInfo);
            MethodInfo = methodInfo;
            IsImplementable = true;
            MethodName = methodInfo.Name;
            NodeElement.AddToClassList("ImplementableEvent");
            SetEventName(MethodName);
            var titleLabel = NodeElement.Q<Label>("title-label");
            titleLabel.UnregisterCallback<MouseDownEvent>(OnClick);
            FillMethodParameterPorts(methodInfo);
        }

        public string GetEventName()
        {
            return _eventNameResolver.BaseField.value;
        }
        
        public void SetEventName(string eventName)
        {
            _eventNameResolver.Value = eventName;
            ValidateEventName();
        }
        
        private void FillNodeTitle()
        {
            _eventNameResolver = FindFieldResolver<StringResolver>(nameof(ExecutionEvent.eventName));
            UpdateEventTitle();
            if (CanRename())
            {
                _eventNameResolver.RegisterValueChangeCallback(_ => { UpdateEventTitle(); });
                var titleLabel = NodeElement.Q<Label>("title-label");
                titleLabel.RegisterCallback<MouseDownEvent>(OnClick);
            }
            _eventNameResolver.BaseField.style.display = DisplayStyle.None;
        }

        private void OnClick(MouseDownEvent evt)
        {
            if (evt.clickCount < 2) return;
            if (_editMode) return;
            
            _eventNameResolver.BaseField.style.display = DisplayStyle.Flex;
            _eventNameResolver.EditorField.Focus();
            _editMode = true;
            evt.StopPropagation();
            GraphView.contentContainer.RegisterCallback<MouseDownEvent>(OnRelease);
        }

        private void OnRelease(MouseDownEvent evt)
        {
            /* Exit edit mode when click any other node */
            if (evt.target is VisualElement element && element.GetFirstAncestorOfType<Node>() == NodeElement)
            {
                return;
            }
            GraphView.contentContainer.UnregisterCallback<MouseDownEvent>(OnRelease);
            _eventNameResolver.BaseField.style.display = DisplayStyle.None;
            _editMode = false;
            evt.StopPropagation();
        }
        
        public override void AddPortView(CeresPortView portView)
        {
            if (portView.PortData.GetValueType().IsSubclassOf(typeof(EventDelegateBase)))
            {
                NodeElement.titleContainer.Add(portView.PortElement);
            }
            else if (portView.PortElement.direction == Direction.Input)
            {
                NodeElement.inputContainer.Add(portView.PortElement);
            }
            else
            {
                NodeElement.outputContainer.Add(portView.PortElement);
            }
            PortViews.Add(portView);
        }
        
        public override ExecutableNode CompileNode()
        {
            var node = (ExecutableEvent)base.CompileNode();
            node.isImplementable = IsImplementable;
            return node;
        }

        protected virtual void FillMethodParameterPorts(MethodInfo methodInfo)
        {
            
        }
    }
    
    [CustomNodeView(typeof(ExecutableEvent), true)]
    public class ExecutionEventNodeView: ExecutableEventNodeView
    {
        public ExecutionEventNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
        }
        
        protected override void FillMethodParameterPorts(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                return;
            }
            var parameters = methodInfo.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                var portView = FindPortView($"output{i + 1}");
                portView?.SetDisplayName(parameters[i].Name);
            }
        }
    }
    
    [CustomNodeView(typeof(GeneratedExecutableEvent), true)]
    public class GeneratedExecutableEventNodeView: ExecutableEventNodeView
    {
        public GeneratedExecutableEventNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
        }

        protected override void UpdateEventTitle()
        {
            var label = "Event " + GetEventName()[(nameof(ExecutableEvent).Length + 1)..];
            label += $"\n<color=#414141><size=10>Custom Event</size></color>";
            NodeElement.title = label;
        }
        
        protected override bool CanRename()
        {
            return false;
        }
    }
    
    [CustomNodeView(typeof(ExecutionEventUber), false)]
    public sealed class ExecutionEventUberNodeView : ExecutableEventNodeView
    {
        public ExecutionEventUberNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
            
        }

        public override ExecutableNode CompileNode()
        {
            var node = (ExecutionEventUber)base.CompileNode();
            if (MethodInfo != null)
            {
                /* Skip compile if method not exist */
                node.argumentCount = MethodInfo.GetParameters().Length;
            }
            return node;
        }

        protected override void FillMethodParameterPorts(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                return;
            }
            var outputField = NodeType.GetField("outputs", BindingFlags.Instance | BindingFlags.Public);
            var parameters = methodInfo.GetParameters();
            for(int i = 0; i < parameters.Length; ++i)
            { 
                var portData = new CeresPortData
                {
                    /* Remap to actual property */
                    propertyName = "outputs",
                    type = SerializedType.ToString(parameters[i].ParameterType),
                    connections = Array.Empty<PortConnectionData>(),
                    arrayIndex = i
                };
                AddPortView(PortViewFactory.CreateInstance(outputField, parameters[i], this, portData));
            }
        }
    }
}