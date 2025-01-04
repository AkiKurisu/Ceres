using System.Collections.Generic;
using System.Linq;
using Ceres.Graph;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
namespace Ceres.Editor.Graph
{
    public abstract class NodeGroupHandler
    {
        protected CeresGraphView GraphView { get; }

        protected NodeGroupHandler(CeresGraphView graphView)
        {
            GraphView = graphView;
        }
        
        public abstract Group CreateGroup(Rect rect, NodeGroup dataData = null);

        public abstract void DoUnGroup();
        
        public virtual void DoGroup()
        {
            var nodes = GraphView.selection.OfType<Node>().ToArray();
            if(!nodes.Any()) return;
            var block = CreateGroup(new Rect(nodes[0].transform.position, new Vector2(100, 100)));
            foreach (var node in nodes)
            {
                block.AddElement(node);
            }
        }
        
        public void RestoreGroups(List<NodeGroup> nodeGroups)
        {
            foreach (var nodeBlockData in nodeGroups)
            {
                CreateGroup(new Rect(nodeBlockData.position, new Vector2(100, 100)), nodeBlockData)
                    .AddElements(GraphView.NodeViews.Where(x => nodeBlockData.childNodes.Contains(x.Guid))
                    .Select(x => x.NodeElement));
            }
        }
    }

    public class NodeGroupHandler<TGroup>: NodeGroupHandler where TGroup: CeresNodeGroup, new()
    {
        public NodeGroupHandler(CeresGraphView graphView) : base(graphView)
        {
        }

        public override Group CreateGroup(Rect rect, NodeGroup dataData = null)
        {
            dataData ??= new NodeGroup();
            var group = new TGroup
            {
                autoUpdateGeometry = true,
                title = dataData.title
            };
            GraphView.AddElement(group);
            group.SetPosition(rect);
            return group;
        }
        
        public override void DoUnGroup()
        {
            var groups = GraphView.graphElements.OfType<TGroup>().ToArray();
            foreach (var node in GraphView.selection.OfType<Node>())
            {
                var block = groups.FirstOrDefault(x => x.ContainsElement(node));
                block?.RemoveElement(node);
            }
        }
    }
}
