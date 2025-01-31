using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
namespace Ceres.Utilities
{
    public static class SubClassSearchUtility
    {
        private const char Span = '/';
        
        public static List<Type> FindSubClassTypes(Type searchType)
        {
            return FindSubClassTypes(AppDomain.CurrentDomain.GetAssemblies(), searchType);
        }
        
        public static List<Type> FindSubClassTypes(IEnumerable<Assembly> assemblies, Type searchType)
        {
            return assemblies.SelectMany(a => a.GetTypes())
                                .Where(t => t.IsAssignableTo(searchType) && !t.IsAbstract)
                                .ToList();
        }

        public static List<Type> FindSubClassTypes(Type[] searchTypes)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(a => a.GetTypes())
                            .Where(t => searchTypes.Any(t.IsAssignableTo) && !t.IsAbstract)
                            .ToList();
        }
        
        public static string[] SplitGroupName(string group)
        {
            var array = group.Split(Span, StringSplitOptions.RemoveEmptyEntries);
            return array.Length > 0 ? array : new[] { group };
        }
        
        public static string GetFirstGroupNameOrDefault(MemberInfo memberInfo)
        {
            var groupAttribute = memberInfo.GetCustomAttribute<CeresGroupAttribute>();
            return groupAttribute == null ? null : GetFirstGroupNameOrDefault(groupAttribute.Group);
        }
        
        public static string GetFirstGroupNameOrDefault(string group)
        {
            return string.IsNullOrEmpty(group) ? null : SplitGroupName(group)[0];
        }
    }
}
