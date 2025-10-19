using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.Graph;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using NodeElement = UnityEditor.Experimental.GraphView.Node;

namespace Ceres.Editor.Graph
{
    /// <summary>
    /// Node view for <see cref="RelayNode"/>
    /// </summary>
    public class RelayNodeView : ICeresNodeView
    {
        public string Guid { get; set; }

        public NodeElement NodeElement { get; private set; }

        public CeresGraphView GraphView { get; }

        public RelayNode Data { get; }


        private CeresPortElement _inputPort;

        private CeresPortElement _outputPort;

        private readonly Type _portType;

        public RelayNodeView(CeresGraphView graphView, RelayNode data, Type portType)
        {
            GraphView = graphView;
            Data = data;
            Guid = data.guid;
            _portType = portType ?? typeof(object);

            CreateVisualElement();
        }

        private void CreateVisualElement()
        {
            NodeElement = new NodeElement
            {
                title = string.Empty
            };

            // Load relay node stylesheet
            var styleSheet = CeresGraphView.GetOrLoadStyleSheet("Ceres/RelayNode");
            NodeElement.styleSheets.Add(styleSheet);

            // Create input port
            _inputPort = new CeresPortElement(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Single,
                _portType
            )
            {
                portName = ""
            };

            // Create output port
            _outputPort = new CeresPortElement(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Single,
                _portType
            )
            {
                portName = ""
            };

            NodeElement.inputContainer.Add(_inputPort);
            NodeElement.outputContainer.Add(_outputPort);

            NodeElement.AddToClassList("RelayNode");

            // Set position
            NodeElement.SetPosition(Data.graphPosition);

            // Register deletion callback
            NodeElement.RegisterCallback<DetachFromPanelEvent>(OnNodeDetached);

            NodeElement.userData = this;
        }

        private void OnNodeDetached(DetachFromPanelEvent evt)
        {
            // Check if this is a real deletion (not just graph reload)
            if (GraphView != null)
            {
                OnRemoved();
            }
        }

        public CeresPortElement GetInputPort() => _inputPort;

        public CeresPortElement GetOutputPort() => _outputPort;

        public RelayNode Compile()
        {
            // Update position and metadata
            Data.graphPosition = NodeElement.GetPosition();
            Data.guid = Guid;

            // Save port type
            Data.SetPortType(_portType);

            // Collect input connections (record current state, no flattening)
            var inputs = new List<RelayConnection>();
            foreach (var edge in _inputPort.connections)
            {
                if (edge.output is CeresPortElement outputPort)
                {
                    // Check if connected to ExecutableNode or RelayNode
                    if (outputPort.View != null)
                    {
                        // Connected to ExecutableNode
                        inputs.Add(new RelayConnection
                        {
                            connectionType = RelayConnection.ConnectionType.ExecutableNode,
                            nodeId = outputPort.View.NodeOwner.Guid,
                            portId = outputPort.View.PortData.propertyName,
                            portIndex = outputPort.View.PortData.arrayIndex
                        });
                    }
                    else if (outputPort.node?.userData is RelayNodeView relayNodeView)
                    {
                        // Connected to another RelayNode
                        inputs.Add(new RelayConnection
                        {
                            connectionType = RelayConnection.ConnectionType.RelayNode,
                            nodeId = relayNodeView.Guid
                        });
                    }
                }
            }
            Data.inputs = inputs.ToArray();

            // Collect output connections (record current state, no flattening)
            var outputs = new List<RelayConnection>();
            foreach (var edge in _outputPort.connections)
            {
                if (edge.input is CeresPortElement inputPort)
                {
                    // Check if connected to ExecutableNode or RelayNode
                    if (inputPort.View != null)
                    {
                        // Connected to ExecutableNode
                        outputs.Add(new RelayConnection
                        {
                            connectionType = RelayConnection.ConnectionType.ExecutableNode,
                            nodeId = inputPort.View.NodeOwner.Guid,
                            portId = inputPort.View.PortData.propertyName,
                            portIndex = inputPort.View.PortData.arrayIndex
                        });
                    }
                    else if (inputPort.node?.userData is RelayNodeView relayNodeView)
                    {
                        // Connected to another RelayNode
                        outputs.Add(new RelayConnection
                        {
                            connectionType = RelayConnection.ConnectionType.RelayNode,
                            nodeId = relayNodeView.Guid
                        });
                    }
                }
            }
            Data.outputs = outputs.ToArray();
            return Data;
        }

        private void OnRemoved()
        {
            // Schedule reconnection to avoid issues with cascading deletions
            GraphView.schedule.Execute(() =>
            {
                var inputEdges = _inputPort.connections.ToList();
                var outputEdges = _outputPort.connections.ToList();

                if (inputEdges.Count == 0 || outputEdges.Count == 0)
                    return;

                // Get source port from first input edge
                var sourceEdge = inputEdges.FirstOrDefault();
                if (sourceEdge?.output is not CeresPortElement sourcePort)
                    return;

                // Reconnect all outputs to the source
                foreach (var outputEdge in outputEdges)
                {
                    if (outputEdge.input is CeresPortElement targetPort)
                    {
                        // Create direct connection
                        var newEdge = new CeresEdge
                        {
                            output = sourcePort,
                            input = targetPort
                        };

                        GraphView.AddElement(newEdge);
                        sourcePort.Connect(newEdge);
                        targetPort.Connect(newEdge);
                    }
                }
            }).ExecuteLater(1);
        }
    }
}

