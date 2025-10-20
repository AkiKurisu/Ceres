using System;
using System.Collections.Generic;
using Ceres.Graph;
using UnityEditor.Experimental.GraphView;
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

            NodeElement.userData = this;
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
                            connectionType = RelayConnection.ConnectionType.CeresNode,
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
                            connectionType = RelayConnection.ConnectionType.CeresNode,
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
    }
}

