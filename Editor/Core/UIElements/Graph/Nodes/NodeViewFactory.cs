using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace Ceres.Editor.Graph
{
    /// <summary>
    /// Interface for custom node view resolver
    /// </summary>
    public interface INodeViewResolver
    {
        ICeresNodeView CreateNodeView(Type type, CeresGraphView graphView);

        bool IsAcceptable(Type nodeType);
    }

    public sealed class NodeViewFactory
    {
        public class ResolverStructure
        {
            public readonly Type Type;
            
            public readonly INodeViewResolver Instance;

            public readonly CustomNodeViewAttribute CustomNodeViewAttribute;

            public readonly int Order;
            
            public ResolverStructure(Type type)
            {
                Type = type;
                if (type.GetInterfaces().Any(t => t == typeof(INodeViewResolver)))
                {
                    Instance = (INodeViewResolver)Activator.CreateInstance(type);
                }
                else
                {
                    CustomNodeViewAttribute = type.GetCustomAttribute<CustomNodeViewAttribute>();
                }
                Order = type.GetCustomAttribute<OrderedAttribute>(false)?.Order ?? -1;
            }

            public bool IsAcceptable(Type inType)
            {
                if (Instance == null) return false;
                return Instance.IsAcceptable(inType);
            }
            
            public class Comparer: IComparer<ResolverStructure>
            {
                public int Compare(ResolverStructure a, ResolverStructure b)
                {
                    bool aCustom = a!.Instance == null;
                    bool bCustom = b!.Instance == null;
                    if (aCustom && bCustom)
                    {
                        if (a.CustomNodeViewAttribute.NodeType.IsAssignableFrom(b.CustomNodeViewAttribute.NodeType))
                        {
                            return 1;
                        }
                    }
                    if (!aCustom && bCustom) return 1;
                    if (aCustom && !bCustom) return -1;
                    return a.Order.CompareTo(b.Order);
                }
            }
        }
        
        private static NodeViewFactory _default;

        public static NodeViewFactory Get()
        {
            return _default ??= new NodeViewFactory();
        }
        
        private readonly List<ResolverStructure> _resolvers;

        private NodeViewFactory()
        {
            _resolvers = AppDomain.CurrentDomain
                                        .GetAssemblies()
                                        .Select(x => x.GetTypes())
                                        .SelectMany(x => x)
                                        .Where(IsValidType)
                                        .Select(x=> new ResolverStructure(x))
                                        .ToList();
            _resolvers.Sort(new ResolverStructure.Comparer());
        }
        private static bool IsValidType(Type type)
        {
            if (type.IsAbstract) return false;
            if (type.GetCustomAttribute<CustomNodeViewAttribute>() != null) return true;
            return type.GetInterfaces().Any(t => t == typeof(INodeViewResolver));
        }
        
        /// <summary>
        /// Create node view instance
        /// </summary>
        /// <param name="type"></param>
        /// <param name="graphView"></param>
        /// <returns></returns>
        public ICeresNodeView CreateInstance(Type type, CeresGraphView graphView)
        {
            var customNodeResolvers = _resolvers
                .Where(resolver => resolver.CustomNodeViewAttribute != null 
                                   && TryAcceptNodeEditor(resolver.CustomNodeViewAttribute, type))
                .ToList();
            customNodeResolvers.Sort(new ResolverStructure.Comparer());
            var customNodeResolver = customNodeResolvers.FirstOrDefault();
            if(customNodeResolver != null)
            {
                var viewType = customNodeResolver.Type;
                /* Must have (Type, CeresGraphView) constructor */
                return (ICeresNodeView)Activator.CreateInstance(viewType, type, graphView);
            }
            
            foreach (var resolver in _resolvers)
            {
                if (!resolver.IsAcceptable(type)) continue;
                return ((INodeViewResolver)Activator.CreateInstance(resolver.Type)).CreateNodeView(type, graphView);
            }
            return null;
        }
        
        /// <summary>
        /// Create node view instance with generic resolving
        /// </summary>
        /// <param name="type"></param>
        /// <param name="graphView"></param>
        /// <param name="genericArguments"></param>
        /// <returns></returns>
        public ICeresNodeView CreateInstanceResolved(Type type, CeresGraphView graphView, params Type[] genericArguments)
        {
            var customNodeResolvers = _resolvers
                .Where(resolver => resolver.CustomNodeViewAttribute != null 
                                   && TryAcceptNodeEditor(resolver.CustomNodeViewAttribute, type))
                .ToList();
            customNodeResolvers.Sort(new ResolverStructure.Comparer());
            var customNodeResolver = customNodeResolvers.FirstOrDefault();
            if(customNodeResolver != null)
            {
                var viewType = customNodeResolver.Type;
                if (viewType.IsGenericType)
                {
                    viewType = viewType.MakeGenericType(genericArguments);
                    return (ICeresNodeView)Activator.CreateInstance(viewType, type, graphView);
                }

                if (type.IsGenericTypeDefinition)
                {
                    type = type.MakeGenericType(genericArguments);
                }
                /* Must have (Type, CeresGraphView) constructor */
                return (ICeresNodeView)Activator.CreateInstance(viewType, type, graphView);
            }
            
            foreach (var resolver in _resolvers)
            {
                if (type.IsGenericTypeDefinition)
                {
                    type = type.MakeGenericType(genericArguments);
                }
                if (!resolver.IsAcceptable(type)) continue;
                return ((INodeViewResolver)Activator.CreateInstance(resolver.Type)).CreateNodeView(type, graphView);
            }
            return null;
        }
        
        private static bool TryAcceptNodeEditor(CustomNodeViewAttribute attribute, Type nodeType)
        {
            if (attribute.NodeType == nodeType) return true;
            return attribute.CanInherit && attribute.NodeType.IsAssignableFrom(nodeType);
        }
    }
}