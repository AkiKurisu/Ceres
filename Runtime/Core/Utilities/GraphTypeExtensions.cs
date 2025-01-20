using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Graph;
using Chris;
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
        
        public static IEnumerable<IGrouping<string, TMemberInfo>> GroupsByNodeGroup<TMemberInfo>(this IEnumerable<TMemberInfo> types) where TMemberInfo: MemberInfo
        {
            return types.GroupBy(SubClassSearchUtility.GetFirstGroupNameOrDefault).Where(x => !string.IsNullOrEmpty(x.Key));
        }
        
        public static IEnumerable<IGrouping<string, TMemberInfo>> SubGroups<TMemberInfo>(this IGrouping<string, TMemberInfo> group, int level) where TMemberInfo: MemberInfo
        {
            return group.GroupBy(t =>
            {
                var groupAttribute = t.GetCustomAttribute<CeresGroupAttribute>();
                var subcategory = SubClassSearchUtility.SplitGroupName(groupAttribute.Group);
                return subcategory.Length > level ? subcategory[level] : null;
            }).Where(x => !string.IsNullOrEmpty(x.Key));
        }
        
        public static IEnumerable<IGrouping<string, Type>> SelectSubclass(this IEnumerable<IGrouping<string, Type>> groups, Type baseType)
        {
            return groups.SelectMany(x => x).Where(x => x.IsAssignableTo(baseType)).GroupsByNodeGroup();
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
    }
}