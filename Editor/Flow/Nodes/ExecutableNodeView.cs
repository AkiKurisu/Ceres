using System;
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
            tooltip = NodeInfo.GetInfo(nodeView.NodeType);
            AddToClassList(NodeInfo.GetClassName(nodeView.NodeType));
            styleSheets.Add(CeresGraphView.GetOrLoadStyleSheet("Ceres/Flow/ExecutableNodeElement"));
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
            if(_breakPoint == null)
            {
                evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Add Breakpoint", (a) =>
                {
                    View.AddBreakpoint();
                }));
            }
            if(_breakPoint != null)
            {
                evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Remove Breakpoint", (a) =>
                {
                    View.RemoveBreakpoint();
                }));
            }
            base.BuildContextualMenu(evt);
        }
    }
    
    /// <summary>
    /// Base class for node view of <see cref="ExecutableNode"/>
    /// </summary>
    public class ExecutableNodeView: CeresNodeView
    {
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
        /// Constructor with fill default properties, ports and node visual element+
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
            FieldResolvers.ForEach(r => r.Commit(nodeInstance));
            PortViews.ForEach(p => p.Commit(nodeInstance));
            nodeInstance.GraphPosition = NodeElement.GetPosition();
            nodeInstance.Guid = Guid;
            return nodeInstance;
        }

        public void AddBreakpoint()
        {
            ((FlowGraphView)GraphView).DebugState.AddBreakpoint(Guid);
            ((ExecutableNodeElement)NodeElement).AddBreakpointView();
        }
        
        public void RemoveBreakpoint()
        {
            ((FlowGraphView)GraphView).DebugState.RemoveBreakpoint(Guid);
            ((ExecutableNodeElement)NodeElement).RemoveBreakpointView();
        }
    }
}