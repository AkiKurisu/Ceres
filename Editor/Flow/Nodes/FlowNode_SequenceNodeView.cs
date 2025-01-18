using System;
using System.Collections.Generic;
using System.Reflection;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Utilities;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph.Flow
{
    /// <summary>
    /// Node view for <see cref="FlowNode_Sequence"/>
    /// </summary>
    [CustomNodeView(typeof(FlowNode_Sequence))]
    public sealed class FlowNode_SequenceNodeView: ExecutableNodeView
    {
        private int _portIndex;

        private readonly FieldInfo _outputField;

        private readonly List<CeresPortView> _outputPortViews = new();
        
        public FlowNode_SequenceNodeView(Type type, CeresGraphView graphView): base(type, graphView)
        {
            _outputField = NodeType.GetField("outputs", BindingFlags.Instance | BindingFlags.Public);
        }

        public override void SetNodeInstance(CeresNode ceresNode)
        {
            _portIndex = Math.Max(1, ((FlowNode_Sequence)ceresNode).outputCount);
            for (int i = 0; i < _portIndex; i++)
            {
                AddPort(i);
            }
            base.SetNodeInstance(ceresNode);
        }

        public override ExecutableNode CompileNode()
        {
            var node = (FlowNode_Sequence)base.CompileNode();
            node.outputCount = _portIndex;
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
            var portData = CeresPortData.FromFieldInfo(_outputField);
            portData.arrayIndex = index;
            var newPortView = PortViewFactory.CreateInstance(_outputField, this, portData);
            AddPortView(newPortView);
            _outputPortViews.Add(newPortView);
            newPortView.SetDisplayName($"Then {index}");
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
                _outputPortViews[i].SetDisplayName($"Then {i}");
            }
        }
    }
}