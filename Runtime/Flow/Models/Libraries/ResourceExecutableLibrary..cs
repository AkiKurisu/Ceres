using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Chris.Events;
using Chris.Resource;
using UnityEngine;
using UObject = UnityEngine.Object;
using R3;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Provides Chris.Resource loading and instantiation helpers for Flow graphs.
    /// </summary>
    [CeresGroup("Resource")]
    public partial class ResourceExecutableLibrary: ExecutableFunctionLibrary
    {
        /// <summary>
        /// Loads an asset by address and waits synchronously for completion.
        /// </summary>
        /// <param name="address">The resource address to load.</param>
        /// <returns>The loaded Unity object.</returns>
        [ExecutableFunction, CeresLabel("Load Asset Synchronous")]
        public static UObject Flow_LoadAssetSynchronous(string address)
        {
            return ResourceSystem.LoadAssetAsync<UObject>(address).AddTo(EventSystem.Instance).WaitForCompletion();
        }
        
        /// <summary>
        /// Loads an asset by address asynchronously and invokes a callback when it completes.
        /// </summary>
        /// <param name="address">The resource address to load.</param>
        /// <param name="onComplete">Callback invoked with the loaded object.</param>
        [ExecutableFunction, CeresLabel("Load Asset Async")]
        public static void Flow_LoadAssetAsync(string address, EventDelegate<UObject> onComplete)
        {
            ResourceSystem.LoadAssetAsync<UObject>(address, onComplete).AddTo(EventSystem.Instance);
        }
        
        /// <summary>
        /// Instantiates a resource by address under the specified parent and waits synchronously for completion.
        /// </summary>
        /// <param name="address">The resource address to instantiate.</param>
        /// <param name="parent">The parent transform for the created object.</param>
        /// <returns>The instantiated GameObject.</returns>
        [ExecutableFunction, CeresLabel("Instantiate Synchronous")]
        public static GameObject Flow_InstantiateAsync(string address, Transform parent)
        {
           return ResourceSystem.InstantiateAsync(address, parent).AddTo(EventSystem.Instance).WaitForCompletion();
        }
        
        /// <summary>
        /// Instantiates a resource by address under the specified parent asynchronously.
        /// </summary>
        /// <param name="address">The resource address to instantiate.</param>
        /// <param name="parent">The parent transform for the created object.</param>
        /// <param name="onComplete">Callback invoked with the instantiated GameObject.</param>
        [ExecutableFunction, CeresLabel("Instantiate Async")]
        public static void Flow_InstantiateAsync(string address, Transform parent, EventDelegate<GameObject> onComplete)
        {
            ResourceSystem.InstantiateAsync(address, parent, onComplete).AddTo(EventSystem.Instance);
        }
    }
}
