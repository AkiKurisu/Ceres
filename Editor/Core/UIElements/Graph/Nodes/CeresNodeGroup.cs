using System.Collections.Generic;
using System.Linq;
using Ceres.Graph;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    public abstract class CeresNodeGroup: Group
    {
        protected CeresNodeGroup()
        {
            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
            capabilities |= Capabilities.Ascendable;
            AddToClassList(nameof(CeresNodeGroup));
        }

        public abstract void Commit(List<NodeGroup> nodeGroups);
        
        protected virtual void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.MenuItems().Clear();
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("UnGroup All", (a) =>
            {
                RemoveElements(containedElements.ToArray());
            }));
        }
    }
}