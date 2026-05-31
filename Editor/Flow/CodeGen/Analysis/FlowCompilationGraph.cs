using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.Graph;
using Ceres.Graph.Flow;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal sealed class FlowCompilationGraph
    {
        private readonly Dictionary<string, CeresNode> _nodesByGuid;

        private readonly Dictionary<FlowOutputKey, List<FlowInputUse>> _consumers = new();

        public FlowGraph Graph { get; }

        public IReadOnlyList<ExecutableEvent> Events { get; }

        public FlowCompilationGraph(FlowGraph graph)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _nodesByGuid = graph.nodes.ToDictionary(node => node.Guid);
            Events = graph.Events ?? Array.Empty<ExecutableEvent>();
            BuildConsumerIndex();
        }

        public bool TryGetNode(string guid, out CeresNode node)
        {
            return _nodesByGuid.TryGetValue(guid, out node);
        }

        public CeresNode GetNodeOrDefault(string guid)
        {
            return _nodesByGuid.GetValueOrDefault(guid);
        }

        public IEnumerable<FlowInputUse> GetConsumers(CeresNode source, string portId, int portIndex = -1)
        {
            if (source == null)
            {
                return Array.Empty<FlowInputUse>();
            }

            return _consumers.TryGetValue(new FlowOutputKey(source.Guid, portId, portIndex), out var consumers)
                ? consumers
                : Array.Empty<FlowInputUse>();
        }

        public int GetConsumerCount(CeresNode source, string portId, int portIndex = -1)
        {
            return GetConsumers(source, portId, portIndex).Count();
        }

        public bool TryGetInputConnection(CeresNode target, string propertyName, out FlowConnection connection)
        {
            return TryGetInputConnection(target, propertyName, -1, out connection);
        }

        public bool TryGetInputConnection(CeresNode target, string propertyName, int arrayIndex,
            out FlowConnection connection)
        {
            connection = default;
            var portData = arrayIndex < 0
                ? target.NodeData.FindPortData(propertyName)
                : target.NodeData.FindPortData(propertyName, arrayIndex);
            var directConnection = portData?.connections?.FirstOrDefault(x => !x.isFlattened);
            if (directConnection != null && TryGetNode(directConnection.nodeId, out var source))
            {
                connection = new FlowConnection(source, directConnection.portId, directConnection.portIndex);
                return true;
            }

            foreach (var candidate in Graph.nodes)
            {
                foreach (var candidatePort in candidate.NodeData.portData)
                {
                    foreach (var candidateConnection in candidatePort.connections ?? Array.Empty<PortConnectionData>())
                    {
                        if (candidateConnection.isFlattened ||
                            candidateConnection.nodeId != target.Guid ||
                            candidateConnection.portId != propertyName ||
                            (arrayIndex >= 0 && candidateConnection.portIndex != arrayIndex))
                        {
                            continue;
                        }

                        connection = new FlowConnection(candidate, candidatePort.propertyName,
                            candidatePort.arrayIndex);
                        return true;
                    }
                }
            }

            return false;
        }

        public FlowConnection GetExecConnection(CeresNode source, string propertyName)
        {
            return GetExecConnection(source, propertyName, -1);
        }

        public FlowConnection GetExecConnection(CeresNode source, string propertyName, int arrayIndex)
        {
            var portData = arrayIndex < 0
                ? source.NodeData.FindPortData(propertyName)
                : source.NodeData.FindPortData(propertyName, arrayIndex);
            var connection = portData?.connections?.FirstOrDefault(x => !x.isFlattened);
            return connection != null && TryGetNode(connection.nodeId, out var target)
                ? new FlowConnection(target, connection.portId, connection.portIndex)
                : default;
        }

        public IEnumerable<FlowConnection> GetExecConnections(CeresNode source, string propertyName)
        {
            return source.NodeData.portData
                .Where(port => port.propertyName == propertyName)
                .OrderBy(port => port.arrayIndex)
                .SelectMany(port => port.connections ?? Array.Empty<PortConnectionData>())
                .Where(connection => !connection.isFlattened)
                .Select(connection => TryGetNode(connection.nodeId, out var target)
                    ? new FlowConnection(target, connection.portId, connection.portIndex)
                    : default)
                .Where(connection => connection.IsValid);
        }

        private void BuildConsumerIndex()
        {
            foreach (var target in Graph.nodes)
            {
                foreach (var portData in target.NodeData.portData)
                {
                    foreach (var connection in portData.connections ?? Array.Empty<PortConnectionData>())
                    {
                        if (connection.isFlattened)
                        {
                            continue;
                        }

                        var key = new FlowOutputKey(connection.nodeId, connection.portId, connection.portIndex);
                        if (!_consumers.TryGetValue(key, out var consumers))
                        {
                            consumers = new List<FlowInputUse>();
                            _consumers.Add(key, consumers);
                        }

                        consumers.Add(new FlowInputUse(target, portData.propertyName, portData.arrayIndex));
                    }
                }
            }
        }
    }

    internal readonly struct FlowConnection
    {
        public readonly CeresNode Node;

        public readonly string PortId;

        public readonly int PortIndex;

        public bool IsValid => Node != null;

        public FlowConnection(CeresNode node, string portId, int portIndex)
        {
            Node = node;
            PortId = portId;
            PortIndex = portIndex;
        }
    }

    internal readonly struct FlowInputUse
    {
        public readonly CeresNode Node;

        public readonly string PortId;

        public readonly int PortIndex;

        public FlowInputUse(CeresNode node, string portId, int portIndex)
        {
            Node = node;
            PortId = portId;
            PortIndex = portIndex;
        }
    }

    internal readonly struct FlowOutputKey : IEquatable<FlowOutputKey>
    {
        public readonly string NodeGuid;

        public readonly string PortId;

        public readonly int PortIndex;

        public FlowOutputKey(string nodeGuid, string portId, int portIndex)
        {
            NodeGuid = nodeGuid;
            PortId = portId;
            PortIndex = portIndex;
        }

        public bool Equals(FlowOutputKey other)
        {
            return NodeGuid == other.NodeGuid && PortId == other.PortId && PortIndex == other.PortIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is FlowOutputKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = NodeGuid != null ? NodeGuid.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (PortId != null ? PortId.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ PortIndex;
                return hashCode;
            }
        }
    }
}
