using System;
using System.Collections.Generic;
using System.Linq;
#if CERES_DISABLE_ILPP
using Ceres.Utilities;
using System.Reflection;
using Chris;
using System.Collections;
#endif
using R3.Chris;
using Chris.Serialization;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Pool;
using UObject = UnityEngine.Object;
namespace Ceres.Graph
{
    /// <summary>
    /// Interface for containing <see cref="CeresGraph"/> data
    /// </summary>
    public interface ICeresGraphContainer
    {
        /// <summary>
        /// Container bound <see cref="UObject"/>
        /// </summary>
        /// <remarks>Prefer use GameObject as container key since component is not persistent</remarks>
        /// <value></value>
        UObject Object { get; }
        
        /// <summary>
        /// Set graph persistent data
        /// </summary>
        /// <param name="graph"></param>
        void SetGraphData(CeresGraphData graph);
    }
    
    /// <summary>
    /// Ref to a node of graph
    /// </summary>
    [Serializable]
    public struct NodeReference
    {
        public string guid;

        public NodeReference(string guid)
        {
            this.guid = guid;
        }
        
        public CeresNode Get(CeresGraph graph)
        {
            return graph.FindNode(guid);
        }
        
        public T GetT<T>(CeresGraph graph) where T: CeresNode
        {
            return graph.FindNode(guid) as T;
        }
        
        public static implicit operator NodeReference(string guid)
        {
            return new NodeReference(guid);
        }
    }
    
    /* Must set serializable to let managed reference work */
    [Serializable]
    public class CeresGraph: IDisposable, IDisposableUnregister
    {
        private BlackBoard _blackBoard;

        /// <summary>
        /// Exposed blackboard for data exchange
        /// </summary>
        public BlackBoard BlackBoard
        {
            get
            {
                return _blackBoard ??= BlackBoard.Create(variables, false);
            }
        }
        
        private readonly HashSet<SharedVariable> _internalVariables = new();
        
        private readonly HashSet<CeresPort> _internalPorts = new();
        
#if CERES_DISABLE_ILPP
        private static readonly Dictionary<Type, List<FieldInfo>> VariableFieldInfoLookup = new();
        
        private static readonly Dictionary<Type, List<FieldInfo>> PortFieldInfoLookup = new();
#endif
        
        // ==================== Managed Reference =================== //
        /* Using SerializeReference to instantiate graph easily */
        [SerializeReference]
        public List<SharedVariable> variables;

        [SerializeReference] 
        public List<CeresNode> nodes;
        // ==================== Managed Reference =================== //
        
        public List<NodeGroup> nodeGroups;
        
        [NonSerialized]
        private int[][] _nodeDependencyPath;

        private List<IDisposable> _disposables;

        public CeresGraph()
        {
            
        }
        
        public CeresGraph(CeresGraphData graphData)
        {
            graphData.BuildGraph(this);
        }

        /// <summary>
        /// Compile graph at runtime before execution
        /// </summary>
        public virtual void Compile()
        {
            InitVariables_Imp(this);
            
            /* Init ports while injecting dependency */
            InitPorts_Imp(this);
                        
            /* Calculate dependency path if not cache */
            CollectDependencyPath(this);
            
            BlackBoard.MapGlobal();
        }

        /// <summary>
        /// Get all nodes owned by this graph
        /// </summary>
        /// <returns></returns>
        public virtual List<CeresNode> GetAllNodes()
        {
            return nodes ?? new List<CeresNode>();
        }
        
        /// <summary>
        /// Traverse the graph and init all shared variables automatically
        /// </summary>
        /// <param name="graph"></param>
        protected static void InitVariables_Imp(CeresGraph graph)
        {
            var internalVariables = graph._internalVariables;
            foreach (var node in graph.GetAllNodes())
            {
#if !CERES_DISABLE_ILPP
                /* Variables will be collected by node using ILPP */
                node.InitializeVariables();
                foreach (var variable in node.SharedVariables)
                {
                    internalVariables.Add(variable);
                    variable.MapTo(graph.BlackBoard);
                }
#else
                var nodeType = node.GetType();
                if (!VariableFieldInfoLookup.TryGetValue(nodeType, out var fields))
                {
                    fields = nodeType
                            .GetAllFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .Where(x => x.FieldType.IsSubclassOf(typeof(SharedVariable)) || x.FieldType.IsIListVariable())
                            .ToList();
                    VariableFieldInfoLookup.Add(nodeType, fields);
                }
                foreach (var fieldInfo in fields)
                {
                    var value = fieldInfo.GetValue(node);
                    if (value == null)
                    {
                        value = Activator.CreateInstance(fieldInfo.FieldType);
                        fieldInfo.SetValue(node, value);
                    }
                    if (value is SharedVariable sharedVariable)
                    {
                        sharedVariable.MapTo(graph.BlackBoard);
                        internalVariables.Add(sharedVariable);
                    }
                    else if (value is IList sharedVariableList)
                    {
                        foreach (var variable in sharedVariableList)
                        {
                            var sv = variable as SharedVariable;
                            internalVariables.Add(sv);
                            sv.MapTo(graph.BlackBoard);
                        }
                    }
                }
#endif
            }
        }
        
        /// <summary>
        /// Traverse the graph and init all ports automatically
        /// </summary>
        /// <param name="graph"></param>
        protected static void InitPorts_Imp(CeresGraph graph)
        {
            var internalPorts = graph._internalPorts;
            foreach (var node in graph.GetAllNodes())
            {
#if !CERES_DISABLE_ILPP
                /* Ports will be collected by node using ILPP */
                node.InitializePorts();
                foreach (var pair in node.Ports)
                {
                    graph.LinkPort(pair.Value, pair.Key, node);
                    internalPorts.Add(pair.Value);
                }
                foreach (var pair in node.PortLists)
                {
                    for(int i = 0; i < pair.Value.Count; i++)
                    {
                        graph.LinkPort((CeresPort)pair.Value[i], pair.Key, i, node);
                        internalPorts.Add((CeresPort)pair.Value[i]);
                    }
                }
#else
                var nodeType = node.GetType();
                if (!PortFieldInfoLookup.TryGetValue(nodeType, out var fields))
                {
                    fields = nodeType
                            .GetAllFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .Where(x => x.FieldType.IsSubclassOf(typeof(CeresPort)) || x.FieldType.IsIListPort())
                            .ToList();
                    PortFieldInfoLookup.Add(nodeType, fields);
                }
                foreach (var fieldInfo in fields)
                {
                    var value = fieldInfo.GetValue(node);
                    if (value == null)
                    {
                        value = Activator.CreateInstance(fieldInfo.FieldType);
                        fieldInfo.SetValue(node, value);
                    }
                    if (value is CeresPort ceresPort)
                    {
                        graph.LinkPort(ceresPort, fieldInfo.Name, node);
                    }
                    else if (value is IList list)
                    {
                        for(int i = 0; i < list.Count; i++)
                        {
                            graph.LinkPort((CeresPort)list[i], fieldInfo.Name, i, node);
                        }
                    }
                }
#endif
            }
        }

        private void LinkPort(CeresPort port, string fieldName, CeresNode ownerNode)
        {
            var portData = ownerNode.NodeData.FindPortData(fieldName);
            if(portData == null)
            {
                CeresAPI.LogWarning($"Can not find port data for {fieldName}");
                return;
            }
            LinkPort(port, ownerNode, portData);
        }
        
        private void LinkPort(CeresPort port, string fieldName, int arrayIndex, CeresNode ownerNode)
        {
            var portData = ownerNode.NodeData.FindPortData(fieldName, arrayIndex);
            if(portData == null)
            {
                CeresAPI.LogWarning($"Can not find port data for port {fieldName}_{arrayIndex} from {ownerNode.GetType().Name}");
                return;
            }
            LinkPort(port, ownerNode, portData);
        }

        protected virtual void LinkPort(CeresPort port, CeresNode ownerNode, CeresPortData portData)
        {
            if (port is NodePort nodePort && portData.connections.Length > 0)
            {
                nodePort.Value = portData.connections[0].nodeId;
                var targetNode = FindNode(portData.connections[0].nodeId);
                if(targetNode == null)
                {
                    CeresAPI.LogWarning($"Can not find connected node [{portData.connections[0].nodeId}] from port {portData.propertyName}");
                    return;
                }
                /* Set WeakPtr to easier get target node in graph lifetime scope */
                nodePort.Node = new WeakReference<CeresNode>(targetNode);
                targetNode.NodeData.AddDependency(ownerNode.Guid);
                return;
            }

            foreach (var connection in portData.connections)
            {
                var targetNode = FindNode(connection.nodeId);
                if(targetNode == null)
                {
                    CeresAPI.LogWarning($"Can not find connected node [{connection.nodeId}] from port {portData.propertyName}");
                    continue;
                }

                var targetPortData = targetNode.NodeData.FindPortData(connection.portId);

                var targetPort = targetPortData?.GetPort(targetNode);
                if(targetPort == null)
                {
                    CeresAPI.LogWarning($"Can not find port {connection.portId}_{connection.portIndex} from node {targetNode.GetType().Name}");
                    continue;
                }
                port.Link(targetPort);
                targetNode.NodeData.AddDependency(ownerNode.Guid);
            }
        }
        
        protected static void CollectDependencyPath(CeresGraph graph)
        {
            var path = graph.GetDependencyPaths();
            if(path == null || path.Length == 0)
            {
                graph.SetDependencyPath(TopologicalSort(graph, graph.GetAllNodes()));
            }
        }

        public CeresNode FindNode(string guid)
        {
            foreach (var node in nodes)
            {
                if (node.Guid == guid)
                {
                    return node;
                }
            }

            return null;
        }
        
        
        /// <summary>
        /// Set graph node pre-cached dependency path
        /// </summary>
        /// <param name="dependencyPath"></param>
        public void SetDependencyPath(int[][] dependencyPath)
        {
            _nodeDependencyPath = dependencyPath;
        }
        
        /// <summary>
        /// Get graph node current dependency path if existed
        /// </summary>
        /// <returns></returns>
        public int[][] GetDependencyPaths()
        {
            return _nodeDependencyPath;
        }
        
        /// <summary>
        /// Get dependency execution path for destination node with guid
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public int[] GetNodeDependencyPath(string guid)
        {
            return GetNodeDependencyPath(FindNode(guid));
        }
        
        /// <summary>
        /// Get dependency execution path for destination node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public int[] GetNodeDependencyPath(CeresNode node)
        {
            return node == null ? null : _nodeDependencyPath[nodes.IndexOf(node)];
        }
        
        void IDisposableUnregister.Register(IDisposable disposable)
        {
            _disposables ??= ListPool<IDisposable>.Get();
            _disposables.Add(disposable);
        }
        
        public virtual void Dispose()
        {
            foreach (var variable in variables)
            {
                variable.Dispose();
            }
            variables.Clear();
            foreach (var variable in _internalVariables)
            {
                variable.Dispose();
            }
            _internalVariables.Clear();
            foreach (var port in _internalPorts)
            {
                port.Dispose();
            }
            _internalPorts.Clear();

            _nodeDependencyPath = null;
            foreach (var node in GetAllNodes())
            {
                node.Dispose();
            }
            nodes.Clear();

            if (_disposables != null)
            {
                foreach (var disposable in _disposables)
                {
                    disposable?.Dispose();
                }
                ListPool<IDisposable>.Release(_disposables);
            }
        }

        protected static int[][] TopologicalSort(CeresGraph graph, List<CeresNode> nodes)
        {
            /* Calculate path per node since we need to except forward nodes in each search */
            var paths = new int[nodes.Count][];
            var sorted = ListPool<int>.Get();
            var visited = DictionaryPool<string, bool>.Get();
            try
            {
                for(int i = 0; i < nodes.Count; ++i)
                {
                    VisitDependency(i, nodes[i]);
                    paths[i] = sorted.ToArray();
                    sorted.Clear();
                    visited.Clear();
                }
                return paths;
            }
            finally
            {
                ListPool<int>.Release(sorted);
                DictionaryPool<string, bool>.Release(visited);
            }

            void VisitDependency(int destinationIndex, CeresNode current)
            {
                var hasVisited = visited.TryGetValue(current.Guid, out var inSearch);
                if (hasVisited && inSearch)
                {
                    throw new ArgumentException("[Ceres] Circular dependency found which is not expected!");
                }

                visited[current.Guid] = true;
                var nodeIndex = nodes.IndexOf(current);
                var dependencies = current.NodeData.GetDependencies();
                if (dependencies != null)
                {
                    foreach (var dependency in dependencies)
                    {
                        var dependencyNode = graph.FindNode(dependency);
                        if (dependencyNode == null || dependencyNode.NodeData.executionPath == ExecutionPath.Forward)
                        {
                            continue;
                        }
                        VisitDependency(destinationIndex, dependencyNode);
                    }
                }

                visited[current.Guid] = false;
                
                if(!sorted.Contains(nodeIndex) && nodeIndex != destinationIndex)
                    sorted.Add(nodeIndex);
            }
        }
    }
    
    /// <summary>
    /// Metadata for <see cref="CeresGraph"/>
    /// </summary>
    [Serializable]
    public class CeresGraphData
    {
        // ==================== Managed Reference =================== //
        /* Keep using SerializeReference for faster deserialization */
        [SerializeReference]
        public SharedVariable[] variables;
        
        [SerializeReference]
        public CeresNode[] nodes;
        // ==================== Managed Reference =================== //

        public SharedVariableData[] variableData;
        
        public CeresNodeData[] nodeData;
        
        public NodeGroup[] nodeGroups;

        private APIUpdateConfig _config;
        
        /// <summary>
        /// Build graph from data
        /// </summary>
        /// <param name="graph"></param>
        /// <exception cref="ArgumentException"></exception>
        public virtual void BuildGraph(CeresGraph graph)
        {
            _config = APIUpdateConfig.Get();
            
            // Restore nodes
            nodes ??= new CeresNode[nodeData.Length];
            for (int i = 0; i < nodes.Length; ++i)
            {
                RestoreNode(i);
            }
            
            // Restore variables
            variables ??= new SharedVariable[variables.Length];
            for (int i = 0; i < variables.Length; ++i)
            {
                RestoreVariable(i);
            }

            // Apply instances
            graph.variables = variables.ToList();
            graph.nodes = nodes.ToList();
            graph.nodeGroups = nodeGroups?.ToList() ?? new List<NodeGroup>();
        }

        protected void RestoreNode(int index)
        {
            if (_config)
            {
                var redirectedType = RedirectNodeType(nodeData![index].nodeType);
                if (redirectedType != null)
                {
                    CeresAPI.Log($"Redirect node type {nodeData![index].nodeType} to {redirectedType}");
                    nodes[index] = nodeData[index].Deserialize(redirectedType);
                }
            }

            if(nodes[index] == null)
            {
                if (IsNodeGeneric(index))
                {
                    CreateGenericNodeInstance(index);
                }
                else
                {
                    CreateNodeInstance(index);
                }
                /* Use fallback serialization */
                nodes[index] ??= GetFallbackNode(nodeData[index], index);
            }
            // Restore metadata
            nodes[index].NodeData = nodeData[index];
        }

        protected void RestoreVariable(int index)
        {
            if (!_config) return;
            
            var redirectedType = RedirectVariableType(variableData![index].variableType);
            if (redirectedType == null) return;
            
            CeresAPI.Log($"Redirect variable type {variableData![index].variableType} to {redirectedType}");
            variables[index] = variableData[index].Deserialize(redirectedType);
        }

        /// <summary>
        /// Redirect node type from <see cref="ManagedReferenceType"/>, default using redirectors from <see cref="APIUpdateConfig"/>
        /// </summary>
        /// <param name="nodeType"></param>
        /// <returns></returns>
        protected virtual Type RedirectNodeType(ManagedReferenceType nodeType)
        {
            Assert.IsTrue((bool)_config);
            return _config.RedirectNode(nodeType);
        }
        
        /// <summary>
        /// Redirect variable type from <see cref="ManagedReferenceType"/>, default using redirectors from <see cref="APIUpdateConfig"/>
        /// </summary>
        /// <param name="nodeType"></param>
        /// <returns></returns>
        protected virtual Type RedirectVariableType(ManagedReferenceType nodeType)
        {
            Assert.IsTrue((bool)_config);
            return _config.RedirectVariable(nodeType);
        }

        private bool IsNodeGeneric(int index)
        {
            return nodeData[index].genericParameters != null && nodeData[index].genericParameters.Length > 0;
        }

        private bool CreateGenericNodeInstance(int index)
        {
            try
            {
                /* Try to make generic node type */
                var genericTypeDefinition = nodeData[index].nodeType.ToType();
                var parameterTypes = nodeData[index].genericParameters.Select(SerializedType.FromString)
                    .ToArray();
                var genericType = genericTypeDefinition.MakeGenericType(parameterTypes);
                nodes[index] = nodeData[index].Deserialize(genericType);
                return true;
            }
            catch(Exception e)
            {
                CeresAPI.LogWarning($"Can not create generic node instance from {nodeData[index].nodeType}, {e}");
                return false;
            }
        }
        
        private bool CreateNodeInstance(int index)
        {
            try
            {
                var nodeType = nodeData[index].nodeType.ToType();
                nodes[index] = nodeData[index].Deserialize(nodeType);
                return true;
            }
            catch(Exception e)
            {
                CeresAPI.LogWarning($"Can not create node instance from {nodeData[index].nodeType}, {e}");
                return false;
            }
        }

        public CeresGraphData Clone()
        {
            // use internal serialization to solve UObject hard reference
            return JsonUtility.FromJson<CeresGraphData>(JsonUtility.ToJson(this));
        }
        
        public T CloneT<T>() where T: CeresGraphData
        {
            // use internal serialization to solve UObject hard reference
            return JsonUtility.FromJson<T>(JsonUtility.ToJson(this));
        }

        /// <summary>
        /// Get fallback node for missing class
        /// </summary>
        /// <param name="fallbackNodeData"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        protected virtual CeresNode GetFallbackNode(CeresNodeData fallbackNodeData, int index)
        {
            return new InvalidNode
            {
                nodeType = fallbackNodeData.nodeType.ToString(),
                serializedData = fallbackNodeData.serializedData
            };
        }

        /// <summary>
        /// Preprocess before graph data serialized to container
        /// </summary>
        public virtual void PreSerialization()
        {
            // Remove all generic node instances since [SerializeReference] can not solve them
            for (var i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].GetType().IsGenericType)
                {
                    nodes[i] = null;
                }
            }
        }

        /// <summary>
        /// Serialize <see cref="CeresGraphData"/> to json
        /// </summary>
        /// <param name="indented"></param>
        /// <returns></returns>
        public string ToJson(bool indented = false)
        {
            return JsonUtility.ToJson(this, indented);
        }

        /// <summary>
        /// Deserialize <see cref="CeresGraphData"/> from json
        /// </summary>
        /// <param name="serializedData"></param>
        /// <returns></returns>
        public static T FromJson<T>(string serializedData) where T: CeresGraphData
        {
            return JsonUtility.FromJson<T>(serializedData);
        }
    }

    /// <summary>
    /// Linked graph data for nodes implement <see cref="ILinkedNode"/>
    /// </summary>
    [Serializable]
    public class LinkedGraphData: CeresGraphData
    {
        [Serializable]
        public class Edge
        {
            public int[] children;
        }
                
        public Edge[] edges;

        public LinkedGraphData()
        {
       
        }
        
        public LinkedGraphData(CeresGraph graph)
        {
            ReadFromLinkedNodes(graph);
        }
        
        public override void BuildGraph(CeresGraph graph)
        {
            base.BuildGraph(graph);
            LinkNodes();
        }
        
        /// <summary>
        /// Read graph data from iterating graph nodes, only worked when node implement <see cref="ILinkedNode"/>
        /// </summary>
        /// <param name="graph"></param>
        protected void ReadFromLinkedNodes(CeresGraph graph)
        {
            nodes = graph.nodes.ToArray();
            nodeData = nodes.Select(x => x.GetSerializedData()).ToArray();
            variables = graph.variables.ToArray();
            variableData = variables.Select(x => x.GetSerializedData()).ToArray();
            edges = new Edge[nodes.Length];
            for (int i = 0; i < nodes.Length; ++i)
            {
                var edge = edges[i] = new Edge();
                var linkedInterface = (ILinkedNode)nodes[i];
                edge.children = new int[linkedInterface.GetChildrenCount()];
                for (int n = 0; n < edge.children.Length; ++n)
                {
                    edge.children[n] = Array.IndexOf(nodes, linkedInterface.GetChildAt(n));
                }
                // clear duplicated reference
                linkedInterface.ClearChildren();
            }
            nodeGroups = graph.nodeGroups.ToArray();
        }
        
         protected void LinkNodes()
        {
            if (edges == null || edges.Length == 0) return;
            if (nodes == null || nodes.Length == 0) return;
            if (nodes.Length != edges.Length)
            {
                throw new ArgumentException("[Ceres] The length of behaviors and edges must be the same.");
            }
            for (int n = 0; n < nodes.Length; ++n)
            {
                // connect if it can set linked child
                if (nodes[n] is not ILinkedNode linkedNode) continue;
                
                var edge = edges[n];
                foreach (var childIndex in edge.children)
                {
                    if (childIndex >= 0 && childIndex < nodes.Length)
                    {
                        linkedNode.AddChild(nodes[childIndex]);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Identifier for <see cref="ICeresGraphContainer"/> instance
    /// </summary>
    [Serializable]
    public struct CeresGraphIdentifier: IEquatable<CeresGraphIdentifier>
    {
        public UObject boundObject;

        public string containerType;

        internal CeresGraphIdentifier(ICeresGraphContainer container)
        {
            boundObject = container.Object;
            if (boundObject is Component component)
            {
                boundObject = component.gameObject;
            }
            containerType = SerializedType.ToString(container.GetType());
        }

        public bool IsValid()
        {
            return boundObject && !string.IsNullOrEmpty(containerType);
        }
        
        /// <summary>
        /// Get <see cref="ICeresGraphContainer"/> from identifier
        /// </summary>
        /// <typeparam name="TContainer"></typeparam>
        /// <returns></returns>
        public TContainer GetContainer<TContainer>() where TContainer: class, ICeresGraphContainer
        {
            try
            {
                if (boundObject is GameObject gameObject)
                {
                    return gameObject.GetComponent(SerializedType.FromString(containerType)) as TContainer;
                }

                return boundObject as TContainer;
            }
            catch
            {
                throw new ArgumentException($"[Ceres] Can not get {typeof(TContainer).Name} from {boundObject}");
            }
        }

        public override bool Equals(object obj)
        {
            return obj is CeresGraphIdentifier identifier && Equals(identifier);
        }

        public bool Equals(CeresGraphIdentifier other)
        {
            return boundObject == other.boundObject && containerType == other.containerType;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(boundObject, containerType);
        }

        public override string ToString()
        {
            return $"Object {boundObject} Type {SerializedType.GetTypeName(containerType)}";
        }
    }
    
    public static class CeresGraphContainerExtensions
    {
        /// <summary>
        /// Get a <see cref="CeresGraphIdentifier"/> from <see cref="ICeresGraphContainer"/> instance
        /// </summary>
        /// <param name="container"></param>
        /// <returns></returns>
        public static CeresGraphIdentifier GetIdentifier(this ICeresGraphContainer container)
        {
            Assert.IsNotNull(container);
            return new CeresGraphIdentifier(container);
        }
    }
}
