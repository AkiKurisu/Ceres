using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Graph.Flow;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal sealed class FlowCompilationGraph
    {
        private readonly Dictionary<string, CeresNode> _nodesByGuid;

        private readonly Dictionary<FlowOutputKey, List<FlowInputUse>> _consumers = new();

        private readonly Dictionary<FlowOutputKey, bool> _persistentOutputCache = new();

        private readonly Dictionary<string, EventExecutionInfo> _eventExecutionInfos = new();

        private readonly Dictionary<string, List<ExecutableEvent>> _reachableEventsByNode = new();

        private bool _executionAnalysisBuilt;

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

            if (portIndex >= 0)
            {
                return _consumers.TryGetValue(new FlowOutputKey(source.Guid, portId, portIndex), out var consumers)
                    ? consumers
                    : Array.Empty<FlowInputUse>();
            }

            return _consumers
                .Where(pair => pair.Key.NodeGuid == source.Guid && pair.Key.PortId == portId)
                .SelectMany(pair => pair.Value)
                .Distinct();
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

        public bool ShouldPersistOutput(CeresNode source, string portId, int portIndex = -1)
        {
            if (source == null ||
                source.NodeData.executionPath != ExecutionPath.Forward ||
                !GetConsumers(source, portId, portIndex).Any())
            {
                return false;
            }

            var key = new FlowOutputKey(source.Guid, portId, portIndex);
            if (_persistentOutputCache.TryGetValue(key, out var persistent))
            {
                return persistent;
            }

            persistent = ShouldPersistOutputUncached(source, portId, portIndex);
            _persistentOutputCache.Add(key, persistent);
            return persistent;
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
            foreach (var owner in Graph.nodes)
            {
                foreach (var portData in owner.NodeData.portData)
                {
                    foreach (var connection in portData.connections ?? Array.Empty<PortConnectionData>())
                    {
                        if (connection.isFlattened)
                        {
                            continue;
                        }

                        if (IsInputPort(owner, portData))
                        {
                            AddConsumer(
                                new FlowOutputKey(connection.nodeId, connection.portId, connection.portIndex),
                                new FlowInputUse(owner, portData.propertyName, portData.arrayIndex));
                        }
                        else if (IsOutputPort(owner, portData) && TryGetNode(connection.nodeId, out var target))
                        {
                            AddConsumer(
                                new FlowOutputKey(owner.Guid, portData.propertyName, portData.arrayIndex),
                                new FlowInputUse(target, connection.portId, connection.portIndex));
                        }
                    }
                }
            }
        }

        private void AddConsumer(FlowOutputKey key, FlowInputUse consumer)
        {
            if (string.IsNullOrEmpty(key.NodeGuid) || string.IsNullOrEmpty(key.PortId) || consumer.Node == null)
            {
                return;
            }

            if (!_consumers.TryGetValue(key, out var consumers))
            {
                consumers = new List<FlowInputUse>();
                _consumers.Add(key, consumers);
            }

            if (!consumers.Contains(consumer))
            {
                consumers.Add(consumer);
            }
        }

        private bool ShouldPersistOutputUncached(CeresNode source, string portId, int portIndex)
        {
            EnsureExecutionAnalysis();
            if (!_reachableEventsByNode.TryGetValue(source.Guid, out var sourceEvents) ||
                sourceEvents.Count != 1)
            {
                return true;
            }

            var sourceEvent = sourceEvents[0];
            foreach (var consumer in GetConsumers(source, portId, portIndex))
            {
                if (!_reachableEventsByNode.TryGetValue(consumer.Node.Guid, out var consumerEvents) ||
                    consumerEvents.Count != 1 ||
                    consumerEvents[0] != sourceEvent)
                {
                    return true;
                }

                if (!_eventExecutionInfos.TryGetValue(sourceEvent.Guid, out var info) ||
                    !info.Dominates(source.Guid, consumer.Node.Guid))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureExecutionAnalysis()
        {
            if (_executionAnalysisBuilt)
            {
                return;
            }

            foreach (var evt in Events)
            {
                var info = BuildEventExecutionInfo(evt);
                _eventExecutionInfos[evt.Guid] = info;
                foreach (var nodeGuid in info.ReachableNodeGuids)
                {
                    if (!_reachableEventsByNode.TryGetValue(nodeGuid, out var events))
                    {
                        events = new List<ExecutableEvent>();
                        _reachableEventsByNode.Add(nodeGuid, events);
                    }

                    events.Add(evt);
                }
            }

            _executionAnalysisBuilt = true;
        }

        private EventExecutionInfo BuildEventExecutionInfo(ExecutableEvent evt)
        {
            var reachable = new HashSet<string>();
            var predecessors = new Dictionary<string, HashSet<string>>();
            var queue = new Queue<CeresNode>();
            reachable.Add(evt.Guid);
            queue.Enqueue(evt);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var successor in GetExecSuccessors(node))
                {
                    if (!predecessors.TryGetValue(successor.Guid, out var set))
                    {
                        set = new HashSet<string>();
                        predecessors.Add(successor.Guid, set);
                    }

                    set.Add(node.Guid);
                    if (reachable.Add(successor.Guid))
                    {
                        queue.Enqueue(successor);
                    }
                }
            }

            var dominators = BuildDominators(evt.Guid, reachable, predecessors);
            return new EventExecutionInfo(reachable, dominators);
        }

        private IEnumerable<CeresNode> GetExecSuccessors(CeresNode source)
        {
            foreach (var portData in source.NodeData.portData)
            {
                if (!IsExecOutputPort(source, portData))
                {
                    continue;
                }

                foreach (var connection in portData.connections ?? Array.Empty<PortConnectionData>())
                {
                    if (!connection.isFlattened && TryGetNode(connection.nodeId, out var target))
                    {
                        yield return target;
                    }
                }
            }
        }

        private static Dictionary<string, HashSet<string>> BuildDominators(string rootGuid,
            HashSet<string> reachable, Dictionary<string, HashSet<string>> predecessors)
        {
            var dominators = new Dictionary<string, HashSet<string>>();
            foreach (var nodeGuid in reachable)
            {
                dominators[nodeGuid] = nodeGuid == rootGuid
                    ? new HashSet<string> { rootGuid }
                    : new HashSet<string>(reachable);
            }

            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var nodeGuid in reachable)
                {
                    if (nodeGuid == rootGuid)
                    {
                        continue;
                    }

                    var next = new HashSet<string>(reachable);
                    if (predecessors.TryGetValue(nodeGuid, out var predSet) && predSet.Count > 0)
                    {
                        foreach (var predecessor in predSet)
                        {
                            next.IntersectWith(dominators[predecessor]);
                        }
                    }
                    else
                    {
                        next.Clear();
                    }

                    next.Add(nodeGuid);
                    if (!dominators[nodeGuid].SetEquals(next))
                    {
                        dominators[nodeGuid] = next;
                        changed = true;
                    }
                }
            }

            return dominators;
        }

        private static bool IsInputPort(CeresNode node, CeresPortData portData)
        {
            return portData.GetFieldInfo(node.GetType())?.GetCustomAttributes(typeof(InputPortAttribute), true).Length > 0;
        }

        private static bool IsOutputPort(CeresNode node, CeresPortData portData)
        {
            return portData.GetFieldInfo(node.GetType())?.GetCustomAttributes(typeof(OutputPortAttribute), true).Length > 0;
        }

        private static bool IsExecOutputPort(CeresNode node, CeresPortData portData)
        {
            return IsOutputPort(node, portData) && portData.GetValueType() == typeof(NodeReference);
        }

        private sealed class EventExecutionInfo
        {
            private readonly Dictionary<string, HashSet<string>> _dominators;

            public HashSet<string> ReachableNodeGuids { get; }

            public EventExecutionInfo(HashSet<string> reachableNodeGuids,
                Dictionary<string, HashSet<string>> dominators)
            {
                ReachableNodeGuids = reachableNodeGuids;
                _dominators = dominators;
            }

            public bool Dominates(string sourceGuid, string targetGuid)
            {
                return _dominators.TryGetValue(targetGuid, out var dominators) &&
                       dominators.Contains(sourceGuid);
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

        public bool Equals(FlowInputUse other)
        {
            return Node == other.Node && PortId == other.PortId && PortIndex == other.PortIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is FlowInputUse other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Node != null ? Node.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (PortId != null ? PortId.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ PortIndex;
                return hashCode;
            }
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
