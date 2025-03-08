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
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("IsValid")]
        public static bool Flow_UObjectIsValid([NotNull] UObject uObject)
        {
            return uObject;
        }
        
        [ExecutableFunction]
        public static void Flow_Destroy([NotNull] UObject uObject)
        {
            UObject.Destroy(uObject);
        }
        
        [ExecutableFunction]
        public static UObject Flow_FindObjectOfType(
            [ResolveReturn] SerializedType<UObject> type)
        {
            return UObject.FindObjectOfType(type);
        }
        
        #endregion UObject
        
        #region GameObject
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("SetActive")]
        public static void Flow_GameObjectSetActive(GameObject gameObject, bool value)
        {
            gameObject.SetActive(value);
        }
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetActiveSelf")]
        public static bool Flow_GameObjectGetActiveSelf(GameObject gameObject)
        {
            return gameObject.activeSelf;
        }
        
        [ExecutableFunction, CeresLabel("Find GameObject")]
        public static GameObject Flow_FindGameObject(string name)
        {
            return GameObject.Find(name);
        }
        
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
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("GetChild")]
        public static Transform Flow_TransformGetChild(Transform transform, int index)
        {
            return transform.GetChild(index);
        }
        
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
        
        #region Behaviour
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetIsActiveAndEnabled")]
        public static bool Flow_BehaviourGetIsActiveAndEnabled(Behaviour behaviour)
        {
            return behaviour.isActiveAndEnabled;
        }
        
        #endregion Behaviour

        #region Random
        
        [ExecutableFunction, CeresGroup("Unity/Random")]
        public static float Flow_RandomRange(float minInclusive, float maxInclusive)
        {
            return Random.Range(minInclusive, maxInclusive);
        }
        
        [ExecutableFunction, CeresGroup("Unity/Random")]
        public static int Flow_RandomRangeInt(int minInclusive, int maxInclusive)
        {
            return Random.Range(minInclusive, maxInclusive);
        }

        #endregion Random
    }
}