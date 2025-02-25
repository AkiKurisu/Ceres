using System;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Properties;
using Ceres.Utilities;

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

    [CustomNodeView(typeof(PropertyNode_SharedVariableValue), true)]
    public class PropertyNode_SharedVariableValueNodeView : PropertyNodeView
    {
        public PropertyNode_SharedVariableValueNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
            GraphView.Blackboard.RegisterCallback<VariableChangeEvent>(OnVariableChange);
        }
        
        private SharedVariable _boundVariable;
        
        public override void SetPropertyName(string propertyName)
        {
            base.SetPropertyName(propertyName);
            _boundVariable =  GraphView.Blackboard.GetSharedVariable(propertyName);
        }

        private void OnVariableChange(VariableChangeEvent evt)
        {
            if (NodeElement.panel == null) return;
            if(evt.Variable != _boundVariable) return;
            if(evt.ChangeType == VariableChangeType.Name)
            {
                SetPropertyName(evt.Variable.Name);
            }
            else if(evt.ChangeType == VariableChangeType.Type)
            {
                CeresLogger.LogWarning($"The variable type of {evt.Variable.Name} has changed, which may cause an error in the referenced PropertyNode during runtime. Please recreate the corresponding node.");
                GraphView.ClearSelection();
                GraphView.schedule.Execute(FrameNode).ExecuteLater(200);
            }
            else if(evt.ChangeType == VariableChangeType.Remove)
            {
                CeresLogger.LogWarning($"The variable {evt.Variable.Name} was deleted, which may cause an error in the referenced PropertyNode during runtime. Please remove the corresponding node.");
                GraphView.ClearSelection();
                GraphView.schedule.Execute(FrameNode).ExecuteLater(200);
            }
        }

        private void FrameNode()
        {
            GraphView.AddToSelection(NodeElement);
            GraphView.FrameSelection();
        }
    }
    
    [CustomNodeView(typeof(PropertyNode_PropertyValue), true)]
    public class PropertyNode_PropertyValueNodeView : PropertyNodeView
    {
        protected bool IsSelfTarget { get; private set; }
        
        public PropertyNode_PropertyValueNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
            var targetView = FindPortView("target");
            /* Validate self target in editor first */
            if (!GraphView.GetContainerType().IsAssignableTo(targetView.Binding.DisplayType.Value))
            {
                return;
            }
            targetView.SetTooltip(" [Default is Self]");
        }

        public void SetIsSelfTarget(bool isSelfTarget)
        {
            IsSelfTarget = isSelfTarget;
            if (isSelfTarget)
            {
                FindPortView("target").HidePort();
            }
            else
            {
                FindPortView("target").ShowPort();
            }
        }
        
        public override void SetNodeInstance(CeresNode ceresNode)
        {
            var propertyNode =(PropertyNode_PropertyValue)ceresNode;
            base.SetNodeInstance(ceresNode);
            SetIsSelfTarget(propertyNode.isSelfTarget);
        }
        
        public override ExecutableNode CompileNode()
        {
            var instance = (PropertyNode_PropertyValue)base.CompileNode();
            instance.isSelfTarget = IsSelfTarget;
            return instance;
        }
    }
}