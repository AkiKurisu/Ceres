using System.Diagnostics.CodeAnalysis;
using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Chris.Serialization;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Executable function library for Unity built-in types
    /// </summary>
    [CeresGroup("Unity")]
    public partial class UnityExecutableLibrary: ExecutableFunctionLibrary
    {
        #region UObject
        
        /// <summary>
        /// Validate a GameObject, component or asset is alive.
        /// </summary>
        /// <param name="uObject"></param>
        /// <returns></returns>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("IsValid")]
        public static bool Flow_UObjectIsValid([NotNull] UObject uObject)
        {
            return uObject;
        }
        
        /// <summary>
        /// Removes a GameObject, component or asset.
        /// </summary>
        /// <param name="uObject"></param>
        [ExecutableFunction]
        public static void Flow_Destroy([NotNull] UObject uObject)
        {
            UObject.Destroy(uObject);
        }
        
        /// <summary>
        /// Returns the first active loaded object of Type type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        [ExecutableFunction]
        public static UObject Flow_FindObjectOfType(
            [ResolveReturn] SerializedType<UObject> type)
        {
            return UObject.FindObjectOfType(type);
        }
        
        #endregion UObject
        
        #region GameObject
        
        /// <summary>
        /// ActivatesDeactivates the GameObject, depending on the given true or false value.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="value"></param>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("SetActive")]
        public static void Flow_GameObjectSetActive(GameObject gameObject, bool value)
        {
            gameObject.SetActive(value);
        }
        
        /// <summary>
        /// Get the local active state of this GameObject.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetActiveSelf")]
        public static bool Flow_GameObjectGetActiveSelf(GameObject gameObject)
        {
            return gameObject.activeSelf;
        }
        
        /// <summary>
        /// Finds a GameObject by name and returns it.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [ExecutableFunction, CeresLabel("Find GameObject")]
        public static GameObject Flow_FindGameObject(string name)
        {
            return GameObject.Find(name);
        }
        
        /// <summary>
        /// Returns one active GameObject tag. Returns null if no GameObject was found.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        [ExecutableFunction, CeresLabel("Find GameObject WithTag")]
        public static GameObject Flow_FindGameObjectWithTag(string tag)
        {
            return GameObject.FindWithTag(tag);
        }
                
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetComponent")]
        public static Component Flow_GameObjectGetComponent(GameObject gameObject, 
            [ResolveReturn] SerializedType<Component> type)
        {
            return gameObject.GetComponent(type);
        }
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetComponentInChildren")]
        public static Component Flow_GameObjectGetComponentInChildren(GameObject gameObject, 
            [ResolveReturn] SerializedType<Component> type)
        {
            return gameObject.GetComponentInChildren(type);
        }
        
        #endregion GameObject

        #region Transform
        
        /// <summary>
        /// Returns a transform child by index.
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("GetChild")]
        public static Transform Flow_TransformGetChild(Transform transform, int index)
        {
            return transform.GetChild(index);
        }
        
        /// <summary>
        /// Finds a child by name n and returns it.
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Find")]
        public static Transform Flow_TransformFind(Transform transform, string name)
        {
            return transform.Find(name);
        }

        #endregion Transform

        #region Component
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetComponent")]
        public static Component Flow_ComponentGetComponent(Component component,
            [ResolveReturn] SerializedType<Component> type)
        {
            return component.GetComponent(type);
        }
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetComponentInChildren")]
        public static Component Flow_ComponentGetComponentInChildren(Component component, 
            [ResolveReturn] SerializedType<Component> type)
        {
            return component.GetComponentInChildren(type);
        }
        
        #endregion Component

        #region Random
        
        /// <summary>
        /// Returns a random float within [minInclusive..maxInclusive] (range is inclusive).
        /// </summary>
        /// <param name="minInclusive"></param>
        /// <param name="maxInclusive"></param>
        /// <returns></returns>
        [ExecutableFunction, CeresGroup("Unity/Random")]
        public static float Flow_RandomRange(float minInclusive, float maxInclusive)
        {
            return Random.Range(minInclusive, maxInclusive);
        }
        
        /// <summary>
        /// Return a random int within [minInclusive..maxExclusive).
        /// </summary>
        /// <param name="minInclusive"></param>
        /// <param name="maxInclusive"></param>
        /// <returns></returns>
        [ExecutableFunction, CeresGroup("Unity/Random")]
        public static int Flow_RandomRangeInt(int minInclusive, int maxInclusive)
        {
            return Random.Range(minInclusive, maxInclusive);
        }

        #endregion Random
    }
}