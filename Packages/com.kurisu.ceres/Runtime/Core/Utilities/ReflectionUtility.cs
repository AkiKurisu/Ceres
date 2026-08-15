using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;
using UObject = UnityEngine.Object;

namespace Ceres
{
    public static class ReflectionUtility
    {
        /// <summary>
        /// Gets all fields from an object and its hierarchy inheritance.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="flags">The flags.</param>
        /// <returns>All fields of the type.</returns>
        public static List<FieldInfo> GetAllFields(this Type type, BindingFlags flags)
        {
            // Early exit if Object type
            if (type == typeof(object))
            {
                return new List<FieldInfo>();
            }

            // Recursive call
            var fields = type.BaseType.GetAllFields(flags);
            fields.AddRange(type.GetFields(flags | BindingFlags.DeclaredOnly));
            return fields;
        }

        /// <summary>
        /// Perform a deep copy of the class.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj">The object.</param>
        /// <returns>A deep copy of obj.</returns>
        /// <exception cref="ArgumentNullException">Object cannot be null</exception>
        public static T DeepCopy<T>(T obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj), "object can not be null");
            }
            return (T)DoCopy(obj);
        }


        /// <summary>
        /// Does the copy.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Unknown type</exception>
        private static object DoCopy(object obj)
        {
            if (obj == null)
            {
                return null;
            }

            // Value type
            var type = obj.GetType();
            if (type.IsValueType || type == typeof(string))
            {
                return obj;
            }

            // Array
            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                var array = (Array)obj;
                Array copied = Array.CreateInstance(elementType!, array.Length);
                for (int i = 0; i < array.Length; i++)
                {
                    copied.SetValue(DoCopy(array.GetValue(i)), i);
                }
                return Convert.ChangeType(copied, obj.GetType());
            }

            // Unity Object
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return obj;
            }

            // Class -> Recursion
            if (type.IsClass)
            {
                var copy = Activator.CreateInstance(obj.GetType());

                var fields = type.GetAllFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (FieldInfo field in fields)
                {
                    var fieldValue = field.GetValue(obj);
                    if (fieldValue != null)
                    {
                        field.SetValue(copy, DoCopy(fieldValue));
                    }
                }

                return copy;
            }

            // Fallback
            throw new ArgumentException("Unknown type");
        }
        
                
        /// <summary>
        /// Get type first generic argument type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type GetGenericArgumentType(Type type)
        {
            /* Prevent stackoverflow */
            const int maxDepth = 10;
            int depth = 0;
            while (type != null && !type.IsGenericType && depth < maxDepth)
            {
                type = type.BaseType;
                depth ++;
            }
            Assert.IsNotNull(type);
            Assert.IsTrue(type.GetGenericArguments().Length > 0);
            return type.GetGenericArguments()[0];
        }
        
        public static bool IsInheritedFromGenericDefinition(Type type, Type genericDefinition)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition)
            {
                return true;
            }
            
            if (type.BaseType != null)
            {
                if (IsInheritedFromGenericDefinition(type.BaseType, genericDefinition))
                {
                    return true;
                }
            }
            
            foreach (var interfaceType in type.GetInterfaces())
            {
                if (IsInheritedFromGenericDefinition(interfaceType, genericDefinition))
                {
                    return true;
                }
            }

            return false;
        }
        
         /// <summary>
        /// Get all field names of type that Unity can serialize
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static List<string> GetSerializedFieldsName(Type type)
        {
            List<FieldInfo> fieldInfos = new();
            GetAllSerializableFields(type, fieldInfos);
            return fieldInfos.Select(x => x.Name)
                            .ToList();
        }
         
        /// <summary>
        /// Get all fields of type that Unity can serialize
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static List<FieldInfo> GetSerializedFields(Type type)
        {
            List<FieldInfo> fieldInfos = new();
            GetAllSerializableFields(type, fieldInfos);
            return fieldInfos;
        }
         
        private static void GetAllSerializableFields(Type type, List<FieldInfo> fieldInfos)
        {
            if (type.BaseType != null)
            {
                GetAllSerializableFields(type.BaseType, fieldInfos);
            }

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public 
                                                        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {
                if (!ValidateSerializedField(field))
                {
                    continue;
                }

                fieldInfos.Add(field);
            }
        }

        private static bool ValidateSerializedField(FieldInfo field)
        {
            if (field.IsStatic) return false;

            if (field.IsInitOnly) return false;

            if (!field.IsPublic && !Attribute.IsDefined(field, typeof(SerializeField), false)) return false;

            if (!field.FieldType.IsSerializable && !typeof(UObject).IsAssignableFrom(field.FieldType)
                && !field.FieldType.IsPrimitive && !field.FieldType.IsEnum
                && !typeof(List<>).IsAssignableFrom(field.FieldType) && !field.FieldType.IsArray
                && !field.FieldType.IsGenericType
                && !Attribute.IsDefined(field.FieldType, typeof(SerializableAttribute), false)
                && !IsUnityBuiltinTypes(field.FieldType))
                return false;

            return true;
        }

        private static readonly HashSet<Type> SerializableNumericTypes = new()
        {
            typeof(byte), typeof(sbyte),
            typeof(int), typeof(uint),
            typeof(long), typeof(ulong),
            typeof(short), typeof(ushort),
            typeof(float),
            typeof(double),
            typeof(bool)
        };

        public static bool IsSerializableNumericTypes(Type type)
        {
            return SerializableNumericTypes.Contains(type);
        }
        
        private static readonly HashSet<Type> UnityBuiltinTypes = new()
        {
            typeof(AnimationCurve), 
            typeof(Bounds), typeof(BoundsInt), 
            typeof(Color),
            typeof(Quaternion), 
            typeof(Rect), typeof(RectInt), 
            typeof(Vector2), typeof(Vector2Int),
            typeof(Vector3), typeof(Vector3Int), 
            typeof(Vector4)
        };
        

        public static bool IsUnityBuiltinTypes(Type type)
        {
            return UnityBuiltinTypes.Contains(type);
        }
        
        public static object CreateDefaultValue(Type type)
        {
            if (type.IsArray)
            {
                return Array.CreateInstance(type.GetElementType()!, 0);
            }
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            if (type == typeof(string))
            {
                return string.Empty;
            }
            try
            {
                return Activator.CreateInstance(type);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Determines whether the specified type inherits from the specified generic type definition, and obtains the generic parameter type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="genericDefinition"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static bool IsInheritedFromGenericDefinition(this Type type, Type genericDefinition, out Type[] arguments)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition)
            {
                arguments = type.GetGenericArguments();
                return true;
            }
            
            if (type.BaseType != null)
            {
                if (IsInheritedFromGenericDefinition(type.BaseType, genericDefinition, out arguments))
                {
                    return true;
                }
            }
            
            foreach (var interfaceType in type.GetInterfaces())
            {
                if (IsInheritedFromGenericDefinition(interfaceType, genericDefinition, out arguments))
                {
                    return true;
                }
            }

            arguments = Type.EmptyTypes;
            return false;
        }
    }
}
