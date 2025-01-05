using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Chris;
using Chris.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UObject = UnityEngine.Object;
namespace Ceres.Graph
{
    public interface ICeresGraphContainer
    {
        /// <summary>
        /// Container bound <see cref="UObject"/>
        /// </summary>
        /// <remarks>Prefer use GameObject as container key since component is not persistent</remarks>
        /// <value></value>
        UObject Object { get; }
        
        /// <summary>
        /// Get graph instance
        /// </summary>
        /// <returns></returns>
        CeresGraph GetGraph();
        
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
    public class CeresGraph: IDisposable
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
        
        private static readonly Dictionary<Type, List<FieldInfo>> VariableFieldInfoLookup = new();
        
        private static readonly Dictionary<Type, List<FieldInfo>> PortFieldInfoLookup = new();
        
        [SerializeReference]
        public List<SharedVariable> variables;

        [SerializeReference] 
        public List<CeresNode> nodes;
        
        public List<NodeGroup> nodeGroups;
        
        [NonSerialized]
        private int[][] _nodeDependencyPath;

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
                /* Variables will be collected by node using ILPP */
#if !CERES_DISABLE_ILPP
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
                            .Where(x => x.FieldType.IsSubclassOf(typeof(SharedVariable)) || IsIListVariable(x.FieldType))
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
        
        private static bool IsIListVariable(Type fieldType)
        {
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var genericArgument = fieldType.GetGenericArguments()[0];
                if (typeof(SharedVariable).IsAssignableFrom(genericArgument))
                {
                    return true;
                }
            }
            else if (fieldType.IsArray)
            {
                var elementType = fieldType.GetElementType();
                if (typeof(SharedVariable).IsAssignableFrom(elementType))
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Traverse the graph and init all ports automatically
        /// </summary>
        /// <param name="graph"></param>
        protected static void InitPorts_Imp(CeresGraph graph)
        {
            foreach (var node in graph.GetAllNodes())
            {
                /* Ports will be collected by node using ILPP */
#if !CERES_DISABLE_ILPP
                node.InitializePorts();
                foreach (var pair in node.Ports)
                {
                    graph.LinkPort(pair.Value, pair.Key, node);
                }
                foreach (var pair in node.PortLists)
                {
                    for(int i = 0; i < pair.Value.Count; i++)
                    {
                        graph.LinkPort((CeresPort)pair.Value[i], pair.Key, i, node);
                    }
                }
#else
                var nodeType = node.GetType();
                if (!PortFieldInfoLookup.TryGetValue(nodeType, out var fields))
                {
                    fields = nodeType
                            .GetAllFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .Where(x => x.FieldType.IsSubclassOf(typeof(CeresPort)) || IsIListPort(x.FieldType))
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
                LogWarning($"Can not find port data for {fieldName}");
                return;
            }
            LinkPort(port, ownerNode, portData);
        }
        
        private void LinkPort(CeresPort port, string fieldName, int arrayIndex, CeresNode ownerNode)
        {
            var portData = ownerNode.NodeData.FindPortData(fieldName, arrayIndex);
            if(portData == null)
            {
                LogWarning($"Can not find port data for port {fieldName}_{arrayIndex} from {ownerNode.GetType().Name}");
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
                    LogWarning($"Can not find connected node [{portData.connections[0].nodeId}] from port {portData.propertyName}");
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
                    LogWarning($"Can not find connected node [{connection.nodeId}] from port {portData.propertyName}");
                    continue;
                }

                var targetPortData = targetNode.NodeData.FindPortData(connection.portId);

                var targetPort = targetPortData?.GetPort(targetNode);
                if(targetPort == null)
                {
                    LogWarning($"Can not find port {connection.portId}_{connection.portIndex} from node {targetNode.GetType().Name}");
                    continue;
                }
                port.Link(targetPort);
                targetNode.NodeData.AddDependency(ownerNode.Guid);
            }
        }

        private static bool IsIListPort(Type fieldType)
        {
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var genericArgument = fieldType.GetGenericArguments()[0];
                if (typeof(CeresPort).IsAssignableFrom(genericArgument))
                {
                    return true;
                }
            }
            else if (fieldType.IsArray)
            {
                var elementType = fieldType.GetElementType();
                if (typeof(CeresPort).IsAssignableFrom(elementType))
                {
                    return true;
                }
            }
            return false;
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

        public virtual void Dispose()
        {
            foreach (var variable in variables)
            {
                variable.Unbind();
            }
            foreach (var variable in _internalVariables)
            {
                variable.Unbind();
            }

            _nodeDependencyPath = null;
            variables.Clear();
            _internalVariables.Clear();
            foreach (var node in GetAllNodes())
            {
                node.Dispose();
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
                        if(dependencyNode == null || dependencyNode.NodeData.executionPath == ExecutionPath.Forward)
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

        public static LogType LogLevel { get; set; } = LogType.Warning;
        
        public static void LogWarning(string message)
        {
            if(LogLevel >= LogType.Warning)
                Debug.LogWarning($"<color=#fcbe03>[Ceres]</color> {message}");
        }
        
        public static void Log(string message)
        {
            if(LogLevel >= LogType.Log)
                Debug.Log($"<color=#3aff48>[Ceres]</color> {message}");
        }
    }
    
    /// <summary>
    /// Metadata for <see cref="CeresGraph"/>
    /// </summary>
    [Serializable]
    public class CeresGraphData
    {
        [SerializeReference]
        public SharedVariable[] variables;
        
        [SerializeReference]
        public CeresNode[] nodes;
        
        [SerializeField]
        public CeresNodeData[] nodeData;
        
        [SerializeField]
        public NodeGroup[] nodeGroups;

        /// <summary>
        /// Build graph from data
        /// </summary>
        /// <param name="graph"></param>
        /// <exception cref="ArgumentException"></exception>
        public virtual void BuildGraph(CeresGraph graph)
        {
            // Restore node metadata
            for (int i = 0; i < nodes.Length; ++i)
            {
                RestoreNode(i);
            }
            // Apply instances
            graph.variables = variables?.ToList() ?? new List<SharedVariable>();
            graph.nodeGroups = nodeGroups?.ToList() ?? new List<NodeGroup>();
            graph.nodes = nodes?.ToList() ?? new List<CeresNode>();
        }

        protected void RestoreNode(int index)
        {
            var config = NodeAPIUpdateConfig.GetConfig();
            if (config)
            {
                var redirectedType = config.Redirect(nodeData![index].nodeType);
                if (redirectedType != null)
                {
                    CeresGraph.Log($"Redirect node type {nodeData![index].nodeType} to {redirectedType}");
                    nodes[index] = (CeresNode)Deserialize(nodeData[index].serializedData, redirectedType);
                }
            }

            if(nodes[index] == null)
            {
                // Try make generic node
                if (IsNodeGeneric(index))
                {
                    MakeGenericNode(index);
                }
                /* Use fallback serialization */
                nodes[index] ??= GetFallbackNode(nodeData[index], index);
            }
            nodes[index].NodeData = nodeData[index];
        }

        private bool IsNodeGeneric(int index)
        {
            return nodeData[index].genericParameters != null && nodeData[index].genericParameters.Length > 0;
        }

        private bool MakeGenericNode(int index)
        {
            try
            {
                var genericTypeDefinition = nodeData[index].nodeType.ToType();
                var parameterTypes = nodeData[index].genericParameters.Select(SerializedType.FromString)
                    .ToArray();
                var genericType = genericTypeDefinition.MakeGenericType(parameterTypes);
                nodes[index] = (CeresNode)Deserialize(nodeData[index].serializedData, genericType);
                return true;
            }
            catch(Exception e)
            {
                CeresGraph.LogWarning($"Can not make generic node type from {nodeData[index].nodeType}, {e}");
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
            for(var i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].GetType().IsGenericType)
                {
                    nodes[i] = null;
                }
            }
        }
        
        /// <summary>
        /// Serialize json smarter in editor
        /// </summary>
        /// <param name="data"></param>
        /// <param name="indented"></param>
        /// <returns></returns>
        public static string Serialize(object data, bool indented = false)
        {
            if (data == null) return null;
            string json = JsonUtility.ToJson(data, indented);
#if UNITY_EDITOR
            JObject obj = JObject.Parse(json);
            foreach (var prop in obj.Descendants().OfType<JProperty>().ToList())
            {
                if (prop.Name != "instanceID") continue;
                string propertyName = prop.Name;
                if (prop.Parent?.Parent != null) propertyName = (prop.Parent?.Parent as JProperty)?.Name;
                var uObject = UnityEditor.EditorUtility.InstanceIDToObject((int)prop.Value);
                if (uObject == null) continue;
                string guid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(uObject));
                if (string.IsNullOrEmpty(guid))
                {
                    CeresGraph.LogWarning($"Can't serialize {propertyName} {uObject} in a Scene.");
                    continue;
                }
                //Convert to GUID
                prop.Value = guid;
            }
            return obj.ToString(indented ? Formatting.Indented : Formatting.None);
#else
            return json;
#endif
        }
        
        /// <summary>
        /// Deserialize <see cref="CeresGraphData"/> from json
        /// </summary>
        /// <param name="serializedData"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object Deserialize(string serializedData, Type type)
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(serializedData))
            {
                JObject obj = JObject.Parse(serializedData);
                foreach (var prop in obj.Descendants().OfType<JProperty>().ToList())
                {
                    if (prop.Name != "instanceID") continue;
                    var uObject = UnityEditor.AssetDatabase.LoadAssetAtPath<UObject>(UnityEditor.AssetDatabase.GUIDToAssetPath((string)prop.Value));
                    if (uObject == null)
                    {
                        prop.Value = 0;
                        continue;
                    }
                    prop.Value = uObject.GetInstanceID();
                }
                return JsonUtility.FromJson(obj.ToString(Formatting.Indented), type);
            }
#endif
            return JsonUtility.FromJson(serializedData, type);
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
            variables = graph.variables.ToArray();
            nodes = graph.nodes.ToArray();
            edges = new Edge[nodes.Length];
            nodeData = new CeresNodeData[nodes.Length];
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
                nodeData[i] = nodes[i].GetSerializedData();
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
                var edge = edges[n];
                for (int i = 0; i < edge.children.Length; i++)
                {
                    int childIndex = edge.children[i];
                    if (childIndex >= 0 && childIndex < nodes.Length)
                    {
                        // connect if it can set linked child
                        if( nodes[n] is ILinkedNode linkedNode)
                        {
                            linkedNode.AddChild(nodes[childIndex]);
                        }
                    }
                }
            }
        }
    }
}
