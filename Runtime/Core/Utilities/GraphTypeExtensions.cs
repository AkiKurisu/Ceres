using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Graph;
using Chris;
using UObject = UnityEngine.Object;

namespace Ceres.Utilities
{
     public static class GraphTypeExtensions
    {
        /// <summary>
        /// Get all <see cref="FieldInfo"/> of visible properties in Graph View and Editor Window
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static List<FieldInfo> GetGraphEditorPropertyFields(this Type t)
        {
            return ReflectionUtility.GetSerializedFields(t)
                                    .Where(field =>
                                    {
                                        if (field.GetCustomAttribute<HideInGraphEditorAttribute>() != null)
                                            return false;
                                        // Ignore port in properties
                                        if (field.FieldType.IsSubclassOf(typeof(CeresPort))) 
                                            return false;
                                        // Ignore port array in properties
                                        if (field.FieldType.IsArray && field.FieldType.GetElementType()!.IsSubclassOf(typeof(CeresPort))) 
                                            return false;
                                        return true;
                                    })
                                    .ToList();
        }
        
        /// <summary>
        /// Get all <see cref="FieldInfo"/> of visible ports in Graph View and Editor Window
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static List<FieldInfo> GetGraphEditorPortFields(this Type t)
        {
            return ReflectionUtility.GetSerializedFields(t)
                                    .Where(field =>
                                    {
                                        if(field.FieldType.IsArray && field.FieldType.GetElementType()!.IsSubclassOf(typeof(CeresPort))) 
                                            return true;
                                        return field.FieldType.IsSubclassOf(typeof(CeresPort));
                                    })
                                    .ToList();
        }

        /// <summary>
        /// Determines whether current type can be assigned to another instance of type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static bool IsAssignableTo(this Type type, Type other)
        {
            return other.IsAssignableFrom(type) || other == typeof(object);
        }
        
        public static bool IsSharedTObject(this Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SharedUObject<>);
        }
                
        public static bool IsIList(this Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return true;
            return type.IsArray;
        }
        
        // ReSharper disable once UnusedMember.Global
        public static bool IsIListPort(this Type fieldType)
        {
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var genericArgument = fieldType.GetGenericArguments()[0];
                if (typeof(CeresPort).IsAssignableFrom(genericArgument))
                {
                    return true;
                }
            }
            else if (fieldType.IsArray)
            {
                var elementType = fieldType.GetElementType();
                if (typeof(CeresPort).IsAssignableFrom(elementType))
                {
                    return true;
                }
            }
            return false;
        }
                
        // ReSharper disable once UnusedMember.Global
        public static bool IsIListVariable(this Type fieldType)
        {
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var genericArgument = fieldType.GetGenericArguments()[0];
                if (typeof(SharedVariable).IsAssignableFrom(genericArgument))
                {
                    return true;
                }
            }
            else if (fieldType.IsArray)
            {
                var elementType = fieldType.GetElementType();
                if (typeof(SharedVariable).IsAssignableFrom(elementType))
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Determines whether the specified type inherits from the specified generic type definition, and obtains the generic parameter type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="genericDefinition"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static bool IsInheritedFromGenericDefinition(this Type type, Type genericDefinition, out Type[] arguments)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition)
            {
                arguments = type.GetGenericArguments();
                return true;
            }
            
            if (type.BaseType != null)
            {
                if (IsInheritedFromGenericDefinition(type.BaseType, genericDefinition, out arguments))
                {
                    return true;
                }
            }
            
            foreach (var interfaceType in type.GetInterfaces())
            {
                if (IsInheritedFromGenericDefinition(interfaceType, genericDefinition, out arguments))
                {
                    return true;
                }
            }

            arguments = Type.EmptyTypes;
            return false;
        }
        
        /// <summary>
        /// Whether type instance can be serialized in Ceres graph editor
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsCeresSerializable(this Type type)
        {
            if (type.IsGenericType) return false;
                        
            if(typeof(List<>).IsAssignableFrom(type) || type.IsArray)
            {
                return false;
            }

            if (type.Name == "<>c")
            {
                return false;
            }
                        
            if (typeof(Attribute).IsAssignableFrom(type))
            {
                return false;
            }
                        
            if (type.Assembly.GetName().Name.Contains("Editor"))
            {
                return false;
            }
            
            if (typeof(UObject).IsAssignableFrom(type) || type == typeof(UObject))
            {
                return true;
            }

            if (ReflectionUtility.IsSerializableNumericTypes(type))
            {
                return true;
            }
                        
            if (ReflectionUtility.IsUnityBuiltinTypes(type))
            {
                return true;
            }

            if (Attribute.IsDefined(type, typeof(SerializableAttribute), true))
            {
                return true;
            }
              
            /* Non-serializable type only visible when assigned where only in the case user want to use it */
            return CeresPort.GetAssignedPortValueTypes().Contains(type);
        }
    }

    public static class MemberInfoGroupingExtensions
    {
        public static IEnumerable<IGrouping<string, TMemberInfo>> GroupByFirstGroup<TMemberInfo>(this IEnumerable<TMemberInfo> types) where TMemberInfo: MemberInfo
        {
            return types.GroupBy(SubClassSearchUtility.GetFirstGroupNameOrDefault).Where(x => !string.IsNullOrEmpty(x.Key));
        }
        
        public static IEnumerable<IGrouping<string, TMemberInfo>> SubGroup<TMemberInfo>(this IGrouping<string, TMemberInfo> group, int level) where TMemberInfo: MemberInfo
        {
            return group.GroupBy(t =>
            {
                var groupAttribute = t.GetCustomAttribute<CeresGroupAttribute>();
                if (groupAttribute == null) return null;
                var subcategory = SubClassSearchUtility.SplitGroupName(groupAttribute.Group);
                return subcategory.Length > level ? subcategory[level] : null;
            }).Where(grouping => !string.IsNullOrEmpty(grouping.Key));
        }
        
        public static IEnumerable<IGrouping<string, Type>> SelectSubclass(this IEnumerable<IGrouping<string, Type>> groups, Type baseType)
        {
            return groups.SelectMany(x => x).Where(x => x.IsAssignableTo(baseType)).GroupByFirstGroup();
        }
        
        public static IEnumerable<IGrouping<string, TMemberInfo>> SelectGroup<TMemberInfo>(this IEnumerable<IGrouping<string, TMemberInfo>> groups, string[] showGroupNames)
        {
            if (showGroupNames == null || showGroupNames.Length == 0)
            {
                return groups;
            }
            return groups.Where(x => showGroupNames.Any(a => a == x.Key));
        }
        
        public static IEnumerable<IGrouping<string, TMemberInfo>> ExceptGroup<TMemberInfo>(this IEnumerable<IGrouping<string, TMemberInfo>> groups, string[] notShowGroupNames)
        {
            if (notShowGroupNames == null || notShowGroupNames.Length == 0)
            {
                return groups;
            }
            return groups.Where(x => notShowGroupNames.All(a => a != x.Key));
        }
    }

    /// <summary>
    /// Interface for let object can be grouped by <see cref="GroupedGroupingExtensions"/>
    /// </summary>
    public interface IGrouped
    {
        string Group { get; }
    }
    
    public static class GroupedGroupingExtensions
    {
        public static IEnumerable<IGrouping<string, T>> SubGroup<T>(this IGrouping<string, T> group, int level) where T: IGrouped
        {
            return group.GroupBy(t =>
            {
                var subcategory = SubClassSearchUtility.SplitGroupName(t.Group);
                return subcategory.Length > level ? subcategory[level] : null;
            }).Where(x => !string.IsNullOrEmpty(x.Key));
        }
    }
}