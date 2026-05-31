using System;
using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using R3;
using R3.Chris;
using UnityEngine;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Provides R3 subscription and disposable helpers for Flow graphs.
    /// </summary>
    [CeresGroup("Rx")]
    public partial class RxExecutableLibrary: ExecutableFunctionLibrary
    {
        /// <summary>
        /// Disposes the target subscription or disposable object.
        /// </summary>
        /// <param name="disposable">The disposable object to dispose.</param>
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Dispose")]
        public static void Flow_IDisposableDispose(IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        /// <summary>
        /// Registers the disposable with an IDisposableUnregister lifetime owner.
        /// </summary>
        /// <param name="disposable">The disposable to register.</param>
        /// <param name="disposableUnregister">The unregister owner that will dispose it.</param>
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Add To Disposable Unregister")]
        public static void Flow_IDisposableAddToIDisposableUnregister(IDisposable disposable, IDisposableUnregister disposableUnregister)
        {
            disposable.AddTo(disposableUnregister);
        }
        
        /// <summary>
        /// Registers the disposable with a component lifetime.
        /// </summary>
        /// <param name="disposable">The disposable to register.</param>
        /// <param name="component">The component whose lifetime owns the disposable.</param>
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Add To Component")]
        public static void Flow_IDisposableAddToComponent(IDisposable disposable, Component component)
        {
            disposable.AddTo(component);
        }
        
        /// <summary>
        /// Registers the disposable with a GameObject lifetime.
        /// </summary>
        /// <param name="disposable">The disposable to register.</param>
        /// <param name="gameObject">The GameObject whose lifetime owns the disposable.</param>
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Add To GameObject")]
        public static void Flow_IDisposableAddToGameObject(IDisposable disposable, GameObject gameObject)
        {
            disposable.AddTo(gameObject);
        }
        
        /// <summary>
        /// Creates a disposable that invokes the supplied event when disposed.
        /// </summary>
        /// <param name="onDispose">Callback invoked when the disposable is disposed.</param>
        /// <returns>A disposable callback wrapper.</returns>
        [ExecutableFunction, CeresLabel("Create Disposable From Event")]
        public static IDisposable Flow_DisposableCreate(EventDelegate onDispose)
        {
            return Disposable.Create(onDispose);
        }
    }
}
