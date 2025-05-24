using System;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Graph.Flow;

namespace Ceres.Editor.Graph.Flow.Properties
{
    [CustomNodeView(typeof(PropertyNode), true)]
    public class PropertyNodeView: ExecutableNodeView
    {
        protected string PropertyName { get; private set; }
        
        public PropertyNodeView(Type type, CeresGraphView graphView)
        {
            Initialize(type, graphView);
            SetupNodeElement(new ExecutableNodeElement(this));
            FillDefaultNodeProperties();
            FillDefaultNodePorts();
        }

        public override void SetNodeInstance(CeresNode ceresNode)
        {
            var propertyNode =(IPropertyNode)ceresNode;
            base.SetNodeInstance(ceresNode);
            SetPropertyName(propertyNode.GetPropertyName());
        }

        public virtual void SetPropertyName(string propertyName)
        {
            PropertyName = propertyName;
            var label = CeresLabel.GetLabel(NodeType);
            NodeElement.title = string.Format(label, propertyName);
        }

        public override ExecutableNode CompileNode()
        {
            var instance = base.CompileNode();
            ((IPropertyNode)instance).SetPropertyName(PropertyName);
            return instance;
        }
    }
}