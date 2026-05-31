using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph
{
    public class CeresEdge : Edge
    {
        private const string ExecutionEdgeClass = "exec-edge";

        private const string ValueEdgeClass = "value-edge";

        private const int ExecutionEdgeWidth = 4;

        private static readonly Color ExecutionEdgeColor = new(0.86f, 0.86f, 0.86f, 1f);

        private bool _isEdgeControlStyleScheduled;

        public bool IsExecutionEdge { get; private set; }

        public CeresEdge()
        {
            AddToClassList(nameof(CeresEdge));
            styleSheets.Add(CeresGraphView.GetOrLoadStyleSheet("Ceres/CeresPortElement"));
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        public void ApplyPortStyle()
        {
            ApplyPortStyle(input as CeresPortElement, output as CeresPortElement);
        }

        public void ApplyPortStyle(CeresPortElement inputPort, CeresPortElement outputPort)
        {
            IsExecutionEdge = inputPort != null && outputPort != null &&
                              inputPort.IsExecutionPort && outputPort.IsExecutionPort;
            EnableInClassList(ExecutionEdgeClass, IsExecutionEdge);
            EnableInClassList(ValueEdgeClass, !IsExecutionEdge);
            if (!IsExecutionEdge)
            {
                UnregisterCallback<AttachToPanelEvent>(OnAttachToPanelApplyEdgeControlStyle);
                _isEdgeControlStyleScheduled = false;
                return;
            }

            ScheduleApplyEdgeControlStyle();
        }

        private void ScheduleApplyEdgeControlStyle()
        {
            if (_isEdgeControlStyleScheduled)
            {
                return;
            }

            _isEdgeControlStyleScheduled = true;
            if (panel == null)
            {
                RegisterCallback<AttachToPanelEvent>(OnAttachToPanelApplyEdgeControlStyle);
                return;
            }

            schedule.Execute(ApplyEdgeControlStyle).ExecuteLater(0);
        }

        private void OnAttachToPanelApplyEdgeControlStyle(AttachToPanelEvent evt)
        {
            UnregisterCallback<AttachToPanelEvent>(OnAttachToPanelApplyEdgeControlStyle);
            schedule.Execute(ApplyEdgeControlStyle).ExecuteLater(0);
        }

        private void ApplyEdgeControlStyle()
        {
            _isEdgeControlStyleScheduled = false;
            if (!IsExecutionEdge || edgeControl == null)
            {
                return;
            }

            edgeControl.inputColor = ExecutionEdgeColor;
            edgeControl.outputColor = ExecutionEdgeColor;
            edgeControl.edgeWidth = ExecutionEdgeWidth;
            edgeControl.MarkDirtyRepaint();
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            // Double click to insert relay node
            if (evt.clickCount == 2)
            {
                var graphView = GetFirstAncestorOfType<CeresGraphView>();
                if (graphView == null) return;

                // Calculate world position with empirical offset
                var position = evt.mousePosition + new Vector2(-25f, -45);
                var worldPos = graphView.ChangeCoordinatesTo(graphView.contentViewContainer, position);

                // Insert relay node between the connected ports
                graphView.InsertRelayNode((CeresPortElement)input, (CeresPortElement)output, worldPos);

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
                if (edgeToCreate is CeresEdge ceresEdge)
                {
                    ceresEdge.ApplyPortStyle();
                }
            }
        }
    }
}
