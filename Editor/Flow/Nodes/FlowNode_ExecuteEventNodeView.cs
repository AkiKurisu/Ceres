using System;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Utilities;
namespace Ceres.Editor.Graph.Flow
{
    /// <summary>
    /// Node view for <see cref="FlowNode_ExecuteEvent"/>
    /// </summary>
    [CustomNodeView(typeof(FlowNode_ExecuteEvent), true)]
    public sealed class FlowNode_ExecuteEventNodeView: ExecutableNodeView
    {
        private string _eventName;
        
        public FlowNode_ExecuteEventNodeView(Type type, CeresGraphView graphView)
        {
            Initialize(type, graphView);
            SetupNodeElement(new ExecutableNodeElement(this));
            FillDefaultNodeProperties();
            FillDefaultNodePorts();
        }

        public override void SetNodeInstance(CeresNode ceresNode)
        {
            var eventNode =(FlowNode_ExecuteEvent)ceresNode;
            base.SetNodeInstance(ceresNode);
            SetEventName(eventNode.eventName);
        }

        public void SetEventName(string inName)
        {
            _eventName = inName;
            NodeElement.title = "Execute " + _eventName;
        }

        public override ExecutableNode CompileNode()
        {
            var instance = (FlowNode_ExecuteEvent)base.CompileNode();
            instance.eventName = _eventName;
            return instance;
        }
    }
}