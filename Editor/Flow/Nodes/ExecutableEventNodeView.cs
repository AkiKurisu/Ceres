using System;
using System.Reflection;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Chris.Serialization;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph.Flow
{
    public abstract class ExecutableEventNodeView: ExecutableNodeView
    {
        protected MethodInfo MethodInfo { get; private set; }
        
        protected string MethodName { get; private set; }
        
        private StringResolver _eventNameResolver;

        private bool _editMode;
        
        public ExecutableEventNodeView(Type type, CeresGraphView graphView)
        {
            Initialize(type, graphView);
            SetupNodeElement(new ExecutableNodeElement(this));
            FillDefaultNodeProperties();
            FillNodeTitle();
            FillDefaultNodePorts();
        }

        public override void SetNodeInstance(CeresNode ceresNode)
        {
            var eventNode = (ExecutableEvent)ceresNode;
            base.SetNodeInstance(eventNode);
            /* Update title */
            UpdateEventTitle();

            
            if(eventNode.isImplementable)
            {
                var methodInfo = GetContainerType().GetMethod(eventNode.eventName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (methodInfo == null)
                {
                    Debug.LogWarning($"[Ceres] {eventNode.eventName} is not a implementable event of {GetContainerType().Name}");
                    return;
                }
                SetMethodInfo(methodInfo);
            }
        }

        protected virtual void UpdateEventTitle()
        {
            NodeElement.title = $"Event {_eventNameResolver.BaseField.value}";
        }
        
        public void SetMethodInfo(MethodInfo methodInfo)
        {
            Assert.IsNotNull(methodInfo);
            MethodInfo = methodInfo;
            MethodName = methodInfo.Name;
            NodeElement.AddToClassList("ImplementableEvent");
            SetEventName(MethodName);
            var titleLabel = NodeElement.Q<Label>("title-label", (string)null);
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
        }
        
        private void FillNodeTitle()
        {
            _eventNameResolver = FindFieldResolver<StringResolver>(nameof(ExecutionEvent.eventName));
            UpdateEventTitle();
            _eventNameResolver.RegisterValueChangeCallback(evt =>
            {
                UpdateEventTitle();
            });
            var titleLabel = NodeElement.Q<Label>("title-label", (string)null);
            titleLabel.RegisterCallback<MouseDownEvent>(OnClick);
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
            if(MethodInfo != null)
            {
                node.isImplementable = true;
            }
            return node;
        }

        protected abstract void FillMethodParameterPorts(MethodInfo methodInfo);
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

    [Ordered]
    [CustomNodeView(typeof(ExecutionEventUber), false)]
    public sealed class ExecutionEventUberNodeView : ExecutableEventNodeView
    {
        public ExecutionEventUberNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
            
        }

        protected override void UpdateEventTitle()
        {
            string label = "Event " + GetEventName();
            label += CeresNode.GetTargetSubtitle(GetContainerType().Name);
            NodeElement.title = label;
        }

        public override ExecutableNode CompileNode()
        {
            var node = (ExecutionEventUber)base.CompileNode();
            if(MethodInfo != null)
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
                var portData = new CeresPortData()
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