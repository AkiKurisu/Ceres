using System;
using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using R3;
using R3.Chris;
using UnityEngine;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Executable function library for R3
    /// </summary>
    [CeresGroup("Rx")]
    public partial class RxExecutableLibrary: ExecutableFunctionLibrary
    {
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Dispose")]
        public static void Flow_IDisposableDispose(IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Add to Disposable Unregister")]
        public static void Flow_IDisposableAddToIDisposableUnregister(IDisposable disposable, IDisposableUnregister disposableUnregister)
        {
            disposable.AddTo(disposableUnregister);
        }
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Add to Component")]
        public static void Flow_IDisposableAddToComponent(IDisposable disposable, Component component)
        {
            disposable.AddTo(component);
        }
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Add to GameObject")]
        public static void Flow_IDisposableAddToGameObject(IDisposable disposable, GameObject gameObject)
        {
            disposable.AddTo(gameObject);
        }
    }
}