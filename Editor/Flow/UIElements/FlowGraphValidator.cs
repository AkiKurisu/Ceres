using System;
using System.Collections.Generic;
using Ceres.Annotations;
using Ceres.Editor.Graph.Flow;

namespace Ceres.Editor
{
    /// <summary>
    /// Validate flow graph whether it can be compiled
    /// </summary>
    public class FlowGraphValidator: IDisposable
    {
        private readonly HashSet<ExecutableNodeView> _invalidNodeViews = new();
        
        public void MarkAsInvalid(ExecutableNodeView nodeView, string reason = null)
        {
            _invalidNodeViews.Add(nodeView);
            nodeView.Flags |= ExecutableNodeViewFlags.Invalid;
            if (!string.IsNullOrEmpty(reason))
            {
                CeresLogger.LogError($"Validate node {CeresLabel.GetTypeName(nodeView.NodeType)} failed with reason: {reason}");
            }
        }

        public void Dispose()
        {
            foreach (var invalidNodeView in _invalidNodeViews)
            {
                invalidNodeView.Flags &= ~ExecutableNodeViewFlags.Invalid;
            }
            _invalidNodeViews.Clear();
        }
    }
}
