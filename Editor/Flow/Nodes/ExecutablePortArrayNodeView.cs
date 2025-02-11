using System;
using System.Collections.Generic;
using Ceres.Graph;
using Ceres.Graph.Flow;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph.Flow
{
    public abstract class ExecutablePortArrayNodeView: ExecutableNodeView
    {
        private int _portIndex;

        private readonly List<CeresPortView> _dynamicPortViews = new();

        /// <summary>
        /// Reflection data for <see cref="IPortArrayNode"/>
        /// </summary>
        protected PortArrayNodeReflection NodeReflection { get; }
        
        protected ExecutablePortArrayNodeView(Type type, CeresGraphView graphView): base(type, graphView)
        {
            NodeReflection = PortArrayNodeReflection.Get(type);
            _portIndex = NodeReflection.DefaultArrayLength;
            for (int i = 0; i < _portIndex; i++)
            {
                AddPort(i);
            }
        }

        public override void SetNodeInstance(CeresNode ceresNode)
        {
            RemoveUnconnectedPorts();
            _portIndex = ((IReadOnlyPortArrayNode)ceresNode).GetPortArrayLength();
            for (int i = 0; i < _portIndex; i++)
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
                portArrayNode.SetPortArrayLength(_portIndex);
            }
            FieldResolvers.ForEach(r => r.Commit(nodeInstance));
            PortViews.ForEach(p => p.Commit(nodeInstance));
            nodeInstance.GraphPosition = NodeElement.GetPosition();
            nodeInstance.Guid = Guid;
            return nodeInstance;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Add new port", _ =>
            {
                AddPort(_portIndex++);
            }));
            if (_portIndex > 0)
            {
                evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Remove last port", _ =>
                {
                    RemovePort(_portIndex - 1);
                }));
            }
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Remove unconnected ports", _ =>
            {
                RemoveUnconnectedPorts();
            }));
            evt.menu.AppendSeparator();
        }

        private void AddPort(int index)
        {
            var portData = CeresPortData.FromFieldInfo(NodeReflection.PortArrayField);
            portData.arrayIndex = index;
            var newPortView = PortViewFactory.CreateInstance(NodeReflection.PortArrayField, this, portData);
            AddPortView(newPortView);
            _dynamicPortViews.Add(newPortView);
            newPortView.SetDisplayName(GetPortArrayElementDisplayName(index));
        }

        private void RemovePort(int index)
        {
            var portView = _dynamicPortViews[index];
            _portIndex--;
            _dynamicPortViews.RemoveAt(index);
            RemovePortView(portView);
            
            /* Reorder */
            ReorderDynamicPorts();
        }

        private void RemoveUnconnectedPorts()
        {
            for (int i = _dynamicPortViews.Count - 1; i >= 0; i--)
            {
                var portView = _dynamicPortViews[i];
                if (portView.PortElement.connected)
                {
                    continue;
                }
                _portIndex--;
                _dynamicPortViews.RemoveAt(i);
                RemovePortView(portView);
            }
            /* Reorder */
            ReorderDynamicPorts();
        }

        private void ReorderDynamicPorts()
        {
            for (int i = 0; i < _portIndex; i++)
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