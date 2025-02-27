using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.Annotations;
using Ceres.Graph.Flow;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph.Flow
{
    /// <summary>
    /// Base class for node visual element of <see cref="ExecutableNodeView"/>
    /// </summary>
    public class ExecutableNodeElement : Node
    {
        public ExecutableNodeView View { get; }
                
        private VisualElement _breakPoint;
        
        public ExecutableNodeElement(ExecutableNodeView nodeView)
        {
            View = nodeView;
            /* Additionally add node name as style class */
            tooltip = nodeView.GetDefaultTooltip();
            AddToClassList(GetClassName(nodeView.NodeType));
            styleSheets.Add(CeresGraphView.GetOrLoadStyleSheet("Ceres/Flow/ExecutableNodeElement"));
        }
        
        private static string GetClassName(Type type)
        {
            if (type.IsGenericType)
            {
                type = type.GetGenericTypeDefinition();
                return type.Name.Split('`')[0];
            }
            return type.Name;
        }
        
        public void AddBreakpointView()
        {
            _breakPoint?.RemoveFromHierarchy();
            var icon = Resources.Load<Texture2D>("Ceres/breakPoint");
            _breakPoint = new Button
            {
                name = "breakPoint", 
                style =
                {
                    backgroundImage = icon
                }
            };
            titleContainer.Add(_breakPoint);
        }

        public void RemoveBreakpointView()
        {
            _breakPoint?.RemoveFromHierarchy();
            _breakPoint = null;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target is not Node)
                return;
            if(_breakPoint == null)
            {
                evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Add breakpoint", (a) =>
                {
                    View.AddBreakpoint();
                }));
            }
            if(_breakPoint != null)
            {
                evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Remove breakpoint", (a) =>
                {
                    View.RemoveBreakpoint();
                }));
            }
            evt.menu.AppendSeparator();
            View.BuildContextualMenu(evt);
            base.BuildContextualMenu(evt);
            View.GraphView.ContextualMenuRegistry.BuildContextualMenu(ContextualMenuType.Node, evt, View.NodeType);
        }
        
                
        private void CollectConnectedEdges(HashSet<GraphElement> edgeSet)
        {
            /* Allow edges connected by port in titleContainer can be deleted */
            edgeSet.UnionWith(titleContainer.Children().OfType<Port>().SelectMany(c => c.connections).Where(d => (d.capabilities & Capabilities.Deletable) != 0));
            edgeSet.UnionWith(inputContainer.Children().OfType<Port>().SelectMany(c => c.connections).Where(d => (d.capabilities & Capabilities.Deletable) != 0));
            edgeSet.UnionWith(outputContainer.Children().OfType<Port>().SelectMany(c => c.connections).Where(d => (d.capabilities & Capabilities.Deletable) != 0));
        }

        public override void CollectElements(
            HashSet<GraphElement> collectedElementSet,
            Func<GraphElement, bool> conditionFunc)
        {
            CollectConnectedEdges(collectedElementSet);
        }
    }
    
    [Flags]
    public enum ExecutableNodeViewFlags
    {
        None = 0,
        /// <summary>
        /// Node view is no longer valid that need clean up
        /// </summary>
        Invalid = 1
    }
    
    /// <summary>
    /// Base class for node view of <see cref="ExecutableNode"/>
    /// </summary>
    [CustomNodeView(typeof(ExecutableNode), true)]
    public class ExecutableNodeView: CeresNodeView
    {
        public ExecutableNodeViewFlags Flags { get; internal set; }
        
        /// <summary>
        /// Get node visual element, see <see cref="ExecutableNodeElement"/>
        /// </summary>
        protected ExecutableNodeElement ExecutableNodeElement => (ExecutableNodeElement)NodeElement;

        /// <summary>
        /// Default constructor without initialization
        /// </summary>
        protected ExecutableNodeView()
        {
            
        }
        
        /// <summary>
        /// Constructor with fill default properties, ports and node visual element
        /// </summary>
        /// <param name="type"></param>
        /// <param name="graphView"></param>
        public ExecutableNodeView(Type type, CeresGraphView graphView)
        {
            Initialize(type, graphView);
            SetupNodeElement(new ExecutableNodeElement(this));
            FillDefaultNodeProperties();
            FillDefaultNodeTitle();
            FillDefaultNodePorts();
        }

        public virtual ExecutableNode CompileNode()
        {
            var nodeInstance = (ExecutableNode)Activator.CreateInstance(NodeType);
            FieldResolvers.ForEach(fieldResolver => fieldResolver.Commit(nodeInstance));
            PortViews.ForEach(portView => portView.Commit(nodeInstance));
            nodeInstance.GraphPosition = NodeElement.GetPosition();
            nodeInstance.Guid = Guid;
            return nodeInstance;
        }

        public void AddBreakpoint()
        {
            ((FlowGraphView)GraphView).DebugState.AddBreakpoint(Guid);
            ExecutableNodeElement.AddBreakpointView();
        }
        
        public void RemoveBreakpoint()
        {
            ((FlowGraphView)GraphView).DebugState.RemoveBreakpoint(Guid);
            ExecutableNodeElement.RemoveBreakpointView();
        }

        public string GetDefaultTooltip()
        {
            string tooltip = string.Empty;
            if (CeresSettings.DisplayDebug)
            {
                tooltip += NodeInfo.GetInfo(GetType());
                tooltip += '\n';
            }
            tooltip += NodeInfo.GetInfo(NodeType);
            return tooltip;
        }

        /// <summary>
        /// Add menu items to the node contextual menu.
        /// </summary>
        /// <param name="evt"></param>
        public virtual void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
        }

        /// <summary>
        /// Validate before compiling
        /// </summary>
        /// <param name="validator"></param>
        public virtual void Validate(FlowGraphValidator validator)
        {
            foreach (var portView in PortViews)
            {
                if (portView.Flags.HasFlag(CeresPortViewFlags.ValidateConnection) && !portView.PortElement.connections.Any())
                {
                    validator.MarkAsInvalid(this, $"{portView.Binding.DisplayName.Value} must be connected");
                }
            }
        }
    }
}