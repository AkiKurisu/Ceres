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

        private readonly List<CeresPortView> _outputPortViews = new();

        /// <summary>
        /// Reflection data for <see cref="IPortArrayNode"/>
        /// </summary>
        protected PortArrayNodeReflection NodeReflection { get; }
        
        protected ExecutablePortArrayNodeView(Type type, CeresGraphView graphView): base(type, graphView)
        {
            NodeReflection = PortArrayNodeReflection.Get(type);
            _portIndex = NodeReflection.DefaultArraySize;
            for (int i = 0; i < _portIndex; i++)
            {
                AddPort(i);
            }
        }

        public override void SetNodeInstance(CeresNode ceresNode)
        {
            RemoveUnconnectedPorts();
            _portIndex = ((IReadOnlyPortArrayNode)ceresNode).GetPortArraySize();
            for (int i = 0; i < _portIndex; i++)
            {
                AddPort(i);
            }
            base.SetNodeInstance(ceresNode);
        }

        public override ExecutableNode CompileNode()
        {
            var node = base.CompileNode();
            if(node is IPortArrayNode portArrayNode)
                portArrayNode.SetPortArraySize(_portIndex);
            return node;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Add new port", (a) =>
            {
                AddPort(_portIndex++);
            }));
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Remove unconnected ports", (a) =>
            {
                RemoveUnconnectedPorts();
            }));
        }

        private void AddPort(int index)
        {
            var portData = CeresPortData.FromFieldInfo(NodeReflection.PortArrayField);
            portData.arrayIndex = index;
            var newPortView = PortViewFactory.CreateInstance(NodeReflection.PortArrayField, this, portData);
            AddPortView(newPortView);
            _outputPortViews.Add(newPortView);
            newPortView.SetDisplayName(GetPortArrayElementDisplayName(index));
        }

        private void RemoveUnconnectedPorts()
        {
            for (int i = _outputPortViews.Count - 1; i >= 0; i--)
            {
                var portView = _outputPortViews[i];
                if (portView.PortElement.connected)
                {
                    continue;
                }
                _portIndex--;
                _outputPortViews.RemoveAt(i);
                RemovePortView(portView);
            }
            /* Reorder */
            for (int i = 0; i < _portIndex; i++)
            {
                _outputPortViews[i].PortData.arrayIndex = i;
                _outputPortViews[i].SetDisplayName(GetPortArrayElementDisplayName(i));
            }
        }

        protected virtual string GetPortArrayElementDisplayName(int index)
        {
            return $"{NodeReflection.PortArrayLabel} {index}";
        }
    }
}