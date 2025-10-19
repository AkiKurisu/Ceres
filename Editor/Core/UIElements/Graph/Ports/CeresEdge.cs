using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph
{
    public class CeresEdge : Edge
    {
        public CeresEdge()
        {
            AddToClassList(nameof(CeresEdge));
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            // Double click to insert relay node
            if (evt.clickCount == 2)
            {
                var graphView = GetFirstAncestorOfType<CeresGraphView>();
                if (graphView == null) return;

                // Calculate world position with empirical offset
                var position = evt.mousePosition + new Vector2(-10f, -28);
                var worldPos = graphView.ChangeCoordinatesTo(
                    graphView.contentViewContainer,
                    position
                );

                // Insert relay node between the connected ports
                graphView.InsertRelayNode(
                    input as CeresPortElement,
                    output as CeresPortElement,
                    worldPos
                );

                evt.StopPropagation();
            }
        }
    }

    /// <summary>
    /// Edge listener connecting <see cref="CeresEdge"/> between <see cref="CeresPortElement"/>
    /// </summary>
    public class CeresEdgeListener : IEdgeConnectorListener
    {
        private readonly GraphViewChange _graphViewChange;

        private readonly List<Edge> _edgesToCreate;

        private readonly List<GraphElement> _edgesToDelete;

        private readonly CeresGraphView _graphView;

        public CeresEdgeListener(CeresGraphView ceresGraphView)
        {
            _graphView = ceresGraphView;
            _edgesToCreate = new List<Edge>();
            _edgesToDelete = new List<GraphElement>();
            _graphViewChange.edgesToCreate = _edgesToCreate;
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            var screenPosition = GUIUtility.GUIToScreenPoint(
                Event.current.mousePosition
            );

            if (edge.output?.edgeConnector.edgeDragHelper.draggedPort != null)
            {
                _graphView.OpenSearch(
                    screenPosition,
                    ((CeresPortElement)edge.output.edgeConnector.edgeDragHelper.draggedPort).View
                );
            }
            else if (edge.input?.edgeConnector.edgeDragHelper.draggedPort != null)
            {
                _graphView.OpenSearch(
                    screenPosition,
                    ((CeresPortElement)edge.input.edgeConnector.edgeDragHelper.draggedPort).View
                );
            }
        }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            _edgesToCreate.Clear();
            _edgesToCreate.Add(edge);
            _edgesToDelete.Clear();

            if (edge.input.capacity == Port.Capacity.Single)
            {
                foreach (var connection in edge.input.connections)
                {
                    if (connection != edge)
                        _edgesToDelete.Add(connection);
                }
            }

            if (edge.output.capacity == Port.Capacity.Single)
            {
                foreach (var connection in edge.output.connections)
                {
                    if (connection != edge)
                        _edgesToDelete.Add(connection);
                }
            }

            if (_edgesToDelete.Count > 0)
            {
                graphView.DeleteElements(_edgesToDelete);
            }

            var edgesToCreate = _edgesToCreate;
            if (graphView.graphViewChanged != null)
            {
                edgesToCreate = graphView.graphViewChanged(_graphViewChange).edgesToCreate;
            }
            foreach (var edgeToCreate in edgesToCreate)
            {
                graphView.AddElement(edgeToCreate);
                edgeToCreate.input.Connect(edgeToCreate);
                edgeToCreate.output.Connect(edgeToCreate);
            }
        }
    }
}