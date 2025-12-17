using System;
using System.Collections.Generic;
using Ceres.Graph;
using Ceres.Graph.Flow;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph.Flow
{
    public class ExecutablePortArrayNodeViewResolver: INodeViewResolver
    {
        public ICeresNodeView CreateNodeView(Type type, CeresGraphView graphView)
        {
            return new ExecutablePortArrayNodeView(type, graphView);
        }

        public bool IsAcceptable(Type nodeType)
        {
            return nodeType.IsSubclassOf(typeof(ExecutableNode)) &&
                   typeof(IReadOnlyPortArrayNode).IsAssignableFrom(nodeType);
        }
    }
    
    [CustomNodeView(null)]
    public class ExecutablePortArrayNodeView: ExecutableNodeView
    {
        protected int PortLength { get; private set; }

        private readonly List<CeresPortView> _dynamicPortViews = new();

        /// <summary>
        /// Reflection data for <see cref="IPortArrayNode"/>
        /// </summary>
        protected PortArrayNodeReflection NodeReflection { get; }
        
        public ExecutablePortArrayNodeView(Type type, CeresGraphView graphView): base(type, graphView)
        {
            NodeReflection = PortArrayNodeReflection.Get(type);
            for (int i = 0; i < NodeReflection.DefaultArrayLength; i++)
            {
                AddPort(i);
            }
        }

        public override void SetNodeInstance(CeresNode ceresNode)
        {
            RemoveUnconnectedPorts();
            for (int i = 0; i < ((IReadOnlyPortArrayNode)ceresNode).GetPortArrayLength(); i++)
            {
                AddPort(i);
            }
            base.SetNodeInstance(ceresNode);
        }

        public override ExecutableNode CompileNode()
        {
            var nodeInstance = (ExecutableNode)Activator.CreateInstance(NodeType);
            if(nodeInstance is IPortArrayNode portArrayNode)
            {
                /* Allocate before commit */
                portArrayNode.SetPortArrayLength(PortLength);
            }
            FieldResolverInfos.ForEach(info => info.Resolver.Commit(nodeInstance));
            PortViews.ForEach(p => p.Commit(nodeInstance));
            nodeInstance.GraphPosition = NodeElement.GetPosition();
            nodeInstance.Guid = Guid;
            return nodeInstance;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            if (!NodeReflection.IsResizeable) return;
            
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Add new port", _ =>
            {
                AddPort(PortLength);
            }));
            if (PortLength > 0)
            {
                evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Remove last port", _ =>
                {
                    RemovePort(PortLength - 1);
                }));
            }
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Remove unconnected ports", _ =>
            {
                RemoveUnconnectedPorts();
            }));
            evt.menu.AppendSeparator();
        }

        protected void AddPort(int index)
        {
            var portData = CeresPortData.FromFieldInfo(NodeReflection.PortArrayField);
            portData.arrayIndex = index;
            PortLength++;
            var newPortView = PortViewFactory.CreateInstance(NodeReflection.PortArrayField, this, portData);
            AddPortView(newPortView);
            _dynamicPortViews.Add(newPortView);
            newPortView.SetDisplayName(GetPortArrayElementDisplayName(index));
        }

        protected void RemovePort(int index)
        {
            var portView = _dynamicPortViews[index];
            PortLength--;
            _dynamicPortViews.RemoveAt(index);
            RemovePortView(portView);
            
            /* Reorder */
            ReorderDynamicPorts();
        }

        protected void RemoveUnconnectedPorts()
        {
            for (int i = _dynamicPortViews.Count - 1; i >= 0; i--)
            {
                var portView = _dynamicPortViews[i];
                if (portView.PortElement.connected)
                {
                    continue;
                }
                PortLength--;
                _dynamicPortViews.RemoveAt(i);
                RemovePortView(portView);
            }
            /* Reorder */
            ReorderDynamicPorts();
        }

        protected void ReorderDynamicPorts()
        {
            for (int i = 0; i < PortLength; i++)
            {
                _dynamicPortViews[i].PortData.arrayIndex = i;
                _dynamicPortViews[i].SetDisplayName(GetPortArrayElementDisplayName(i));
            }
        }

        protected virtual string GetPortArrayElementDisplayName(int index)
        {
            return $"{NodeReflection.PortArrayLabel} {index}";
        }
    }
}