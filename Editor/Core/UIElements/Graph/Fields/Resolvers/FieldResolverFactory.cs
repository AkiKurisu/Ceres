using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ceres.Utilities;
using Chris.Serialization;
using UnityEngine.Assertions;
using UObject = UnityEngine.Object;

namespace Ceres.Editor.Graph
{
    /// <summary>
    /// Use this attribute to let factory input child resolver as second constructor argument
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ResolveChildAttribute : Attribute
    {

    }
    
    public class FieldResolverFactory
    {
        public readonly struct InlineIMGUIAutoScope: IDisposable
        {
            private readonly bool _isInline;

            public InlineIMGUIAutoScope(bool isInline)
            {
                _isInline = InlineIMGUI;
                InlineIMGUI = isInline;
            }

            public void Dispose()
            {
                InlineIMGUI = _isInline;
            }
        }
        
        private class ResolverStructure
        {
            public readonly Type Type;
            
            public IFieldResolver Instance;

            public ResolverStructure(Type type, IFieldResolver instance)
            {
                Type = type;
                Instance = instance;
            }
        }
        
        private static FieldResolverFactory _instance;
        
        private readonly List<ResolverStructure> _resolvers;
        
        private static readonly object[] Parameters = { null };

        public static bool InlineIMGUI { get; private set; }
        
        public static FieldResolverFactory Get()
        {
            return _instance ?? new FieldResolverFactory();
        }

        private FieldResolverFactory()
        {
            _instance = this;
            var validTypes = new ConcurrentBag<Type>();
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            Parallel.ForEach(allAssemblies, assembly =>
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

            var resolverTypes = validTypes.ToList();

            resolverTypes.Sort((a, b) =>
            {
                var aOrdered = a.GetCustomAttribute<OrderedAttribute>(false);
                var bOrdered = b.GetCustomAttribute<OrderedAttribute>(false);
                if (aOrdered == null && bOrdered == null) return 0;
                if (aOrdered != null && bOrdered != null) return aOrdered.Order - bOrdered.Order;
                if (aOrdered != null) return -1;
                return 1;
            });
            
            var parameters = new object[]{ null };
            var resolverList = new ConcurrentBag<ResolverStructure>();
            Parallel.ForEach(resolverTypes, type =>
            {
                if (type.IsGenericTypeDefinition)
                {
                    resolverList.Add(new ResolverStructure(type, null));
                }
                else
                {
                    var instance = (IFieldResolver)Activator.CreateInstance(type, parameters);
                    resolverList.Add(new ResolverStructure(type, instance));
                }
            });
            
            _resolvers = resolverList.ToList();
        }
        
        private static bool IsValidType(Type type)
        {
            if (type.IsAbstract) return false;
            if (type.GetMethod("IsAcceptable", BindingFlags.Instance | BindingFlags.Public) == null) 
                return false;
            /* Need have default constructor */
            if (type.GetConstructors().All(x => x.GetParameters().Length != 1)) return false;
            if (type == typeof(ObjectResolver)) return false;
            if (type.GetInterfaces().All(t => t != typeof(IFieldResolver))) return false;
            return true;
        }
        
        public IFieldResolver Create(FieldInfo fieldInfo)
        {
            Assert.IsNotNull(fieldInfo);
            return Create(fieldInfo.FieldType, fieldInfo);
        }
        
        public IFieldResolver Create(Type fieldType, FieldInfo fieldInfo = null)
        {
            Assert.IsNotNull(fieldType);
            var parameterType = SerializedType.GenericType(fieldType);
            foreach (var resolver in _resolvers)
            {
                var resolverType = resolver.Type;
                // Try to resolve a generic resolver for this field
                // Can be easier to implement custom field
                if (resolverType.IsGenericTypeDefinition)
                {
                    try
                    {
                        resolverType = resolverType.MakeGenericType(parameterType);
                        resolver.Instance = (IFieldResolver)Activator.CreateInstance(resolverType, Parameters);
                    }
                    catch
                    {
                        continue;
                    }
                }
                if (!resolver.Instance.IsAcceptable(fieldType, fieldInfo)) continue;
                // Identify the list field whether it should resolve its child
                if (resolverType.GetCustomAttribute<ResolveChildAttribute>(false) != null)
                    return (IFieldResolver)Activator.CreateInstance(resolverType, fieldInfo, GetChildResolver(parameterType, fieldInfo));
                
                return (IFieldResolver)Activator.CreateInstance(resolverType, fieldInfo);
            }

            if (!fieldType.IsIList())
            {
                if (fieldType.IsAssignableTo(typeof(UObject)))
                {
                    return new ObjectResolver(fieldInfo);
                }

                var wrapperType = typeof(WrapFieldResolver<>).MakeGenericType(fieldType);
                return (IFieldResolver)Activator.CreateInstance(wrapperType, fieldInfo);
            }
            // Special case: IList<Object>
            IFieldResolver childResolver = GetChildResolver(parameterType, fieldInfo);
            if (childResolver == null)
                return (IFieldResolver)Activator.CreateInstance(typeof(ObjectListResolver<>).MakeGenericType(parameterType), fieldInfo);
            return (IFieldResolver)Activator.CreateInstance(typeof(ListResolver<>).MakeGenericType(parameterType), fieldInfo, childResolver);
        }
        
        private IFieldResolver GetChildResolver(Type childFieldType, FieldInfo fatherFieldInfo)
        {
            foreach (var resolver in _resolvers)
            {
                if (resolver.Instance.IsAcceptable(childFieldType, fatherFieldInfo))
                    return (IFieldResolver)Activator.CreateInstance(resolver.Type, fatherFieldInfo);
            }
            return null;
        }
        
        public static InlineIMGUIAutoScope InlineIMGUIAuto(bool isInline)
        {
            return new InlineIMGUIAutoScope(isInline);
        }
    }
}