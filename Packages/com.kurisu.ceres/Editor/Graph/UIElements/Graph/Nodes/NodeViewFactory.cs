using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ceres.Annotations;

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
            
            public readonly CustomNodeViewAttribute CustomNodeViewAttribute;

            private readonly INodeViewResolver _instance;
            
            private readonly int _order;
            
            public ResolverStructure(Type type)
            {
                Type = type;
                if (type.GetInterfaces().Any(t => t == typeof(INodeViewResolver)))
                {
                    _instance = (INodeViewResolver)Activator.CreateInstance(type);
                }
                else
                {
                    CustomNodeViewAttribute = type.GetCustomAttribute<CustomNodeViewAttribute>();
                }
                _order = type.GetCustomAttribute<OrderedAttribute>(false)?.Order ?? -1;
            }

            public bool IsAcceptable(Type inType)
            {
                if (_instance == null) return false;
                return _instance.IsAcceptable(inType);
            }
            
            public class Comparer: IComparer<ResolverStructure>
            {
                public int Compare(ResolverStructure a, ResolverStructure b)
                {
                    bool aCustom = a!._instance == null;
                    bool bCustom = b!._instance == null;
                    if (aCustom && bCustom)
                    {
                        if (a.CustomNodeViewAttribute.NodeType.IsAssignableFrom(b.CustomNodeViewAttribute.NodeType))
                        {
                            return 1;
                        }
                    }
                    if (!aCustom && bCustom) return 1;
                    if (aCustom && !bCustom) return -1;
                    return a._order.CompareTo(b._order);
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
            var validTypes = new ConcurrentBag<Type>();
            
            Parallel.ForEach(AppDomain.CurrentDomain.GetAssemblies(), assembly =>
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (IsValidType(type))
                        {
                            validTypes.Add(type);
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    foreach (var type in ex.Types)
                    {
                        if (type != null && IsValidType(type))
                        {
                            validTypes.Add(type);
                        }
                    }
                }
            });
            
            var resolverList = new ConcurrentBag<ResolverStructure>();
            Parallel.ForEach(validTypes, type =>
            {
                resolverList.Add(new ResolverStructure(type));
            });
            
            _resolvers = resolverList.ToList();
            _resolvers.Sort(new ResolverStructure.Comparer());
        }
        
        private static bool IsValidType(Type type)
        {
            if (type.IsAbstract) return false;
            var customViewAttribute = type.GetCustomAttribute<CustomNodeViewAttribute>();
            if (customViewAttribute is { NodeType: not null }) return true;
            return type.GetInterfaces().Any(t => t == typeof(INodeViewResolver));
        }
        
        /// <summary>
        /// Create node view instance
        /// </summary>
        /// <param name="nodeType"></param>
        /// <param name="graphView"></param>
        /// <returns></returns>
        public ICeresNodeView CreateInstance(Type nodeType, CeresGraphView graphView)
        {
            var customNodeResolvers = _resolvers
                .Where(resolver => resolver.CustomNodeViewAttribute != null 
                                   && TryAcceptNodeEditor(resolver.CustomNodeViewAttribute, nodeType))
                .ToList();
            if (CeresMetadata.IsDefined(nodeType, "ResolverOnly"))
            {
                goto UseResolver;
            }
            customNodeResolvers.Sort(new ResolverStructure.Comparer());
            var customNodeResolver = customNodeResolvers.FirstOrDefault();
            if (customNodeResolver != null)
            {
                var viewType = customNodeResolver.Type;
                /* Must have (Type, CeresGraphView) constructor */
                return (ICeresNodeView)Activator.CreateInstance(viewType, nodeType, graphView);
            }
            
            UseResolver:
            foreach (var resolver in _resolvers.Except(customNodeResolvers))
            {
                if (!resolver.IsAcceptable(nodeType)) continue;
                return ((INodeViewResolver)Activator.CreateInstance(resolver.Type)).CreateNodeView(nodeType, graphView);
            }
            return null;
        }
        
        /// <summary>
        /// Create node view instance with generic resolving
        /// </summary>
        /// <param name="nodeType"></param>
        /// <param name="graphView"></param>
        /// <param name="genericArguments"></param>
        /// <returns></returns>
        public ICeresNodeView CreateInstanceResolved(Type nodeType, CeresGraphView graphView, params Type[] genericArguments)
        {
            var customNodeResolvers = _resolvers
                .Where(resolver => resolver.CustomNodeViewAttribute != null 
                                   && TryAcceptNodeEditor(resolver.CustomNodeViewAttribute, nodeType))
                .ToList();
            if (CeresMetadata.IsDefined(nodeType, "ResolverOnly"))
            {
                goto UseResolver;
            }
            customNodeResolvers.Sort(new ResolverStructure.Comparer());
            var customNodeResolver = customNodeResolvers.FirstOrDefault();
            if (customNodeResolver != null)
            {
                var viewType = customNodeResolver.Type;
                if (viewType.IsGenericType)
                {
                    viewType = viewType.MakeGenericType(genericArguments);
                    return (ICeresNodeView)Activator.CreateInstance(viewType, nodeType, graphView);
                }

                if (nodeType.IsGenericTypeDefinition)
                {
                    nodeType = nodeType.MakeGenericType(genericArguments);
                }
                /* Must have (Type, CeresGraphView) constructor */
                return (ICeresNodeView)Activator.CreateInstance(viewType, nodeType, graphView);
            } 
            UseResolver:
            foreach (var resolver in _resolvers.Except(customNodeResolvers))
            {
                if (nodeType.IsGenericTypeDefinition)
                {
                    nodeType = nodeType.MakeGenericType(genericArguments);
                }
                if (!resolver.IsAcceptable(nodeType)) continue;
                return ((INodeViewResolver)Activator.CreateInstance(resolver.Type)).CreateNodeView(nodeType, graphView);
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