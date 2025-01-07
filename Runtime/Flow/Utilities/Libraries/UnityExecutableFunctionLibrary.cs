using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Chris.Serialization;
using UnityEngine;
using UnityEngine.Scripting;
using UObject = UnityEngine.Object;
namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Executable function library for Unity built-in types
    /// </summary>
    [Preserve]
    public class UnityExecutableFunctionLibrary: ExecutableFunctionLibrary
    {
        #region UObject
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetName")]
        public static string Flow_UObjectGetName(UObject uObject)
        {
            return uObject.name;
        }
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("IsValid")]
        public static bool Flow_UObjectIsValid(UObject uObject)
        {
            return uObject;
        }
        
        [ExecutableFunction]
        public static void Flow_Destroy(UObject uObject)
        {
            UObject.Destroy(uObject);
        }
        
        [ExecutableFunction]
        public static UObject Flow_FindObjectOfType(
            [CeresMetadata(ExecutableFunction.RESOLVE_RETURN)] SerializedType<UObject> type)
        {
            return UObject.FindObjectOfType(type);
        }
        
        #endregion UObject
        
        #region GameObject
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetLayer")]
        public static int Flow_GameObjectGetLayer(GameObject gameObject)
        {
            return gameObject.layer;
        }
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetTag")]
        public static string Flow_GameObjectGetTag(GameObject gameObject)
        {
            return gameObject.tag;
        }
        
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
            [CeresMetadata(ExecutableFunction.RESOLVE_RETURN)] SerializedType<Component> type)
        {
            return gameObject.GetComponent(type);
        }
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetComponentInChildren")]
        public static Component Flow_GameObjectGetComponentInChildren(GameObject gameObject, 
            [CeresMetadata(ExecutableFunction.RESOLVE_RETURN)] SerializedType<Component> type)
        {
            return gameObject.GetComponentInChildren(type);
        }
        
        #endregion GameObject

        #region Component
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetGameObject")]
        public static GameObject Flow_ComponentGetGameObject(Component component)
        {
            return component.gameObject;
        }
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetComponent")]
        public static Component Flow_ComponentGetComponent(Component component,
            [CeresMetadata(ExecutableFunction.RESOLVE_RETURN)] SerializedType<Component> type)
        {
            return component.GetComponent(type);
        }
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetComponentInChildren")]
        public static Component Flow_ComponentGetComponentInChildren(Component component, 
            [CeresMetadata(ExecutableFunction.RESOLVE_RETURN)] SerializedType<Component> type)
        {
            return component.GetComponentInChildren(type);
        }
        
        #endregion Component
        
        #region Behaviour
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("SetEnabled")]
        public static void Flow_BehaviourSetEnabled(Behaviour behaviour, bool enabled)
        {
            behaviour.enabled = enabled;
        }
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetEnabled")]
        public static bool Flow_BehaviourGetEnabled(Behaviour behaviour)
        {
            return behaviour.enabled;
        }
        
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetIsActiveAndEnabled")]
        public static bool Flow_BehaviourGetIsActiveAndEnabled(Behaviour behaviour)
        {
            return behaviour.isActiveAndEnabled;
        }
        
        #endregion Behaviour

        #region Random
        
        [ExecutableFunction]
        public static float Flow_RandomRange(float minInclusive, float maxInclusive)
        {
            return Random.Range(minInclusive, maxInclusive);
        }
        
        [ExecutableFunction]
        public static int Flow_RandomRangeInt(int minInclusive, int maxInclusive)
        {
            return Random.Range(minInclusive, maxInclusive);
        }

        #endregion Random
    }
}