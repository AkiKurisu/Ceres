using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Derived from this class to add custom static functions
    /// </summary>
    /// <remarks>Must add partial modifier</remarks> 
    public abstract class ExecutableFunctionLibrary
    {
        /// <summary>
        /// Collect all static executable functions in this library
        /// </summary>
        protected virtual void CollectExecutableFunctions()
        {
            
        }

        protected ExecutableFunctionLibrary()
        {
            CollectExecutableFunctions();
        }
        
        /// <summary>
        /// Register static executable function pointer to the reflection system
        /// </summary>
        /// <param name="functionName"></param>
        /// <param name="parameterCount"></param>
        /// <param name="functionPtr"></param>
        /// <typeparam name="TLibrary"></typeparam>
        protected static unsafe void RegisterExecutableFunctionPtr<TLibrary>(string functionName, int parameterCount, void* functionPtr) 
            where TLibrary: ExecutableFunctionLibrary
        {
            ExecutableReflection<TLibrary>.RegisterStaticExecutableFunctionPtr(functionName, parameterCount, (IntPtr)functionPtr);
        }
        
        /// <summary>
        /// Register static executable function file info to the reflection system, only works in editor and development build
        /// </summary>
        /// <param name="functionName"></param>
        /// <param name="parameterCount"></param>
        /// <param name="filePath"></param>
        /// <param name="lineNumber"></param>
        /// <typeparam name="TLibrary"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void RegisterExecutableFunctionFileInfo<TLibrary>(string functionName, int parameterCount, string filePath, int lineNumber) 
            where TLibrary: ExecutableFunctionLibrary
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            /* In editor, we use mono cecil instead */
            if (Application.isEditor) return;
            ExecutableReflection<TLibrary>.RegisterStaticExecutableFunctionFileInfo(functionName, parameterCount, filePath, lineNumber);
#endif
        }
    }
}