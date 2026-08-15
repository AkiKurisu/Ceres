using System;
using System.Diagnostics;
using System.Reflection;

namespace Ceres
{
    internal static class DiagnosticsUtility
    {
        public static StackFrame GetCurrentStackFrame()
        {
            StackTrace stackTrace = new(1, true);
            int frameId = 0;

            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                MethodBase method = stackTrace.GetFrame(i).GetMethod();
                if (method.GetCustomAttribute<StackTraceFrameAttribute>() != null)
                {
                    frameId = i;
                }
            }

            if (frameId < stackTrace.FrameCount - 1) ++frameId;
            return stackTrace.GetFrame(frameId);
        }
        
        public static string GetDelegatePath(Delegate callback)
        {
            var declType = callback.Method.DeclaringType?.Name ?? string.Empty;
            string itemName = $"{declType}.{callback.Method.Name}";
            if (callback.Target != null)
            {
                string objectName = callback.Target.ToString();
                itemName = $"{itemName}>[{objectName}]";
            };
            return itemName;
        }
    }
}