using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using Chris.Collections;
using Chris.Serialization;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Pool;
namespace Ceres.Graph
{
    /// <summary>
    /// Interface for iterate read-only linked node
    /// </summary>
    public interface IReadOnlyLinkedNode
    {
        /// <summary>
        /// Get child not at index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        CeresNode GetChildAt(int index);
        
        /// <summary>
        /// Get child node count
        /// </summary>
        /// <returns></returns>
        int GetChildrenCount();
    }

    /// <summary>
    /// Interface for iterate linked node, useful when your graph is based on a linked list
    /// which is often used in a forward execution mode like behavior tree
    /// </summary>
    public interface ILinkedNode: IReadOnlyLinkedNode
    {
        /// <summary>
        /// Add new child node
        /// </summary>
        /// <param name="node"></param>
        void AddChild(CeresNode node);

        /// <summary>
        /// Clear all child nodes
        /// </summary>
        void ClearChildren();
    }
    
    /// <summary>
    /// Define how to execute node
    /// </summary>
    public enum ExecutionPath
    {
        /// <summary>
        /// Execute node in a forward path that ensure order
        /// </summary>
        Forward,
        /// <summary>
        /// Execute node by a dependency graph that let node executed only when used
        /// </summary>
        Dependency
    }
    
    /// <summary>
    /// Base class for ceres graph node
    /// </summary>
    [Serializable]
    public abstract class CeresNode: IEnumerable<CeresNode>, IDisposable
    {
        // CeresNodeILPP will override this in derived classes to return the name of the concrete type
        internal virtual string __getTypeName() => nameof(CeresNode);
        
#pragma warning disable IDE1006 // disable naming rule violation check
        // RuntimeAccessModifiersILPP will make this `protected`
        internal virtual void __initializeVariables()
#pragma warning restore IDE1006 // restore naming rule violation check
        {
            // ILPP generates code for all CeresNode subtypes to initialize each type's SharedVariables.
        }
        
#pragma warning disable IDE1006 // disable naming rule violation check
        // RuntimeAccessModifiersILPP will make this `protected`
        internal virtual void __initializePorts()
#pragma warning restore IDE1006 // restore naming rule violation check
        {
            // ILPP generates code for all CeresNode subtypes to initialize each type's CeresPort.
        }
        
#if !CERES_DISABLE_ILPP
        // RuntimeAccessModifiersILPP will make this `protected`
        internal readonly List<SharedVariable> SharedVariables = new();
        
        // RuntimeAccessModifiersILPP will make this `protected`
        internal readonly Dictionary<string, CeresPort> Ports = new();
        
        // RuntimeAccessModifiersILPP will make this `protected`
        internal readonly Dictionary<string, IList> PortLists = new();
#endif
        
        public CeresNodeData NodeData { get; protected internal set; }= new();

        /// <summary>
        /// Node unique id
        /// </summary>
        public string Guid 
        { 
            get => NodeData.guid; 
            set => NodeData.guid = value; 
        }
        
        /// <summary>
        /// Node graph position
        /// </summary>
        public Rect GraphPosition
        { 
            get => NodeData.graphPosition; 
            set => NodeData.graphPosition = value; 
        }
        
        /// <summary>
        /// Release on node destroy
        /// </summary>
        public virtual void Dispose()
        {

        }
        
        /// <summary>
        /// Get serialized data of this node
        /// </summary>
        /// <returns></returns>
        public virtual CeresNodeData GetSerializedData()
        {
            /* Allows polymorphic serialization */
            var data = NodeData.Clone();
            data.executionPath = GetExecutionPath();
            data.Serialize(this);
            return data;
        }

        /// <summary>
        /// Get node instance execution path
        /// </summary>
        /// <returns></returns>
        public virtual ExecutionPath GetExecutionPath()
        {
            return GetExecutionPath(GetType());
        }
        
        public virtual IEnumerator<CeresNode> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
                
        public static ExecutionPath GetExecutionPath(Type nodeType)
        {
            var paths = CeresMetadata.GetMetadata(nodeType, "path");
            if (paths.Length <= 0)
            {
                return ExecutionPath.Forward;
            }

            var path = paths[0].ToLower();
            if (path == "forward")
            {
                return ExecutionPath.Forward;
            }
            if (path == "dependency")
            {
                return ExecutionPath.Dependency;
            }
            return ExecutionPath.Forward;
        }
        
        public static string GetTargetSubtitle(string name, bool richText = true)
        {
            if(richText)
            {
                return $"\n<color=#414141><size=10>Target is {name}</size></color>";
            }

            return $"\nTarget is {name}";
        }
        
        public static string GetTargetSubtitle(Type type, bool richText = true)
        {
            return GetTargetSubtitle(CeresLabel.GetTypeName(type), richText);
        }
        
        /// <summary>
        /// Collect <see cref="SharedVariable"/> from this node instance
        /// </summary>
        internal void InitializeVariables()
        {
            __initializeVariables();
        }
        
        /// <summary>
        /// Collect <see cref="CeresPort"/> from this node instance
        /// </summary>
        internal void InitializePorts()
        {
            __initializePorts();
        }

        /// <summary>
        /// Get node type short name for debug purpose
        /// </summary>
        /// <returns></returns>
        public string GetTypeName()
        {
#if CERES_DISABLE_ILPP
            return GetType().Name;
#else
            return __getTypeName();
#endif
        }
        
        protected struct Enumerator : IEnumerator<CeresNode>
        {
            private readonly Stack<CeresNode> _stack;
            
            private static readonly ObjectPool<Stack<CeresNode>> Pool = new(() => new Stack<CeresNode>(), null, s => s.Clear());
            
            private CeresNode _currentNode;
            
            public Enumerator(CeresNode root)
            {
                _stack = Pool.Get();
                _currentNode = null;
                if (root != null)
                {
                    _stack.Push(root);
                }
            }

            public readonly CeresNode Current
            {
                get
                {
                    if (_currentNode == null)
                    {
                        throw new InvalidOperationException();
                    }
                    return _currentNode;
                }
            }

            readonly object IEnumerator.Current => Current;

            public void Dispose()
            {
                Pool.Release(_stack);
                _currentNode = null;
            }
            public bool MoveNext()
            {
                if (_stack.Count == 0)
                {
                    return false;
                }

                _currentNode = _stack.Pop();
                if (_currentNode is not IReadOnlyLinkedNode iterateNode) return true;
                
                int childrenCount = iterateNode.GetChildrenCount();
                for (int i = childrenCount - 1; i >= 0; i--)
                {
                    _stack.Push(iterateNode.GetChildAt(i));
                }
                return true;
            }
            public void Reset()
            {
                _stack.Clear();
                if (_currentNode != null)
                {
                    _stack.Push(_currentNode);
                }
                _currentNode = null;
            }
        }
    }
    /// <summary>
    /// Metadata for <see cref="CeresNode"/>
    /// </summary>
    [Serializable]
    public class CeresNodeData
    {
        /// <summary>
        /// Serialized node type in managed reference format
        /// </summary>
        [Serializable]
        public struct NodeType: IEquatable<NodeType>
        {
            // ReSharper disable once InconsistentNaming
            public string _class;

            // ReSharper disable once InconsistentNaming
            public string _ns;
            
            // ReSharper disable once InconsistentNaming
            public string _asm;
            
            public NodeType(string inClass, string inNamespace, string inAssembly)
            {
                _class = inClass;
                _ns = inNamespace;
                _asm = inAssembly;
            }
            
            public NodeType(Type type)
            {
                _class = type.Name;
                _ns = type.Namespace;
                _asm = type.Assembly.GetName().Name;
            }
            
            public readonly Type ToType()
            {
                return Type.GetType(Assembly.CreateQualifiedName(_asm, $"{_ns}.{_class}"));
            }

            public bool Equals(NodeType other)
            {
                return _class == other._class && _ns == other._ns && _asm == other._asm;
            }

            public readonly override string ToString()
            {
                return $"class: {_class} ns: {_ns} asm: {_asm}";
            }
        }
        
        /// <summary>
        /// Node graph editor position
        /// </summary>
        public Rect graphPosition = new(400, 300, 100, 100);
        
        /// <summary>
        /// Node user description
        /// </summary>
        public string description;
        
        /// <summary>
        /// Node unique id
        /// </summary>
        public string guid;
        
        /// <summary>
        /// Node type
        /// </summary>
        public NodeType nodeType;

        /// <summary>
        /// Generic type parameters
        /// </summary>
        public string[] genericParameters = Array.Empty<string>();
        
        /// <summary>
        /// Json serialized data of node properties
        /// </summary>
        public string serializedData;

        /// <summary>
        /// Node execution path
        /// </summary>
        public ExecutionPath executionPath;

        /// <summary>
        /// Execution dependencies
        /// </summary>
        private HashSet<string> _dependencies;
        
        /// <summary>
        /// Port metadata
        /// </summary>
        public CeresPortData[] portData = Array.Empty<CeresPortData>();

        [SerializeField]
        private UObjectLink[] uobjectLinks = Array.Empty<UObjectLink>();
        
        public static bool LogUObjectRelink { get; set; }
        
        public void AddPortData(CeresPortData data)
        {
            ArrayUtils.Add(ref portData, data); 
        }
        
        public void RemovePortData(CeresPortData data)
        {
            ArrayUtils.Remove(ref portData, data); 
        }
        
        public void ClearPortData()
        {
            ArrayUtils.Clear(ref portData); 
        }

        public CeresPortData FindPortData(string propertyName)
        {
            foreach (var data in portData)
            {
                if (data.propertyName == propertyName) return data;
            }
            return null;
        }
        
        public CeresPortData FindPortData(string propertyName, int index)
        {
            foreach (var data in portData)
            {
                if (data.propertyName == propertyName && data.arrayIndex == index) return data;
            }
            return null;
        }

        internal void AddDependency(string nodeGuid)
        {
            _dependencies ??= new HashSet<string>();
            _dependencies.Add(nodeGuid);
        }
        
        internal HashSet<string> GetDependencies()
        {
            return _dependencies;
        }
        
        public virtual CeresNodeData Clone()
        {
            return new CeresNodeData
            {
                graphPosition = graphPosition,
                description = description,
                guid = guid,
                nodeType = nodeType,
                genericParameters = genericParameters.ToArray(),
                serializedData = serializedData,
                portData = portData,
                uobjectLinks = uobjectLinks
            };
        }
        
        /// <summary>
        /// Serialize node data
        /// </summary>
        /// <param name="node"></param>
        public virtual void Serialize(CeresNode node)
        {
            var type = node.GetType();
            if(type.IsGenericType)
            {
                nodeType = new NodeType(type.GetGenericTypeDefinition());
                genericParameters = type.GetGenericArguments().Select(SerializedType.ToString).ToArray();
            }
            else
            {
                nodeType = new NodeType(type);
            }
            serializedData = JsonUtility.ToJson(node);
#if UNITY_EDITOR
            uobjectLinks = Array.Empty<UObjectLink>();
            if(!Application.isPlaying)
            {
                var obj = JObject.Parse(serializedData);
                /* Persistent instanceID */
                foreach (var prop in obj.Descendants().OfType<JProperty>().ToList())
                {
                    if (prop.Name != "instanceID") continue;
                    var instanceId = (int)prop.Value;
                    var uObject = UnityEditor.EditorUtility.InstanceIDToObject(instanceId);
                    if(uObject)
                    {
                        ArrayUtils.Add(ref uobjectLinks, new UObjectLink(uObject));
                    }
                }
            }
#endif
            /* Override to customize serialization like ISerializationCallbackReceiver */
        }
        
        /// <summary>
        /// Deserialize a <see cref="CeresNode"/> instance from this data
        /// </summary>
        /// <param name="outNodeType"></param>
        /// <returns></returns>
        public CeresNode Deserialize(Type outNodeType)
        {
            var obj = JObject.Parse(serializedData);
            /* Resolve instanceID */
            foreach (var prop in obj.Descendants().OfType<JProperty>().ToList())
            {
                if (prop.Name != "instanceID") continue;
                var instanceId = (int)prop.Value;
                var uObject = uobjectLinks.FirstOrDefault(x=> x.instanceId == instanceId);
                if (uObject != null)
                {
                    var linkedUObject = uObject.linkedObject;
                    prop.Value = linkedUObject == null ? 0 : linkedUObject.GetInstanceID();
                    if(linkedUObject && LogUObjectRelink)
                    {
                        CeresAPI.Log($"Relink UObject {instanceId} to {uObject.linkedObject.name} {prop.Value}");
                    }
                }
            }
            return JsonUtility.FromJson(obj.ToString(), outNodeType) as CeresNode;
        }
    }
}