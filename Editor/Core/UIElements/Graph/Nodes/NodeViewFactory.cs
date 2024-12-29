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
    }

    public sealed class NodeViewFactory
    {
        private static NodeViewFactory _default;

        public static NodeViewFactory Get()
        {
            return _default ??= new NodeViewFactory();
        }
        
        private readonly List<Type> _resolverTypes;

        private NodeViewFactory()
        {
            _resolverTypes = AppDomain.CurrentDomain
                                        .GetAssemblies()
                                        .Select(x => x.GetTypes())
                                        .SelectMany(x => x)
                                        .Where(IsValidType)
                                        .ToList();
            _resolverTypes.Sort((a, b) =>
            {
                var aCustom = a.GetCustomAttribute<CustomNodeViewAttribute>(false);
                var bCustom = b.GetCustomAttribute<CustomNodeViewAttribute>(false);
                var aOrdered = a.GetCustomAttribute<OrderedAttribute>(false);
                var bOrdered = b.GetCustomAttribute<OrderedAttribute>(false);
                if (aCustom == null && bCustom != null) return 1;
                if (aCustom != null && bCustom == null) return -1;
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
            foreach (var resolverType in _resolverTypes)
            {
                var attribute = resolverType.GetCustomAttribute<CustomNodeViewAttribute>();
                if (attribute != null)
                {
                    if (TryAcceptNodeEditor(attribute, type))
                    {
                        /* Must have (Type, CeresGraphView) constructor */
                        node = (ICeresNodeView)Activator.CreateInstance(resolverType, type, graphView);
                        break;
                    }
                    continue;
                }
                if (!IsAcceptable(resolverType, type)) continue;
                node = ((INodeViewResolver)Activator.CreateInstance(resolverType)).CreateNodeView(type, graphView);
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
            foreach (var resolverType in _resolverTypes)
            {
                var attribute = resolverType.GetCustomAttribute<CustomNodeViewAttribute>();
                if (attribute != null)
                {
                    if (TryAcceptNodeEditor(attribute, type))
                    {
                        var viewType = resolverType;
                        if (viewType.IsGenericType)
                        {
                            viewType = resolverType.MakeGenericType(genericArguments);
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
                if (!IsAcceptable(resolverType, type)) continue;
                node = ((INodeViewResolver)Activator.CreateInstance(resolverType)).CreateNodeView(type, graphView);
                break;
            }
            return node;
        }
        private static bool TryAcceptNodeEditor(CustomNodeViewAttribute attribute, Type nodeType)
        {
            if (attribute.NodeType == nodeType) return true;
            return attribute.CanInherit && attribute.NodeType.IsAssignableFrom(nodeType);
        }
        private static bool IsAcceptable(Type type, Type behaviorType)
        {
            return (bool)type.InvokeMember("IsAcceptable", BindingFlags.InvokeMethod, null, null, new object[] { behaviorType });
        }
    }
}