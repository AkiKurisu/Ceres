using System;
using Ceres.Graph.Flow.Utilities;
using Chris;
namespace Ceres.Editor.Graph.Flow
{
    /// <summary>
    /// Node view for <see cref="FlowNode_SwitchEnum"/>
    /// </summary>
    [CustomNodeView(typeof(FlowNode_SwitchEnum), true)]
    public class FlowNode_SwitchEnumNodeView: ExecutablePortArrayNodeView
    {
        private string[] _names;
        
        public FlowNode_SwitchEnumNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
            
        }
        
        protected override string GetPortArrayElementDisplayName(int index)
        {
            return GetDisplayNames()[index];
        }

        private string[] GetDisplayNames()
        {
            if (_names == null)
            {
                var enumType = ReflectionUtility.GetGenericArgumentType(NodeType);
                _names = Enum.GetNames(enumType);
            }
            
            return _names;
        }
    }
}