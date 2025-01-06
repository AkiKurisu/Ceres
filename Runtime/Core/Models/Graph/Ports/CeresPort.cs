using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using Chris;
using Chris.Serialization;
namespace Ceres.Graph
{
    
    public interface IPort
    {
        object GetValue();
    }
    
    public interface IPort<out TValue>: IPort
    {
        TValue Value { get; }
    }
    
    /// <summary>
    /// Base class for ceres graph port
    /// </summary>
    public abstract class CeresPort: IPort
    {
        /// <summary>
        /// Link another <see cref="CeresPort"/> in forward path
        /// </summary>
        /// <param name="targetPort"></param>
        public abstract void Link(CeresPort targetPort);

        public abstract void SetValue(object value);

        public abstract object GetValue();
        
        /// <summary>
        /// Last linked adapted port
        /// </summary>
        [NonSerialized]
        protected internal IPort AdaptedGetter;

        private static readonly HashSet<Type> PortValueTypeSet = new();

        public static void AssignValueType<T>()
        {
            PortValueTypeSet.Add(typeof(T));
        }
        
        public static void AssignValueType(Type type)
        {
            PortValueTypeSet.Add(type);
        }

        public static Type[] GetAssignedPortValueTypes()
        {
            return PortValueTypeSet.ToArray();
        }
    }
    
    /// <summary>
    /// Generic port for any value worked as linked list at runtime
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    [Serializable]
    public class CeresPort<TValue>: CeresPort, IPort<TValue>
    {
        /// <summary>
        /// Port self value or default value if has no connection
        /// </summary>
        [HideInGraphEditor, CeresLabel("")]
        public TValue defaultValue;
        
        /// <summary>
        /// Last linked port
        /// </summary>
        [NonSerialized]
        private IPort<TValue> _getter;

        private static Type _valueType;
        
        static CeresPort()
        {
            AssignValueType<TValue>();
            _valueType = typeof(TValue);
        }
        
        public CeresPort()
        {
     
        }
        
        public CeresPort(TValue value): this()
        {
            defaultValue = value;
        }

        public TValue Value
        {
            get
            {
                /* Get value in backward */
                if (_getter != null) return _getter.Value;
                if (AdaptedGetter != null) return (TValue)AdaptedGetter.GetValue();
                return defaultValue;
            }
            set => defaultValue = value;
        }

        public override void Link(CeresPort targetPort)
        {
            if (targetPort is not CeresPort<TValue> genericPort)
            {
                targetPort.AdaptedGetter = this;
                return;
            }
            genericPort._getter = this;
        }

        public override void SetValue(object value)
        {
            Value = (TValue)value;
        }

        public override object GetValue()
        {
            return Value;
        }

        public static Type GetValueType()
        {
            return _valueType;
        }

        public static readonly CeresPort<TValue> Default = new();
            
        public static readonly CeresPort<TValue>[] DefaultArray = Array.Empty<CeresPort<TValue>>();
        
        public static readonly List<CeresPort<TValue>> DefaultList = new();
    }
    
    /// <summary>
    /// Port for providing weak reference to any node in graph scope
    /// </summary>
    [Serializable]
    public sealed class NodePort : CeresPort<NodeReference>
    {
        public NodePort()
        {
            
        }
        
        public NodePort(NodeReference value) : base(value)
        {
            
        }

        internal WeakReference<CeresNode> Node;

        public CeresNode Get()
        {
            if (Node == null) return null;
            return Node.TryGetTarget(out var node) ? node : null;
        }

        public T GetT<T>() where T: CeresNode
        {
            return (T)Get();
        }
    }

    [Serializable]
    public class PortConnectionData
    {
        public string nodeId;
        
        public string portId;
        
        public int portIndex;
    }
    
    /// <summary>
    /// Metadata for <see cref="CeresPort"/>
    /// </summary>
    [Serializable]
    public class CeresPortData
    {
        /// <summary>
        /// Port generic parameter type string
        /// </summary>
        public string type;

        /// <summary>
        /// Bound property name
        /// </summary>
        public string propertyName;

        /// <summary>
        /// Array index if bound property is an array
        /// </summary>
        public int arrayIndex;
        
        /// <summary>
        /// Port connection data
        /// </summary>
        public PortConnectionData[] connections;

        public Type GetValueType()
        {
            return SerializedType.FromString(type);
        }
        
        public void SetValueType(Type valueType)
        {
            type = SerializedType.ToString(valueType);
        }

        /// <summary>
        /// Create port data from <see cref="FieldInfo"/>
        /// </summary>
        /// <param name="fieldInfo"></param>
        /// <returns></returns>
        public static CeresPortData FromFieldInfo(FieldInfo fieldInfo)
        {
            var elementType = fieldInfo.FieldType.IsArray ? fieldInfo.FieldType.GetElementType() : fieldInfo.FieldType;
            if (elementType!.IsSubclassOf(typeof(CeresPort)))
            {
                elementType = ReflectionUtility.GetGenericArgumentType(elementType);
            }
            var data = new CeresPortData
            {
                propertyName = fieldInfo.Name,
                type = SerializedType.ToString(elementType),
                connections = Array.Empty<PortConnectionData>(),
            };
            return data;
        }
        
        /// <summary>
        /// Create port data from <see cref="ParameterInfo"/>
        /// </summary>
        /// <param name="parameterInfo"></param>
        /// <returns></returns>
        public static CeresPortData FromParameterInfo(ParameterInfo parameterInfo)
        {
            var data = new CeresPortData
            {
                propertyName = parameterInfo.Name,
                type = SerializedType.ToString(parameterInfo.ParameterType),
                connections = Array.Empty<PortConnectionData>(),
            };
            return data;
        }

        public FieldInfo GetFieldInfo(Type inNodeType)
        {
            return inNodeType.GetField(propertyName,BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }
        
        public CeresPort GetPort(CeresNode nodeInstance)
        {
            var fieldInfo = GetFieldInfo(nodeInstance.GetType());
            if (fieldInfo == null)
            {
                return null;
            }

            if (fieldInfo.FieldType.IsArray)
            {
                var ports =fieldInfo.GetValue(nodeInstance) as CeresPort[];
                return ports?[arrayIndex];
            }
            return fieldInfo.GetValue(nodeInstance) as CeresPort;
        }
    }
}