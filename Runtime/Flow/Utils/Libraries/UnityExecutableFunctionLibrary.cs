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
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("GetName")]
        public static string Flow_UObjectGetName(
            [CeresMetadata(CeresMetadata.SELF_TARGET)] UObject uObject)
        {
            return uObject.name;
        }
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("IsValid")]
        public static bool Flow_UObjectIsValid(
            [CeresMetadata(CeresMetadata.SELF_TARGET)] UObject uObject)
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
            [CeresMetadata(CeresMetadata.RESOVLE_RETURN)] SerializedType<UObject> type)
        {
            return UObject.FindObjectOfType(type);
        }
        
        #endregion UObject
        
        #region GameObject
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("GetLayer")]
        public static int Flow_GameObjectGetLayer(
            [CeresMetadata(CeresMetadata.SELF_TARGET)] GameObject gameObject)
        {
            return gameObject.layer;
        }
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("GetTag")]
        public static string Flow_GameObjectGetTag(
            [CeresMetadata(CeresMetadata.SELF_TARGET)] GameObject gameObject)
        {
            return gameObject.tag;
        }
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("SetActive")]
        public static void Flow_GameObjectSetActive(
            [CeresMetadata(CeresMetadata.SELF_TARGET)] GameObject gameObject, bool value)
        {
            gameObject.SetActive(value);
        }
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("GetActiveSelf")]
        public static bool Flow_GameObjectGetActiveSelf(
            [CeresMetadata(CeresMetadata.SELF_TARGET)] GameObject gameObject)
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
                
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("GetComponent")]
        public static Component Flow_GameObjectGetComponent(
            [CeresMetadata(CeresMetadata.SELF_TARGET)] GameObject gameObject, 
            [CeresMetadata(CeresMetadata.RESOVLE_RETURN)] SerializedType<Component> type)
        {
            return gameObject.GetComponent(type);
        }
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("GetComponentInChildren")]
        public static Component Flow_GameObjectGetComponentInChildren(
            [CeresMetadata(CeresMetadata.SELF_TARGET)] GameObject gameObject, 
            [CeresMetadata(CeresMetadata.RESOVLE_RETURN)] SerializedType<Component> type)
        {
            return gameObject.GetComponentInChildren(type);
        }
        
        #endregion GameObject

        #region Component
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("GetGameObject")]
        public static GameObject Flow_ComponentGetGameObject(
            [CeresMetadata(CeresMetadata.SELF_TARGET)] Component component)
        {
            return component.gameObject;
        }
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("GetComponent")]
        public static Component Flow_ComponentGetComponent(
            [CeresMetadata(CeresMetadata.SELF_TARGET)] Component component,
            [CeresMetadata(CeresMetadata.RESOVLE_RETURN)] SerializedType<Component> type)
        {
            return component.GetComponent(type);
        }
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("GetComponentInChildren")]
        public static Component Flow_ComponentGetComponentInChildren(
            [CeresMetadata(CeresMetadata.SELF_TARGET)] Component component, 
            [CeresMetadata(CeresMetadata.RESOVLE_RETURN)] SerializedType<Component> type)
        {
            return component.GetComponentInChildren(type);
        }
        
        #endregion Component
        
        #region Behaviour
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("SetEnabled")]
        public static void Flow_BehaviourSetEnabled(
            [CeresMetadata(CeresMetadata.SELF_TARGET)] Behaviour behaviour, bool enabled)
        {
            behaviour.enabled = enabled;
        }
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("GetEnabled")]
        public static bool Flow_BehaviourGetEnabled(
            [CeresMetadata(CeresMetadata.SELF_TARGET)] Behaviour behaviour)
        {
            return behaviour.enabled;
        }
        
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("GetIsActiveAndEnabled")]
        public static bool Flow_BehaviourGetIsActiveAndEnabled(
            [CeresMetadata(CeresMetadata.SELF_TARGET)] Behaviour behaviour)
        {
            return behaviour.isActiveAndEnabled;
        }
        
        #endregion Behaviour
    }
}