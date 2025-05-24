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
    /// Executable function library for Chris.Resource
    /// </summary>
    [CeresGroup("Resource")]
    public partial class ResourceExecutableLibrary: ExecutableFunctionLibrary
    {
        [ExecutableFunction, CeresLabel("Load Asset Synchronous")]
        public static UObject Flow_LoadAssetSynchronous(string address)
        {
            return ResourceSystem.LoadAssetAsync<UObject>(address).AddTo(EventSystem.Instance).WaitForCompletion();
        }
        
        [ExecutableFunction, CeresLabel("Load Asset Async")]
        public static void Flow_LoadAssetAsync(string address, EventDelegate<UObject> onComplete)
        {
            ResourceSystem.LoadAssetAsync<UObject>(address, onComplete).AddTo(EventSystem.Instance);
        }
        
        [ExecutableFunction, CeresLabel("Instantiate Synchronous")]
        public static GameObject Flow_InstantiateAsync(string address, Transform parent)
        {
           return ResourceSystem.InstantiateAsync(address, parent).AddTo(EventSystem.Instance).WaitForCompletion();
        }
        
        [ExecutableFunction, CeresLabel("Instantiate Async")]
        public static void Flow_InstantiateAsync(string address, Transform parent, EventDelegate<GameObject> onComplete)
        {
            ResourceSystem.InstantiateAsync(address, parent, onComplete).AddTo(EventSystem.Instance);
        }
    }
}