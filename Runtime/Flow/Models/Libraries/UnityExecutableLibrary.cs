using System;
using System.Diagnostics.CodeAnalysis;
using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Chris.Serialization;
using UnityEngine;
using Random = UnityEngine.Random;
using UObject = UnityEngine.Object;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Provides UnityEngine object, GameObject, Transform, Component, time, random, physics, and layer helpers for Flow graphs.
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
            return (bool)uObject;
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
        /// Retrieves the first active loaded object of Type type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        [ExecutableFunction]
        public static UObject Flow_FindObjectOfType(
            [ResolveReturn] SerializedType<UObject> type)
        {
#if UNITY_6000_0_OR_NEWER
            return UObject.FindFirstObjectByType(type);
#else
            return UObject.FindObjectOfType(type);
#endif
        }
        
        #endregion UObject
        
        #region GameObject
        
        /// <summary>
        /// Activates or deactivates the GameObject, depending on the given true or false value.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="value"></param>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("SetActive")]
        public static void Flow_GameObjectSetActive(GameObject gameObject, bool value)
        {
            gameObject.SetActive(value);
        }
        
        /// <summary>
        /// Gets the local active state of this GameObject.
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
        /// Finds one active GameObject by tag. Returns null if no GameObject was found.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        [ExecutableFunction, CeresLabel("Find GameObject WithTag")]
        public static GameObject Flow_FindGameObjectWithTag(string tag)
        {
            return GameObject.FindWithTag(tag);
        }
                
        /// <summary>
        /// Retrieves a reference to a component of the specified type.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetComponent")]
        public static Component Flow_GameObjectGetComponent(GameObject gameObject, 
            [ResolveReturn] SerializedType<Component> type)
        {
            return gameObject.GetComponent(type);
        }
        
        /// <summary>
        /// Provides the GameObject Get Component In Children operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetComponentInChildren")]
        public static Component Flow_GameObjectGetComponentInChildren(GameObject gameObject, 
            [ResolveReturn] SerializedType<Component> type)
        {
            return gameObject.GetComponentInChildren(type);
        }
                
        /// <summary>
        /// Adds a component of the specified type to the GameObject.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("AddComponent")]
        public static Component Flow_GameObjectAddComponent(GameObject gameObject,
            [ResolveReturn] SerializedType<Component> type)
        {
            return gameObject.AddComponent(type);
        }
                        
        /// <summary>
        /// Provides the GameObject Get Or Add Component operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetOrAddComponent")]
        public static Component Flow_GameObjectGetOrAddComponent(GameObject gameObject,
            [ResolveReturn] SerializedType<Component> type)
        {
            Type t = type;
            if (gameObject.TryGetComponent(t, out Component component))
            {
                return component;
            }
            return gameObject.AddComponent(type);
        }

        /// <summary>
        /// Provides the GameObject Get Name operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/GameObject"), CeresLabel("Get Name")]
        public static string Flow_GameObjectGetName(GameObject gameObject)
        {
            return gameObject.name;
        }

        /// <summary>
        /// Provides the GameObject Set Name operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/GameObject"), CeresLabel("Set Name")]
        public static void Flow_GameObjectSetName(GameObject gameObject, string name)
        {
            gameObject.name = name;
        }

        /// <summary>
        /// Provides the GameObject Get Tag operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/GameObject"), CeresLabel("Get Tag")]
        public static string Flow_GameObjectGetTag(GameObject gameObject)
        {
            return gameObject.tag;
        }

        /// <summary>
        /// Provides the GameObject Set Tag operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/GameObject"), CeresLabel("Set Tag")]
        public static void Flow_GameObjectSetTag(GameObject gameObject, string tag)
        {
            gameObject.tag = tag;
        }

        /// <summary>
        /// Provides the GameObject Compare Tag operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/GameObject"), CeresLabel("Compare Tag")]
        public static bool Flow_GameObjectCompareTag(GameObject gameObject, string tag)
        {
            return gameObject.CompareTag(tag);
        }

        /// <summary>
        /// Provides the GameObject Get Layer operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/GameObject"), CeresLabel("Get Layer")]
        public static int Flow_GameObjectGetLayer(GameObject gameObject)
        {
            return gameObject.layer;
        }

        /// <summary>
        /// Provides the GameObject Set Layer operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/GameObject"), CeresLabel("Set Layer")]
        public static void Flow_GameObjectSetLayer(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
        }

        /// <summary>
        /// Provides the GameObject Get Active In Hierarchy operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/GameObject"), CeresLabel("Get Active In Hierarchy")]
        public static bool Flow_GameObjectGetActiveInHierarchy(GameObject gameObject)
        {
            return gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Provides the Create GameObject operation for Flow graphs.
        /// </summary>
        [ExecutableFunction, CeresGroup("Unity/GameObject"), CeresLabel("Create GameObject")]
        public static GameObject Flow_CreateGameObject(string name)
        {
            return new GameObject(name);
        }

        /// <summary>
        /// Provides the Create Primitive operation for Flow graphs.
        /// </summary>
        [ExecutableFunction, CeresGroup("Unity/GameObject"), CeresLabel("Create Primitive")]
        public static GameObject Flow_CreatePrimitive(PrimitiveType primitiveType)
        {
            return GameObject.CreatePrimitive(primitiveType);
        }

        /// <summary>
        /// Provides the Instantiate GameObject operation for Flow graphs.
        /// </summary>
        [ExecutableFunction, CeresGroup("Unity/GameObject"), CeresLabel("Instantiate GameObject")]
        public static GameObject Flow_InstantiateGameObject(GameObject original)
        {
            return UObject.Instantiate(original);
        }

        /// <summary>
        /// Provides the Instantiate GameObject At Transform operation for Flow graphs.
        /// </summary>
        [ExecutableFunction, CeresGroup("Unity/GameObject"), CeresLabel("Instantiate GameObject at Transform")]
        public static GameObject Flow_InstantiateGameObjectAtTransform(GameObject original, Transform parent)
        {
            return UObject.Instantiate(original, parent);
        }

        /// <summary>
        /// Provides the Instantiate GameObject At Position operation for Flow graphs.
        /// </summary>
        [ExecutableFunction, CeresGroup("Unity/GameObject"), CeresLabel("Instantiate GameObject at Position")]
        public static GameObject Flow_InstantiateGameObjectAtPosition(GameObject original, Vector3 position, Quaternion rotation)
        {
            return UObject.Instantiate(original, position, rotation);
        }

        /// <summary>
        /// Provides the Destroy GameObject operation for Flow graphs.
        /// </summary>
        [ExecutableFunction, CeresGroup("Unity/GameObject"), CeresLabel("Destroy GameObject")]
        public static void Flow_DestroyGameObject(GameObject gameObject, float delay)
        {
            UObject.Destroy(gameObject, delay);
        }

        /// <summary>
        /// Provides the Dont Destroy On Load operation for Flow graphs.
        /// </summary>
        [ExecutableFunction, CeresGroup("Unity/GameObject"), CeresLabel("Dont Destroy On Load")]
        public static void Flow_DontDestroyOnLoad(GameObject gameObject)
        {
            UObject.DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Provides the GameObject Get Component In Parent operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/GameObject"), CeresLabel("Get Component In Parent")]
        public static Component Flow_GameObjectGetComponentInParent(GameObject gameObject,
            [ResolveReturn] SerializedType<Component> type)
        {
            return gameObject.GetComponentInParent(type);
        }

        /// <summary>
        /// Provides the GameObject Get Components operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/GameObject"), CeresLabel("Get Components")]
        public static Component[] Flow_GameObjectGetComponents(GameObject gameObject,
            [ResolveReturn] SerializedType<Component> type)
        {
            return gameObject.GetComponents(type);
        }

        /// <summary>
        /// Provides the GameObject Get Components In Children operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/GameObject"), CeresLabel("Get Components In Children")]
        public static Component[] Flow_GameObjectGetComponentsInChildren(GameObject gameObject,
            [ResolveReturn] SerializedType<Component> type)
        {
            return gameObject.GetComponentsInChildren(type);
        }

        /// <summary>
        /// Provides the GameObject Get Components In Parent operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/GameObject"), CeresLabel("Get Components In Parent")]
        public static Component[] Flow_GameObjectGetComponentsInParent(GameObject gameObject,
            [ResolveReturn] SerializedType<Component> type)
        {
            return gameObject.GetComponentsInParent(type);
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

        /// <summary>
        /// Provides the Transform Get Position operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Get Position")]
        public static Vector3 Flow_TransformGetPosition(Transform transform)
        {
            return transform.position;
        }

        /// <summary>
        /// Provides the Transform Set Position operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Transform"), CeresLabel("Set Position")]
        public static void Flow_TransformSetPosition(Transform transform, Vector3 position)
        {
            transform.position = position;
        }

        /// <summary>
        /// Provides the Transform Get Local Position operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Get Local Position")]
        public static Vector3 Flow_TransformGetLocalPosition(Transform transform)
        {
            return transform.localPosition;
        }

        /// <summary>
        /// Provides the Transform Set Local Position operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Transform"), CeresLabel("Set Local Position")]
        public static void Flow_TransformSetLocalPosition(Transform transform, Vector3 position)
        {
            transform.localPosition = position;
        }

        /// <summary>
        /// Provides the Transform Get Rotation operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Get Rotation")]
        public static Quaternion Flow_TransformGetRotation(Transform transform)
        {
            return transform.rotation;
        }

        /// <summary>
        /// Provides the Transform Set Rotation operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Transform"), CeresLabel("Set Rotation")]
        public static void Flow_TransformSetRotation(Transform transform, Quaternion rotation)
        {
            transform.rotation = rotation;
        }

        /// <summary>
        /// Provides the Transform Get Local Rotation operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Get Local Rotation")]
        public static Quaternion Flow_TransformGetLocalRotation(Transform transform)
        {
            return transform.localRotation;
        }

        /// <summary>
        /// Provides the Transform Set Local Rotation operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Transform"), CeresLabel("Set Local Rotation")]
        public static void Flow_TransformSetLocalRotation(Transform transform, Quaternion rotation)
        {
            transform.localRotation = rotation;
        }

        /// <summary>
        /// Provides the Transform Get Euler Angles operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Get Euler Angles")]
        public static Vector3 Flow_TransformGetEulerAngles(Transform transform)
        {
            return transform.eulerAngles;
        }

        /// <summary>
        /// Provides the Transform Set Euler Angles operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Transform"), CeresLabel("Set Euler Angles")]
        public static void Flow_TransformSetEulerAngles(Transform transform, Vector3 eulerAngles)
        {
            transform.eulerAngles = eulerAngles;
        }

        /// <summary>
        /// Provides the Transform Get Local Euler Angles operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Get Local Euler Angles")]
        public static Vector3 Flow_TransformGetLocalEulerAngles(Transform transform)
        {
            return transform.localEulerAngles;
        }

        /// <summary>
        /// Provides the Transform Set Local Euler Angles operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Transform"), CeresLabel("Set Local Euler Angles")]
        public static void Flow_TransformSetLocalEulerAngles(Transform transform, Vector3 eulerAngles)
        {
            transform.localEulerAngles = eulerAngles;
        }

        /// <summary>
        /// Provides the Transform Get Local Scale operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Get Local Scale")]
        public static Vector3 Flow_TransformGetLocalScale(Transform transform)
        {
            return transform.localScale;
        }

        /// <summary>
        /// Provides the Transform Set Local Scale operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Transform"), CeresLabel("Set Local Scale")]
        public static void Flow_TransformSetLocalScale(Transform transform, Vector3 scale)
        {
            transform.localScale = scale;
        }

        /// <summary>
        /// Provides the Transform Get Forward operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Get Forward")]
        public static Vector3 Flow_TransformGetForward(Transform transform)
        {
            return transform.forward;
        }

        /// <summary>
        /// Provides the Transform Get Right operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Get Right")]
        public static Vector3 Flow_TransformGetRight(Transform transform)
        {
            return transform.right;
        }

        /// <summary>
        /// Provides the Transform Get Up operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Get Up")]
        public static Vector3 Flow_TransformGetUp(Transform transform)
        {
            return transform.up;
        }

        /// <summary>
        /// Provides the Transform Get Parent operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Get Parent")]
        public static Transform Flow_TransformGetParent(Transform transform)
        {
            return transform.parent;
        }

        /// <summary>
        /// Provides the Transform Set Parent operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Transform"), CeresLabel("Set Parent")]
        public static void Flow_TransformSetParent(Transform transform, Transform parent, bool worldPositionStays)
        {
            transform.SetParent(parent, worldPositionStays);
        }

        /// <summary>
        /// Provides the Transform Get Child Count operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Get Child Count")]
        public static int Flow_TransformGetChildCount(Transform transform)
        {
            return transform.childCount;
        }

        /// <summary>
        /// Provides the Transform Translate operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Transform"), CeresLabel("Translate")]
        public static void Flow_TransformTranslate(Transform transform, Vector3 translation, Space relativeTo)
        {
            transform.Translate(translation, relativeTo);
        }

        /// <summary>
        /// Provides the Transform Rotate operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Transform"), CeresLabel("Rotate")]
        public static void Flow_TransformRotate(Transform transform, Vector3 eulers, Space relativeTo)
        {
            transform.Rotate(eulers, relativeTo);
        }

        /// <summary>
        /// Provides the Transform Look At operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Transform"), CeresLabel("Look At")]
        public static void Flow_TransformLookAt(Transform transform, Transform target)
        {
            transform.LookAt(target);
        }

        /// <summary>
        /// Provides the Transform Point operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Transform Point")]
        public static Vector3 Flow_TransformPoint(Transform transform, Vector3 position)
        {
            return transform.TransformPoint(position);
        }

        /// <summary>
        /// Provides the Inverse Transform Point operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Inverse Transform Point")]
        public static Vector3 Flow_InverseTransformPoint(Transform transform, Vector3 position)
        {
            return transform.InverseTransformPoint(position);
        }

        /// <summary>
        /// Provides the Transform Direction operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Transform Direction")]
        public static Vector3 Flow_TransformDirection(Transform transform, Vector3 direction)
        {
            return transform.TransformDirection(direction);
        }

        /// <summary>
        /// Provides the Inverse Transform Direction operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Transform"), CeresLabel("Inverse Transform Direction")]
        public static Vector3 Flow_InverseTransformDirection(Transform transform, Vector3 direction)
        {
            return transform.InverseTransformDirection(direction);
        }

        #endregion Transform

        #region Component
        
        /// <summary>
        /// Provides the Component Get Component operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetComponent")]
        public static Component Flow_ComponentGetComponent(Component component,
            [ResolveReturn] SerializedType<Component> type)
        {
            return component.GetComponent(type);
        }
        
        /// <summary>
        /// Provides the Component Get Component In Children operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true), CeresLabel("GetComponentInChildren")]
        public static Component Flow_ComponentGetComponentInChildren(Component component, 
            [ResolveReturn] SerializedType<Component> type)
        {
            return component.GetComponentInChildren(type);
        }

        /// <summary>
        /// Provides the Component Get GameObject operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Component"), CeresLabel("Get GameObject")]
        public static GameObject Flow_ComponentGetGameObject(Component component)
        {
            return component.gameObject;
        }

        /// <summary>
        /// Provides the Component Get Transform operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true),
         CeresGroup("Unity/Component"), CeresLabel("Get Transform")]
        public static Transform Flow_ComponentGetTransform(Component component)
        {
            return component.transform;
        }

        /// <summary>
        /// Provides the Component Get Component In Parent operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Component"), CeresLabel("Get Component In Parent")]
        public static Component Flow_ComponentGetComponentInParent(Component component,
            [ResolveReturn] SerializedType<Component> type)
        {
            return component.GetComponentInParent(type);
        }

        /// <summary>
        /// Provides the Component Get Components operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Component"), CeresLabel("Get Components")]
        public static Component[] Flow_ComponentGetComponents(Component component,
            [ResolveReturn] SerializedType<Component> type)
        {
            return component.GetComponents(type);
        }

        /// <summary>
        /// Provides the Component Get Components In Children operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Component"), CeresLabel("Get Components In Children")]
        public static Component[] Flow_ComponentGetComponentsInChildren(Component component,
            [ResolveReturn] SerializedType<Component> type)
        {
            return component.GetComponentsInChildren(type);
        }

        /// <summary>
        /// Provides the Component Get Components In Parent operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true),
         CeresGroup("Unity/Component"), CeresLabel("Get Components In Parent")]
        public static Component[] Flow_ComponentGetComponentsInParent(Component component,
            [ResolveReturn] SerializedType<Component> type)
        {
            return component.GetComponentsInParent(type);
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

        #region Time

        /// <summary>
        /// Provides the Time Get Time operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Time"), CeresLabel("Get Time")]
        public static float Flow_TimeGetTime()
        {
            return Time.time;
        }

        /// <summary>
        /// Provides the Time Get Unscaled Time operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Time"), CeresLabel("Get Unscaled Time")]
        public static float Flow_TimeGetUnscaledTime()
        {
            return Time.unscaledTime;
        }

        /// <summary>
        /// Provides the Time Get Delta Time operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Time"), CeresLabel("Get Delta Time")]
        public static float Flow_TimeGetDeltaTime()
        {
            return Time.deltaTime;
        }

        /// <summary>
        /// Provides the Time Get Fixed Delta Time operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Time"), CeresLabel("Get Fixed Delta Time")]
        public static float Flow_TimeGetFixedDeltaTime()
        {
            return Time.fixedDeltaTime;
        }

        /// <summary>
        /// Provides the Time Get Frame Count operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Time"), CeresLabel("Get Frame Count")]
        public static int Flow_TimeGetFrameCount()
        {
            return Time.frameCount;
        }

        /// <summary>
        /// Provides the Time Get Realtime Since Startup operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Time"), CeresLabel("Get Realtime Since Startup")]
        public static float Flow_TimeGetRealtimeSinceStartup()
        {
            return Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Provides the Time Get Time Scale operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Time"), CeresLabel("Get Time Scale")]
        public static float Flow_TimeGetTimeScale()
        {
            return Time.timeScale;
        }

        /// <summary>
        /// Provides the Time Set Time Scale operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true), CeresGroup("Unity/Time"), CeresLabel("Set Time Scale")]
        public static void Flow_TimeSetTimeScale(float timeScale)
        {
            Time.timeScale = timeScale;
        }

        #endregion Time

        #region Physics

        /// <summary>
        /// Provides the Physics Raycast operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Physics"), CeresLabel("Raycast")]
        public static bool Flow_PhysicsRaycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask,
            QueryTriggerInteraction queryTriggerInteraction)
        {
            return Physics.Raycast(origin, direction, maxDistance, layerMask, queryTriggerInteraction);
        }

        /// <summary>
        /// Provides the Physics Raycast All operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Physics"), CeresLabel("Raycast All")]
        public static RaycastHit[] Flow_PhysicsRaycastAll(Vector3 origin, Vector3 direction, float maxDistance, int layerMask,
            QueryTriggerInteraction queryTriggerInteraction)
        {
            return Physics.RaycastAll(origin, direction, maxDistance, layerMask, queryTriggerInteraction);
        }

        /// <summary>
        /// Provides the Physics Linecast operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Physics"), CeresLabel("Linecast")]
        public static bool Flow_PhysicsLinecast(Vector3 start, Vector3 end, int layerMask,
            QueryTriggerInteraction queryTriggerInteraction)
        {
            return Physics.Linecast(start, end, layerMask, queryTriggerInteraction);
        }

        /// <summary>
        /// Provides the Physics Overlap Sphere operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Physics"), CeresLabel("Overlap Sphere")]
        public static Collider[] Flow_PhysicsOverlapSphere(Vector3 position, float radius, int layerMask,
            QueryTriggerInteraction queryTriggerInteraction)
        {
            return Physics.OverlapSphere(position, radius, layerMask, queryTriggerInteraction);
        }

        /// <summary>
        /// Provides the Physics Check Sphere operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Physics"), CeresLabel("Check Sphere")]
        public static bool Flow_PhysicsCheckSphere(Vector3 position, float radius, int layerMask,
            QueryTriggerInteraction queryTriggerInteraction)
        {
            return Physics.CheckSphere(position, radius, layerMask, queryTriggerInteraction);
        }

        /// <summary>
        /// Provides the Physics Sphere Cast All operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Physics"), CeresLabel("Sphere Cast All")]
        public static RaycastHit[] Flow_PhysicsSphereCastAll(Vector3 origin, float radius, Vector3 direction,
            float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            return Physics.SphereCastAll(origin, radius, direction, maxDistance, layerMask, queryTriggerInteraction);
        }

        /// <summary>
        /// Provides the Physics Default Raycast Layers operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Physics"), CeresLabel("Default Raycast Layers")]
        public static int Flow_PhysicsDefaultRaycastLayers()
        {
            return Physics.DefaultRaycastLayers;
        }

        /// <summary>
        /// Provides the Layer Mask All Layers operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Physics"), CeresLabel("All Layers")]
        public static int Flow_LayerMaskAllLayers()
        {
            return Physics.AllLayers;
        }

        /// <summary>
        /// Provides the Layer Mask Name To Layer operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Physics"), CeresLabel("Name To Layer")]
        public static int Flow_LayerMaskNameToLayer(string layerName)
        {
            return LayerMask.NameToLayer(layerName);
        }

        /// <summary>
        /// Provides the Layer Mask Get Mask operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Physics"), CeresLabel("Get Mask")]
        public static int Flow_LayerMaskGetMask(string layerName)
        {
            return LayerMask.GetMask(layerName);
        }

        /// <summary>
        /// Provides the Layer Mask Get Mask2 operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Physics"), CeresLabel("Get Mask 2")]
        public static int Flow_LayerMaskGetMask2(string layerName1, string layerName2)
        {
            return LayerMask.GetMask(layerName1, layerName2);
        }

        /// <summary>
        /// Provides the Layer Mask Get Mask3 operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(ExecuteInDependency = true), CeresGroup("Unity/Physics"), CeresLabel("Get Mask 3")]
        public static int Flow_LayerMaskGetMask3(string layerName1, string layerName2, string layerName3)
        {
            return LayerMask.GetMask(layerName1, layerName2, layerName3);
        }

        #endregion Physics
    }
}
