using System;
using Ceres.Graph.Flow;
namespace Ceres.Editor.Graph.Flow
{
    public class ForwardNodeViewResolver : INodeViewResolver
    {
        public static bool IsAcceptable(Type nodeType) => nodeType.IsSubclassOf(typeof(ForwardNode));
        public ICeresNodeView CreateNodeView(Type type, CeresGraphView graphView)
        {
            return new ExecutableNodeView(type, graphView);
        }
    }
}