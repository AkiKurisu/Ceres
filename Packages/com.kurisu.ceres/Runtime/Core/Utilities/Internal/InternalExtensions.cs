using System;
using System.Reflection;
using UnityEngine;

namespace Ceres
{
    internal static class InternalExtensions
    {
        public static MethodInfo GetStaticMethodWithNoParametersInBase(this Type type, string methodName)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            foreach (var method in methods)
            {
                if (method.Name == methodName && method.GetParameters().Length == 0)
                {
                    return method;
                }
            }

            return null;
        }
        
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (!component) component = gameObject.AddComponent<T>();
            return component;
        }
    }
}
