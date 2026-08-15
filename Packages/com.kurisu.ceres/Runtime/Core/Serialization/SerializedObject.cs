using System;
using UnityEngine;

namespace Ceres.Serialization
{
    [Serializable]
    public class SerializedObjectBase
    {
        private abstract class Box
        {
            public abstract object GetValue();
        }

        [Serializable]
        private class Box<T> : Box
        {
            public T m_Value;

            public override object GetValue()
            {
                return m_Value;
            }
        }

        /// <summary>
        /// Formatted type metadata, see <see cref="SerializedType"/>
        /// </summary>
        public string serializedTypeString;

        /// <summary>
        /// Whether object is array
        /// </summary>
        public bool isArray;

        /// <summary>
        /// Serialized object data
        /// </summary>
        public string jsonData;

#if UNITY_EDITOR
        /// <summary>
        /// Editor wrapper, used in SerializedObjectDrawer
        /// </summary>
        [SerializeField]
        internal ulong objectHandle;
#endif
        public SerializedObjectBase Clone()
        {
            return new SerializedObjectBase
            {
                serializedTypeString = serializedTypeString,
                isArray = isArray,
                jsonData = jsonData
            };
        }

        public virtual Type GetObjectType()
        {
            var type = SerializedType.FromString(serializedTypeString);
            if (type == null)
            {
                return null;
            }

            if (isArray)
            {
                type = type.MakeArrayType();
            }
            return type;
        }

        private static bool IsTypeNeedBox(Type type)
        {
            return type.IsArray || type.IsPrimitive || type == typeof(string);
        }

        public Type GetBoxType()
        {
            var type = GetObjectType();
            if (type is null)
            {
                return null;
            }

            if (IsTypeNeedBox(type))
            {
                return typeof(Box<>).MakeGenericType(type);
            }
            return type;
        }

        public SerializedObject<T> Convert<T>()
        {
            return new SerializedObject<T>
            {
                serializedTypeString = serializedTypeString,
                isArray = isArray,
                jsonData = jsonData
            };
        }

        public T CreateInstance<T>()
        {
            var value = CreateInstance();
            if (value == null)
            {
                return default;
            }
            return (T)value;
        }

        public object CreateInstance()
        {
            var type = SerializedType.FromString(serializedTypeString);
            if (type == null)
            {
                return null;
            }
            if (isArray)
            {
                type = type.MakeArrayType();
            }

            return DeserializeObject(jsonData, type);
        }


        public static object DeserializeObject(string jsonData, Type objectType)
        {
            if (IsTypeNeedBox(objectType))
            {
                var boxType = typeof(Box<>).MakeGenericType(objectType);
                return ((Box)JsonUtility.FromJson(jsonData, boxType)).GetValue();
            }
            return JsonUtility.FromJson(jsonData, objectType);
        }

        public static string SerializeObject(object @object, Type objectType)
        {
            return SerializeObject_Imp(@object, IsTypeNeedBox(objectType));
        }

        private static string SerializeObject_Imp(object @object, bool needBox)
        {
            if (needBox)
            {
                var boxType = typeof(Box<>).MakeGenericType(@object.GetType());
                var box = Activator.CreateInstance(boxType);
                return JsonUtility.ToJson(box);
            }
            return JsonUtility.ToJson(@object);
        }

        public static SerializedObjectBase FromObject(object @object)
        {
            var type = @object.GetType();
            return new SerializedObjectBase
            {
                serializedTypeString = SerializedType.ToString(type.IsArray ? type.GetElementType() : type),
                jsonData = SerializeObject(@object, type),
                isArray = type.IsArray
            };
        }
    }

    /// <summary>
    /// Serialized object that will serialize metadata and fields of object implementing T
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public sealed class SerializedObject<T> : SerializedObjectBase
    {
#pragma warning disable CS8632
        [NonSerialized]
        private T? _value;
#pragma warning restore CS8632

        /// <summary>
        /// Get default object from <see cref="SerializedObject{T}"/>
        /// </summary>
        /// <returns></returns>
        public T GetObject()
        {
            if (_value == null)
            {
                _value = CreateInstance<T>();
            }
            return _value;
        }

        /// <summary>
        /// Instantiate new object from <see cref="SerializedObject{T}"/>
        /// </summary>
        /// <returns></returns>
        public T NewObject()
        {
            return CreateInstance<T>(); ;
        }

        /// <summary>
        /// Get object type from <see cref="SerializedObject{T}"/>
        /// </summary>
        /// <returns></returns>
        public override Type GetObjectType()
        {
            return _value != null ? _value.GetType() : base.GetObjectType();
        }

        /// <summary>
        /// Create <see cref="SerializedObject{T}"/> from object
        /// </summary>
        /// <param name="object"></param>
        /// <returns></returns>
        public new static SerializedObject<T> FromObject(object @object)
        {
            var type = @object.GetType();
            return new SerializedObject<T>
            {
                serializedTypeString = SerializedType.ToString(type.IsArray ? type.GetElementType() : type),
                jsonData = SerializeObject(@object, type),
                isArray = type.IsArray
            };
        }

        public SerializedObject<T> CloneT()
        {
            return new SerializedObject<T>
            {
                serializedTypeString = serializedTypeString,
                isArray = isArray,
                jsonData = jsonData
            };
        }

        internal void InternalUpdate()
        {
            if (_value != null && SerializedType.ToString(_value.GetType()) != serializedTypeString)
            {
                _value = default;
            }
        }
    }
}
