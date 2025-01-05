using System.Collections.Generic;
using System.Linq;
using Ceres.Graph;
namespace Ceres.Editor.Graph.Flow
{
    public class ExecutableNodeGroup: CeresNodeGroup
    {
        public ExecutableNodeGroup()
        {
            AddToClassList(nameof(ExecutableNodeGroup));
        }
        
        public override void Commit(List<NodeGroup> nodeGroups)
        {
            var guids = containedElements
                .OfType<ExecutableNodeElement>()
                .Select(x => x.View.Guid)
                .ToList();
            nodeGroups.Add(new NodeGroup
            {
                childNodes = guids,
                title = title,
                position = GetPosition().position
            });
        }
    }
}