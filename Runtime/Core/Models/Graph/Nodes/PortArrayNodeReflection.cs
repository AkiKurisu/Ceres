using System;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using UnityEngine.Scripting;
namespace Ceres.Graph
{
    /// <summary>
    /// Interface for node has port array
    /// </summary>
    public interface IReadOnlyPortArrayNode
    {
        int GetPortArraySize();

        string GetPortArrayFieldName();
    }

    /// <summary>
    /// Interface for node has port array that can be resized
    /// </summary>
    public interface IPortArrayNode: IReadOnlyPortArrayNode
    {
        void SetPortArraySize(int newSize);
    }

    public abstract class PortArrayNodeReflection
    {
        public FieldInfo PortArrayField { get; protected set; }

        public string PortArrayLabel { get; protected set; }

        public IReadOnlyPortArrayNode DefaultNode { get; protected set; }

        public int DefaultArraySize { get; protected set; }

        public static PortArrayNodeReflection Get(Type nodeType)
        {
            return (PortArrayNodeReflection)typeof(PortArrayNodeReflection<>)
                .MakeGenericType(nodeType)
                .GetMethod("GetOrCreate")!
                .Invoke(null, Array.Empty<object>());
        }
    }
    
    public class PortArrayNodeReflection<TNode>: PortArrayNodeReflection where TNode: CeresNode, IReadOnlyPortArrayNode, new()
    {
        private static PortArrayNodeReflection<TNode> _instance;

        private PortArrayNodeReflection()
        {
            DefaultNode = new TNode();
            PortArrayField = typeof(TNode).GetField(DefaultNode.GetPortArrayFieldName(), BindingFlags.Instance | BindingFlags.Public);
            PortArrayLabel = CeresLabel.GetLabel(PortArrayField);
            DefaultArraySize = Math.Max(GetDefaultPortArrayLength(), DefaultNode.GetPortArraySize());
        }

        [Preserve]
        public static PortArrayNodeReflection<TNode> GetOrCreate()
        {
            return _instance ??= new PortArrayNodeReflection<TNode>();
        }
        
        private int GetDefaultPortArrayLength()
        {
            var str = CeresMetadata.GetMetadata(PortArrayField, "DefaultLength").FirstOrDefault();
            if (string.IsNullOrEmpty(str) || !int.TryParse(str, out var length))
            {
                return 0;
            }
            return length;
        }
    }
}