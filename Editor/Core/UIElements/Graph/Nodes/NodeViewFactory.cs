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
            }

            public bool IsAcceptable(Type inType)
            {
                if (Instance == null) return false;
                return Instance.IsAcceptable(inType);
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
            _resolvers.Sort((a, b) =>
            {
                var aCustom = a.Instance == null;
                var bCustom = b.Instance == null;
                var aOrdered = a.Type.GetCustomAttribute<OrderedAttribute>(false);
                var bOrdered = b.Type.GetCustomAttribute<OrderedAttribute>(false);
                if (!aCustom && bCustom) return 1;
                if (aCustom && !bCustom) return -1;
                if (aOrdered == null && bOrdered == null) return 0;
                if (aOrdered != null && bOrdered != null) return aOrdered.Order - bOrdered.Order;
                if (aOrdered != null) return -1;
                return 1;
            });
        }
        private static bool IsValidType(Type type)
        {
            if (type.IsAbstract) return false;
            if (type.GetCustomAttribute<CustomNodeViewAttribute>() != null) return true;
            if (type.GetMethod("IsAcceptable") == null) return false;
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
            ICeresNodeView node = null;
            foreach (var resolver in _resolvers)
            {
                var attribute = resolver.CustomNodeViewAttribute;
                if (attribute != null)
                {
                    if (TryAcceptNodeEditor(attribute, type))
                    {
                        /* Must have (Type, CeresGraphView) constructor */
                        node = (ICeresNodeView)Activator.CreateInstance(resolver.Type, type, graphView);
                        break;
                    }
                    continue;
                }
                if (!resolver.IsAcceptable(type)) continue;
                node = ((INodeViewResolver)Activator.CreateInstance(resolver.Type)).CreateNodeView(type, graphView);
                break;
            }
            return node;
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
            ICeresNodeView node = null;
            foreach (var resolver in _resolvers)
            {
                var attribute = resolver.CustomNodeViewAttribute;
                if (attribute != null)
                {
                    if (TryAcceptNodeEditor(attribute, type))
                    {
                        var viewType = resolver.Type;
                        if (viewType.IsGenericType)
                        {
                            viewType = viewType.MakeGenericType(genericArguments);
                            node = (ICeresNodeView)Activator.CreateInstance(viewType, type, graphView);
                        }
                        else
                        {
                            if (type.IsGenericTypeDefinition)
                            {
                                type = type.MakeGenericType(genericArguments);
                            }
                            node = (ICeresNodeView)Activator.CreateInstance(viewType, type, graphView);
                        }
                        break;
                    }
                    continue;
                }

                if (type.IsGenericTypeDefinition)
                {
                    type = type.MakeGenericType(genericArguments);
                }
                if (!resolver.IsAcceptable(type)) continue;
                node = ((INodeViewResolver)Activator.CreateInstance(resolver.Type)).CreateNodeView(type, graphView);
                break;
            }
            return node;
        }
        
        private static bool TryAcceptNodeEditor(CustomNodeViewAttribute attribute, Type nodeType)
        {
            if (attribute.NodeType == nodeType) return true;
            return attribute.CanInherit && attribute.NodeType.IsAssignableFrom(nodeType);
        }
    }
}