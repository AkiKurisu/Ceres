using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ceres.Annotations;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    public sealed class NodeGeneratorFactory
    {
        private sealed class ResolverStructure
        {
            public readonly Type Type;

            public readonly CustomNodeGeneratorAttribute CustomNodeGeneratorAttribute;

            private readonly INodeGeneratorResolver _resolver;

            private readonly INodeGenerator _generator;

            private readonly int _order;

            public ResolverStructure(Type type)
            {
                Type = type;
                _order = type.GetCustomAttribute<OrderedAttribute>(false)?.Order ?? -1;
                if (typeof(INodeGeneratorResolver).IsAssignableFrom(type))
                {
                    _resolver = (INodeGeneratorResolver)Activator.CreateInstance(type, true);
                }
                else
                {
                    CustomNodeGeneratorAttribute = type.GetCustomAttribute<CustomNodeGeneratorAttribute>();
                    _generator = (INodeGenerator)Activator.CreateInstance(type, true);
                }
            }

            public bool IsResolverAcceptable(Type nodeType)
            {
                return _resolver != null && _resolver.IsAcceptable(nodeType);
            }

            public INodeGenerator Create(Type nodeType)
            {
                return _resolver != null ? _resolver.CreateNodeGenerator(nodeType) : _generator;
            }

            public sealed class Comparer : IComparer<ResolverStructure>
            {
                public int Compare(ResolverStructure a, ResolverStructure b)
                {
                    var aCustom = a!._resolver == null;
                    var bCustom = b!._resolver == null;
                    if (aCustom && bCustom)
                    {
                        if (IsMoreSpecific(a.CustomNodeGeneratorAttribute.NodeType,
                                b.CustomNodeGeneratorAttribute.NodeType))
                        {
                            return -1;
                        }

                        if (IsMoreSpecific(b.CustomNodeGeneratorAttribute.NodeType,
                                a.CustomNodeGeneratorAttribute.NodeType))
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

        private static NodeGeneratorFactory _default;

        public static NodeGeneratorFactory Get()
        {
            return _default ??= new NodeGeneratorFactory();
        }

        private readonly List<ResolverStructure> _resolvers;

        private readonly Dictionary<Type, INodeGenerator> _cache = new();

        private readonly HashSet<Type> _misses = new();

        private NodeGeneratorFactory()
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

        public bool TryGetGenerator(Type nodeType, out INodeGenerator generator)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(nodeType, out generator))
                {
                    return true;
                }

                if (_misses.Contains(nodeType))
                {
                    generator = null;
                    return false;
                }
            }

            generator = ResolveGenerator(nodeType);
            lock (_cache)
            {
                if (generator != null)
                {
                    _cache[nodeType] = generator;
                    return true;
                }

                _misses.Add(nodeType);
                return false;
            }
        }

        private INodeGenerator ResolveGenerator(Type nodeType)
        {
            var customGenerators = _resolvers
                .Where(resolver => resolver.CustomNodeGeneratorAttribute != null &&
                                   TryAcceptNodeGenerator(resolver.CustomNodeGeneratorAttribute, nodeType))
                .ToList();
            customGenerators.Sort(new ResolverStructure.Comparer());
            var customGenerator = customGenerators.FirstOrDefault();
            if (customGenerator != null)
            {
                return customGenerator.Create(nodeType);
            }

            foreach (var resolver in _resolvers.Except(customGenerators))
            {
                if (!resolver.IsResolverAcceptable(nodeType)) continue;
                return resolver.Create(nodeType);
            }

            return null;
        }

        private static bool IsValidType(Type type)
        {
            if (type.IsAbstract) return false;
            if (type.GetCustomAttribute<CustomNodeGeneratorAttribute>() is { NodeType: not null } &&
                typeof(INodeGenerator).IsAssignableFrom(type))
            {
                return true;
            }

            return typeof(INodeGeneratorResolver).IsAssignableFrom(type);
        }

        private static bool TryAcceptNodeGenerator(CustomNodeGeneratorAttribute attribute, Type nodeType)
        {
            return TypeMatches(attribute.NodeType, nodeType) ||
                   attribute.CanInherit && IsAssignableFrom(attribute.NodeType, nodeType);
        }

        private static bool TypeMatches(Type expectedType, Type nodeType)
        {
            if (expectedType == nodeType) return true;
            return expectedType.IsGenericTypeDefinition &&
                   nodeType.IsGenericType &&
                   nodeType.GetGenericTypeDefinition() == expectedType;
        }

        private static bool IsAssignableFrom(Type expectedType, Type nodeType)
        {
            if (expectedType.IsAssignableFrom(nodeType)) return true;
            if (!expectedType.IsGenericTypeDefinition) return false;
            return EnumerateSelfBaseTypes(nodeType)
                .Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == expectedType);
        }

        private static bool IsMoreSpecific(Type a, Type b)
        {
            if (a == b) return false;
            return IsAssignableFrom(b, a);
        }

        private static IEnumerable<Type> EnumerateSelfBaseTypes(Type type)
        {
            while (type != null)
            {
                yield return type;
                foreach (var interfaceType in type.GetInterfaces())
                {
                    yield return interfaceType;
                }

                type = type.BaseType;
            }
        }
    }
}
