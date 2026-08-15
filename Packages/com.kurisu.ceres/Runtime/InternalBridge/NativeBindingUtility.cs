using System.Reflection;
using UnityEngine.Bindings;

namespace UnityEngine
{
    public static class NativeBindingUtility
    {
        public static bool IsNativeMethod(MethodInfo methodInfo)
        {
            return methodInfo.GetCustomAttribute<NativeMethodAttribute>() != null;
        }
    }
}