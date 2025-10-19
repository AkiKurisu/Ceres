using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Utilities;
using Chris;
using Chris.Serialization;
using Unity.Collections.LowLevel.Unsafe;

namespace Ceres.Graph
{
    public interface IPort
    {
        object GetValue();
    }

    public interface IPort<out TValue> : IPort
    {
        TValue Value { get; }
    }

    /// <summary>
    /// Base class for ceres graph port
    /// </summary>
    public abstract class CeresPort : IPort, IDisposable
    {
        protected class PortCompatibleStructure
        {
            public readonly HashSet<Type> CompatibleTypes = new();

            public readonly Dictionary<Type, Func<IPort, IPort>> AdapterCreateFunc = new();
        }

        /// <summary>
        /// Link another <see cref="CeresPort"/> in forward path
        /// </summary>
        /// <param name="targetPort"></param>
        public abstract void Link(CeresPort targetPort);

        /// <summary>
        /// Set port default value
        /// </summary>
        /// <param name="value"></param>
        public abstract void SetValue(object value);

        /// <summary>
        /// Get port value
        /// </summary>
        /// <returns></returns>
        public abstract object GetValue();

        /// <summary>
        /// Last linked adapted port
        /// </summary>
        protected IPort AdaptedGetter { get; private set; }

        private static readonly HashSet<Type> PortValueTypeSet = new();

        private static readonly Dictionary<Type, PortCompatibleStructure> CompatibleStructures = new();

        public static void AssignValueType<T>()
        {
            if (PortValueTypeSet.Add(typeof(T)))
            {
                CompatibleStructures.Add(typeof(T), new PortCompatibleStructure());
            }
        }

        public static void AssignValueType(Type type)
        {
            if (PortValueTypeSet.Add(type))
            {
                CompatibleStructures.Add(type, new PortCompatibleStructure());
            }
        }

        public static Type[] GetAssignedPortValueTypes()
        {
            return PortValueTypeSet.ToArray();
        }

        protected static PortCompatibleStructure GetCompatibleStructure(Type type)
        {
            return CompatibleStructures.GetValueOrDefault(type);
        }

        /// <summary>
        /// Whether port value type is compatible to another
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static bool IsCompatibleTo(Type from, Type to)
        {
            var structure = GetCompatibleStructure(from);
            if (structure == null)
            {
                return from.IsAssignableTo(to);
            }
            return structure.CompatibleTypes.Contains(to) || from.IsAssignableTo(to);
        }

        public static Type[] GetCompatibleTypes(Type from)
        {
            var structure = GetCompatibleStructure(from);
            if (structure == null)
            {
                return Type.EmptyTypes;
            }

            return structure.CompatibleTypes.ToArray();
        }

        public abstract Type GetValueType();

        /// <summary>
        /// Assign this port's input value source
        /// </summary>
        /// <param name="port"></param>
        public virtual void AssignValueGetter(IPort port)
        {
            AdaptedGetter = port;
        }

        protected internal abstract IPort CreateProxyPort(CeresPort<CeresPort> ceresPort);

        public virtual void Dispose()
        {
            AdaptedGetter = null;
        }
    }

    /// <summary>
    /// Generic port for any value worked as linked list at runtime
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    [Serializable]
    public class CeresPort<TValue> : CeresPort, IPort<TValue>
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
        internal IPort<TValue> Getter;

        private static Type _valueType;

        public static readonly CeresPort<TValue> Default = new();

        public static readonly CeresPort<TValue>[] DefaultArray = Array.Empty<CeresPort<TValue>>();

        public static readonly List<CeresPort<TValue>> DefaultList = new();

        private static readonly PortCompatibleStructure CompatibleStructure;

        static CeresPort()
        {
            AssignValueType<TValue>();
            CompatibleStructure = GetCompatibleStructure(typeof(TValue));
            _valueType = typeof(TValue);
            if (!_valueType.IsEnum) return;
            MakeCompatibleTo<int>(enumValue => enumValue.GetHashCode());
            CeresPort<int>.MakeCompatibleTo<TValue>(intValue => UnsafeUtility.As<int, TValue>(ref intValue));
        }

        public CeresPort()
        {

        }

        public CeresPort(TValue value) : this()
        {
            defaultValue = value;
        }

        public TValue Value
        {
            get
            {
                /* Get value in backward */
                if (Getter != null) return Getter.Value;
                if (AdaptedGetter != null) return (TValue)AdaptedGetter.GetValue();
                return defaultValue;
            }
            set => defaultValue = value;
        }

        /// <summary>
        /// Get port value type
        /// </summary>
        /// <returns></returns>
        public override Type GetValueType()
        {
            return _valueType;
        }

        /// <summary>
        /// Register an output value convert method to make <see cref="TValue"/> is compatible to <see cref="T"/>
        /// </summary>
        /// <param name="valueConvertFunc">Func for converting input <see cref="TValue"/> to <see cref="T"/></param>
        /// <typeparam name="T"></typeparam>
        public static void MakeCompatibleTo<T>(Func<TValue, T> valueConvertFunc)
        {
            MakeCompatibleTo_Internal<T>(port => new AdapterPort<TValue, T>((CeresPort<TValue>)port, valueConvertFunc));
        }

        private static void MakeCompatibleTo_Internal<T>(Func<IPort, IPort> adapterCreateFunc)
        {
            if (CompatibleStructure.CompatibleTypes.Add(typeof(T)))
            {
                CompatibleStructure.AdapterCreateFunc.Add(typeof(T), adapterCreateFunc);
            }
        }

        private static bool IsCompatibleTo_Internal(Type type)
        {
            return CompatibleStructure.CompatibleTypes.Contains(type);
        }

        /// <summary>
        /// Is this port value type compatible to specific type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsCompatibleTo(Type type)
        {
            return IsCompatibleTo_Internal(type) || typeof(TValue).IsAssignableTo(type);
        }

        private IPort CreateAdapterPort(CeresPort ceresPort)
        {
            return CompatibleStructure.AdapterCreateFunc[ceresPort.GetValueType()](this);
        }

        protected internal override IPort CreateProxyPort(CeresPort<CeresPort> ceresPort)
        {
            return new ProxyPort<TValue>(ceresPort);
        }

        private IPort<CeresPort> CreateBridgePort(CeresPort ceresPort)
        {
            return new BridgePort(ceresPort);
        }

        public override void Link(CeresPort targetPort)
        {
            if (this is CeresPort<CeresPort> proxyPort)
            {
                /* Forward value inside proxy */
                targetPort.AssignValueGetter(targetPort.CreateProxyPort(proxyPort));
                return;
            }
            if (targetPort is CeresPort<CeresPort> bridgePort)
            {
                /* Provide connected port itself */
                bridgePort.AssignValueGetter(CreateBridgePort(this));
                return;
            }
            if (targetPort is not CeresPort<TValue> genericPort)
            {
                if (IsCompatibleTo_Internal(targetPort.GetValueType()))
                {
                    targetPort.AssignValueGetter(CreateAdapterPort(targetPort));
                    return;
                }

                targetPort.AssignValueGetter(this);
                return;
            }
            genericPort.Getter = this;
        }

        public override void AssignValueGetter(IPort port)
        {
            if (port is IPort<TValue> genericPort)
            {
                Getter = genericPort;
                return;
            }
            base.AssignValueGetter(port);
        }

        public override void SetValue(object value)
        {
            Value = (TValue)value;
        }

        public override object GetValue()
        {
            return Value;
        }

        public override void Dispose()
        {
            Getter = null;
            defaultValue = default;
            base.Dispose();
        }
    }

    public class AdapterPort<TIn, TOut> : IPort<TOut>
    {
        private readonly IPort<TIn> _port;

        private readonly Func<TIn, TOut> _adapterFunc;

        public AdapterPort(IPort<TIn> port, Func<TIn, TOut> adapterFunc)
        {
            _port = port;
            _adapterFunc = adapterFunc;
        }

        public object GetValue()
        {
            return Value;
        }

        public TOut Value => _adapterFunc(_port.Value);
    }

    internal class ProxyPort<TOut> : IPort<TOut>
    {
        private readonly CeresPort<CeresPort> _port;

        public ProxyPort(CeresPort<CeresPort> port)
        {
            _port = port;
        }

        public object GetValue()
        {
            return Value;
        }

        public TOut Value => ((CeresPort<TOut>)_port.Value).Value;
    }

    internal class BridgePort : IPort<CeresPort>
    {
        public BridgePort(CeresPort port)
        {
            Value = port;
        }

        public object GetValue()
        {
            return Value;
        }

        public CeresPort Value { get; }
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

        /// <summary>
        /// Get <see cref="CeresNode"/> if exist
        /// </summary>
        /// <returns></returns>
        public CeresNode Get()
        {
            if (Node == null) return null;
            return Node.TryGetTarget(out var node) ? node : null;
        }

        /// <summary>
        /// Get <see cref="T"/> node if exist
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetT<T>() where T : CeresNode
        {
            return Get() as T;
        }
    }

    [Serializable]
    public class PortConnectionData
    {
        public string nodeId;

        public string portId;

        public int portIndex;

        /// <summary>
        /// Whether this connection was flattened (bypassing relay nodes)
        /// If true, this connection should be skipped during deserialization
        /// </summary>
        public bool isFlattened;
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
            return inNodeType.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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
                var ports = fieldInfo.GetValue(nodeInstance) as CeresPort[];
                return ports?[arrayIndex];
            }
            return fieldInfo.GetValue(nodeInstance) as CeresPort;
        }
    }
}