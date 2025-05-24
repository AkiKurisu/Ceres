using System;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Properties;
using Ceres.Utilities;

namespace Ceres.Editor.Graph.Flow.Properties
{
    [CustomNodeView(typeof(PropertyNode_PropertyValue), true)]
    public class PropertyNode_PropertyValueNodeView : PropertyNodeView
    {
        protected bool IsSelfTarget { get; private set; }
        
        protected bool IsStatic { get; private set; }
        
        public PropertyNode_PropertyValueNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {

        }

        public void SetPropertyFlags(bool isSelfTarget, bool isStatic)
        {
            var targetView = FindPortView("target");
            /* Validate self target in editor first */
            if (isSelfTarget && !GraphView.GetContainerType().IsAssignableTo(targetView.Binding.DisplayType.Value))
            {
                isSelfTarget = false;
            }
            IsSelfTarget = isSelfTarget;
            if (isSelfTarget)
            {
                targetView.Flags &= ~CeresPortViewFlags.ValidateConnection;
                targetView.HidePort();
            }
            else
            {
                targetView.Flags |= CeresPortViewFlags.ValidateConnection;
                targetView.ShowPort();
            }

            IsStatic = isStatic;
            if (isStatic)
            {
                NodeElement.AddToClassList("ConstNode");
            }
        }
        
        public override void SetNodeInstance(CeresNode ceresNode)
        {
            var propertyNode =(PropertyNode_PropertyValue)ceresNode;
            base.SetNodeInstance(ceresNode);
            SetPropertyFlags(propertyNode.isSelfTarget, propertyNode.isStatic);
        }
        
        public override ExecutableNode CompileNode()
        {
            var instance = (PropertyNode_PropertyValue)base.CompileNode();
            instance.isSelfTarget = IsSelfTarget;
            instance.isStatic = IsStatic;
            return instance;
        }
    }
}