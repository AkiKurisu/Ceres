using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using R3;
using UnityEngine;

namespace Ceres.Serialization.Editor
{
    [Serializable]
    public class SerializedObjectWrapper<T> : SerializedObjectWrapper
    {
        [SerializeField]
        private T m_Value;

        public override object Value
        {
            get => m_Value;
            set => m_Value = (T)value;
        }

        public readonly Subject<T> ValueChange = new();

        private void OnValidate()
        {
            // Hook IMGUI view model change
            ValueChange.OnNext(m_Value);
        }
    }

    /// <summary>
    /// Class to manage SerializedObjectWrapper
    /// </summary>
    public static class SerializedObjectWrapperManager
    {
        // Cache for MakeGenericType results
        private static readonly ConcurrentDictionary<Type, Type> s_GenericTypeCache = new();
        
        // Cache for GetField results
        private static readonly ConcurrentDictionary<Type, FieldInfo> s_FieldInfoCache = new();
        
        // Cache for GetCustomAttributes results
        private static readonly ConcurrentDictionary<FieldInfo, Attribute[]> s_AttributesCache = new();
        
        /// <summary>
        /// Create an editor wrapper for providing <see cref="Type"/> and track it by <see cref="SoftObjectHandle"/>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="softObjectHandle"></param>
        /// <returns></returns>
        public static SerializedObjectWrapper CreateWrapper(Type type, ref SoftObjectHandle softObjectHandle)
        {
            if (type == null) return null;

            var wrapper = softObjectHandle.GetObject() as SerializedObjectWrapper;
            // Validate wrapped type
            if (!wrapper || wrapper.Value.GetType() != type || wrapper.FieldInfo != null)
            {
                // Delay CreateDefaultValue call until we actually need to create wrapper
                var defaultValue = ReflectionUtility.CreateDefaultValue(type);
                wrapper = Wrap(type, defaultValue);
                GlobalObjectManager.UnregisterObject(softObjectHandle);
                GlobalObjectManager.RegisterObject(wrapper, ref softObjectHandle);
            }
            return wrapper;
        }

        /// <summary>
        /// Create an editor wrapper for providing <see cref="FieldInfo"/> and track it by <see cref="SoftObjectHandle"/>
        /// </summary>
        /// <param name="fieldInfo"></param>
        /// <param name="softObjectHandle"></param>
        /// <returns></returns>
        public static SerializedObjectWrapper CreateFieldWrapper(FieldInfo fieldInfo, ref SoftObjectHandle softObjectHandle)
        {
            if (fieldInfo == null) return null;

            var wrapper = softObjectHandle.GetObject() as SerializedObjectWrapper;
            // Validate wrapped type
            if (!wrapper || wrapper.Value.GetType() != fieldInfo.FieldType || wrapper.FieldInfo != fieldInfo)
            {
                // Delay CreateDefaultValue call until we actually need to create wrapper
                var defaultValue = ReflectionUtility.CreateDefaultValue(fieldInfo.FieldType);
                wrapper = Wrap(fieldInfo.FieldType, defaultValue, fieldInfo);
                GlobalObjectManager.UnregisterObject(softObjectHandle);
                GlobalObjectManager.RegisterObject(wrapper, ref softObjectHandle);
            }
            return wrapper;
        }

        /// <summary>
        /// Manually destroy wrapper
        /// </summary>
        /// <param name="softObjectHandle"></param>
        public static void DestroyWrapper(SoftObjectHandle softObjectHandle)
        {
            GlobalObjectManager.UnregisterObject(softObjectHandle);
        }

        /// <summary>
        /// Get editor wrapper if exists
        /// </summary>
        /// <param name="type"></param>
        /// <param name="softObjectHandle"></param>
        /// <returns></returns>
        public static SerializedObjectWrapper GetWrapper(Type type, SoftObjectHandle softObjectHandle)
        {
            var wrapper = softObjectHandle.GetObject() as SerializedObjectWrapper;
            // Validate wrapped type
            if (wrapper && wrapper.Value.GetType() != type)
            {
                GlobalObjectManager.UnregisterObject(softObjectHandle);
                return null;
            }
            return wrapper;
        }

        private static SerializedObjectWrapper Wrap(Type valueType, object value = null, FieldInfo fieldInfo = null)
        {
            // Cache MakeGenericType result
            var genericType = s_GenericTypeCache.GetOrAdd(valueType, t => typeof(SerializedObjectWrapper<>).MakeGenericType(t));
            var dynamicType = DynamicTypeBuilder.MakeDerivedType(genericType, valueType);
            if (fieldInfo != null)
            {
                // Cache GetField result
                var valueFieldInfo = s_FieldInfoCache.GetOrAdd(genericType, gt => gt.GetField("m_Value", BindingFlags.NonPublic | BindingFlags.Instance));
                if (valueFieldInfo != null)
                {
                    // Cache GetCustomAttributes result
                    var attributes = s_AttributesCache.GetOrAdd(fieldInfo, fi =>
                    {
                        var attrs = fi.GetCustomAttributes().ToList();
                        attrs.RemoveAll(x => x is SerializeField);
                        return attrs.ToArray();
                    });
                    TypeDescriptor.AddAttributes(valueFieldInfo, attributes);
                }
            }
            var dynamicTypeInstance = ScriptableObject.CreateInstance(dynamicType);
            if (dynamicTypeInstance is not SerializedObjectWrapper wrapper)
            {
                return null;
            }
            if (value != null)
            {
                wrapper.Value = value;
            }
            return wrapper;
        }
    }
}
