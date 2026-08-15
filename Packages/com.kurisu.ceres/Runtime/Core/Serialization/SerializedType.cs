// Modified from UnityEditor.Graphs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Debug = UnityEngine.Debug;

namespace Ceres.Serialization
{
    public static class SerializedType
    {
        [Serializable]
        public struct SerializedTypeData
        {
            public string typeName;
            
            public string genericTypeName;
            
            public bool isGeneric;
        }
        
        private static string StripTypeNameString(string str, int index)
        {
            int toIndex = index + 1;
            while (toIndex < str.Length && str[toIndex] != ',' && str[toIndex] != ']')
                toIndex++;
            return str.Remove(index, toIndex - index);
        }

        private static string StripAllFromTypeNameString(string str, string toStrip)
        {
            int lastIndex = str.IndexOf(toStrip, StringComparison.Ordinal);
            while (lastIndex != -1)
            {
                str = StripTypeNameString(str, lastIndex);
                lastIndex = str.IndexOf(toStrip, lastIndex, StringComparison.Ordinal);
            }
            return str;
        }

        private static string ToShortTypeName(Type t)
        {
            // strip version, token and culture info, only leave type name and assembly name
            var name = t.AssemblyQualifiedName;
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            name = StripAllFromTypeNameString(name, ", Version");
            name = StripAllFromTypeNameString(name, ", Culture");
            name = StripAllFromTypeNameString(name, ", PublicKeyToken");
            return name;
        }

        private static string SafeTypeName(Type type)
        {
            return type.FullName?.Replace('+', '.');
        }

        private static SerializedTypeData SplitTypeString(string serializedTypeString)
        {
            if (string.IsNullOrEmpty(serializedTypeString))
                throw new ArgumentException("Cannot parse serialized type string, it is empty.");

            SerializedTypeData data;
            data.isGeneric = IsGeneric(serializedTypeString);
            int index = serializedTypeString.IndexOf('#');
            
            /* Case when type string is in invalid format */
            if (index == -1)
            {
                data.typeName = string.Empty;
                data.genericTypeName = string.Empty;
                return data;
            }

            data.typeName = serializedTypeString[..index];
            data.genericTypeName = serializedTypeString.Substring(data.typeName.Length + 1,
                serializedTypeString.IndexOf('#', data.typeName.Length + 1) - data.typeName.Length - 1);
            return data;
        }

        private static string ToString(SerializedTypeData data)
        {
            return data.typeName + "#" + data.genericTypeName + "#" + (data.isGeneric ? "1" : "0");
        }

        private static Type FromData(SerializedTypeData data)
        {
            return Type.GetType(data.typeName, true);
        }

        #region Public API
        public static Type GenericType(Type t)
        {
            if (t.IsArray)
                return t.GetElementType();
            if (!t.IsGenericType)
                return t;
            var args = t.GetGenericArguments();
            if (args.Length != 1)
                throw new ArgumentException("Internal error: got generic type with more than one generic argument.");
            return args[0];
        }

        public static bool IsListType(Type t)
        {
            return typeof(IList).IsAssignableFrom(t);
        }

        public static string GetFullName(Type t)
        {
            if (!t.IsGenericType)
                return SafeTypeName(t);
            if (t.GetGenericTypeDefinition() != typeof(List<>))
                throw new ArgumentException("Internal error: got unsupported generic type");
            return $"System.Collections.Generic.List<{SafeTypeName(t.GetGenericArguments()[0])}>";
        }
        
        public static string ToString(Type t)
        {
            var data = new SerializedTypeData();

            if (t == null)
                return string.Empty;

            data.typeName = string.Empty;
            data.isGeneric = t.ContainsGenericParameters; /* eg. List<T> not List<> */

            data.typeName = data.isGeneric switch
            {
                true when t.IsGenericType => ToShortTypeName(t.GetGenericTypeDefinition()),
                true when t.IsArray => "T[]",
                true => "T",
                _ => ToShortTypeName(t)
            };

            return ToString(data);
        }

        public static Type FromString(string serializedTypeString)
        {
            if (string.IsNullOrEmpty(serializedTypeString))
                return null;
            /* Only support generic definition */
            if (IsGeneric(serializedTypeString))
            {
                Debug.LogError("SerializedType not support unassigned generic type");
                return null;
            }
            if (SerializedTypeRedirector.TryRedirectSerializedType(serializedTypeString, out var type))
            {
                return type;
            }
            var data = SplitTypeString(serializedTypeString);
            if (SerializedTypeRedirector.TryRedirect_Internal(data.typeName, out type))
            {
                return type;
            }
            return Type.GetType(data.typeName);
        }

        public static string GetTypeName(string serializedTypeString)
        {
            if (string.IsNullOrEmpty(serializedTypeString))
                return null;
            /* Only support generic definition */
            if (IsGeneric(serializedTypeString))
            {
                Debug.LogError("SerializedType not support unassigned generic type");
                return null;
            }
            var data = SplitTypeString(serializedTypeString);
            return data.typeName;
        }

        public static bool IsGeneric(string serializedTypeString)
        {
            if (string.IsNullOrEmpty(serializedTypeString))
                return false;
            return serializedTypeString[^1] == '1';
        }

        public static bool IsBaseTypeGeneric(string serializedTypeString)
        {
            if (string.IsNullOrEmpty(serializedTypeString))
                return false;
            var data = SplitTypeString(serializedTypeString);
            return data.isGeneric || data.genericTypeName != string.Empty;
        }

        public static string SetGenericArgumentType(string serializedTypeString, Type type)
        {
            if (!IsGeneric(serializedTypeString))
            {
                if (IsBaseTypeGeneric(serializedTypeString))
                    throw new ArgumentException("Trying to set a different generic type. Reset old one first.");

                throw new ArgumentException("Trying to set generic argument type for non generic type.");
            }

            var data = SplitTypeString(serializedTypeString);

            data.genericTypeName = data.typeName;
            data.isGeneric = false;

            data.typeName = data.typeName switch
            {
                "T" => ToShortTypeName(type),
                "T[]" => ToShortTypeName(type.MakeArrayType()),
                _ => ToShortTypeName(Type.GetType(data.typeName, true).GetGenericTypeDefinition().MakeGenericType(type)),
            };
            return ToString(data);
        }

        public static string ResetGenericArgumentType(string serializedTypeString)
        {
            if (string.IsNullOrEmpty(serializedTypeString))
                throw new ArgumentException("Cannot reset generic argument type for null type.");

            var data = SplitTypeString(serializedTypeString);

            if (string.IsNullOrEmpty(data.genericTypeName))
                throw new ArgumentException("Cannot reset generic argument type, previous generic type unknown.");

            data.typeName = data.genericTypeName;
            data.isGeneric = true;
            data.genericTypeName = string.Empty;

            return ToString(data);
        }

        public static bool CanAssignFromGenericType(string serializedTypeString, Type t)
        {
            var data = SplitTypeString(serializedTypeString);
            if (!data.isGeneric)
                return false;

            if (t.IsGenericType)
            {
                // don't allow connecting e.g. List<> to T (which is assumed to be simple non-generic type)
                if (data.typeName == "T" || data.typeName == "T[]")
                    return false;

                var args = t.GetGenericArguments();
                if (args.Length != 1)
                    return false;

                if (args[0].IsGenericType)
                    return false;

                return t.GetGenericTypeDefinition() == FromData(data).GetGenericTypeDefinition();
            }

            return data.typeName == "T" || data.typeName == "T[]"; // no constraints right now
        }
        #endregion
    }

    [Serializable]
    public abstract class SerializedTypeBase
    {
        /// <summary>
        /// Formatted type metadata, see <see cref="SerializedType"/>
        /// </summary>
        public string serializedTypeString;

        
        /// <summary>
        /// Whether type is valid
        /// </summary>
        /// <returns></returns>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(serializedTypeString)) return false;
            return GetObjectType() != null;
        }
        
        /// <summary>
        /// Get object type
        /// </summary>
        /// <returns></returns>
        public abstract Type GetObjectType();
    }

    /// <summary>
    /// Serialized type that will serialize metadata of class implementing T
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class SerializedType<T>: SerializedTypeBase
    {
#pragma warning disable CS8632
        private T? _value;

        private Type _type;
#pragma warning restore CS8632
        
        /// <summary>
        /// Get default object from <see cref="SerializedType{T}"/>
        /// </summary>
        /// <returns></returns>
        public T GetObject()
        {
            InternalUpdate();
            if (_value == null)
            {
                _type = SerializedType.FromString(serializedTypeString);
                if (_type != null)
                {
                    _value = (T)Activator.CreateInstance(_type);
                }
            }
            return _value;
        }
        
        public override Type GetObjectType()
        {
            InternalUpdate();
            if (_type != null)
            {
                return _type;
            }
            return _type = SerializedType.FromString(serializedTypeString);
        }

        /// <summary>
        /// Get default <see cref="SerializedType{T}"/>
        /// </summary>
        public static SerializedType<T> Default => FromType(typeof(T));
        
        /// <summary>
        /// Create <see cref="SerializedType{T}"/> from type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static SerializedType<T> FromType(Type type)
        {
            return new SerializedType<T>
            {
                serializedTypeString = SerializedType.ToString(type)
            };
        }
        
        [Conditional("UNITY_EDITOR")]
        internal void InternalUpdate()
        {
            if (_type != null && SerializedType.ToString(_type) != serializedTypeString)
            {
                _value = default;
                _type = null;
            }
        }
        
        public static implicit operator Type(SerializedType<T> serializedType)
        {
            return serializedType.GetObjectType();
        }
    }

    /// <summary>
    /// Attribute type that changed assembly, namespace or class name.
    /// </summary>
    /// <remarks>Format is {Namespace}.{ClassName}, {AssemblyName}</remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class FormerlySerializedTypeAttribute : Attribute
    {
        public string TypeName { get; }
        
        public FormerlySerializedTypeAttribute(string typeName)
        {
            TypeName = typeName;
        }
    }

    /// <summary>
    /// Attribute type that will not show in SerializedType search window
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class HideInSerializedTypeAttribute : Attribute
    {
        
    }
    
    public static class SerializedTypeRedirector
    {
        public delegate Type RedirectSerializedTypeDelegate(string serializedType);
        
        private static readonly Lazy<Dictionary<string, Type>> UpdatableType;

        /// <summary>
        /// Hook func before directing <see cref="SerializedType"/>
        /// </summary>
        public static event RedirectSerializedTypeDelegate RedirectSerializedType;
        
        static SerializedTypeRedirector()
        {
            UpdatableType = new Lazy<Dictionary<string, Type>>(() =>
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(x => x.GetTypes())
                        .Where(x => x.GetCustomAttribute<FormerlySerializedTypeAttribute>() != null)
                        .ToDictionary(x => x.GetCustomAttribute<FormerlySerializedTypeAttribute>().TypeName, x => x);
            });
        }
        
        /// <summary>
        /// Try get redirected type from <see cref="SerializedType"/>
        /// </summary>
        /// <param name="serializedType"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool TryRedirectSerializedType(string serializedType, out Type type)
        {
            type = RedirectSerializedType?.Invoke(serializedType);
            return type != null;
        }
        
        /// <summary>
        /// Try get redirected type from <see cref="SerializedType.SerializedTypeData.typeName"/>
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static bool TryRedirect_Internal(string typeName, out Type type)
        {
            return UpdatableType.Value.TryGetValue(typeName, out type);
        }
    }
}
