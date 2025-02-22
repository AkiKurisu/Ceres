using System;
using Ceres.Graph.Flow.CustomFunctions;
using UnityEditor.Experimental.GraphView;

namespace Ceres.Editor.Graph.Flow.CustomFunctions
{
    [CustomNodeView(typeof(CustomFunctionOutput))]
    public class CustomFunctionOutputNodeView : ExecutableNodeView
    {
        public CustomFunctionOutputNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
            NodeElement.capabilities &= ~Capabilities.Copiable;
            NodeElement.capabilities &= ~Capabilities.Deletable;
        }
    }
}