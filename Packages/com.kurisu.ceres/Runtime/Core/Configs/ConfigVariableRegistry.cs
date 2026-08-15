using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using R3;

namespace Ceres.Configs
{
    public class ConfigVariableRegistry
    {
        private const string CeresAssemblyName = "Ceres";

        private readonly Dictionary<string, ConfigVariable> _variables = new();

        private static ConfigVariableRegistry _instance;

        // Type mapping for console variables
        private static readonly Dictionary<Type, Type> TypeToConsoleVariableMap = new()
        {
            { typeof(int), typeof(IntConfigVariable<>) },
            { typeof(float), typeof(FloatConfigVariable<>) },
            { typeof(bool), typeof(BoolConfigVariable<>) },
            { typeof(string), typeof(StringConfigVariable<>) }
        };

        private ConfigVariableRegistry()
        {
            InitializeVariables();
        }

        public static ConfigVariableRegistry Get()
        {
            return _instance ??= new ConfigVariableRegistry();
        }

        private void InitializeVariables()
        {
            var configTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetReferencedAssemblies()
                    .Any(a => a.Name == CeresAssemblyName) || assembly.GetName().Name == CeresAssemblyName)
                .SelectMany(assembly => assembly.GetTypes()
                    .Where(IsConfigType))
                .ToArray();

            foreach (var configType in configTypes)
            {
                ProcessConfigType(configType);
            }
        }

        private void ProcessConfigType(Type configType)
        {
            // Process fields
            ProcessMembers(configType, GetFieldsWithAttribute(configType), CreateFieldAccessor);

            // Process properties
            ProcessMembers(configType, GetPropertiesWithAttribute(configType), CreatePropertyAccessor);
        }

        private static FieldInfo[] GetFieldsWithAttribute(Type configType)
        {
            return configType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => field.GetCustomAttribute<ConfigVariableAttribute>() != null)
                .ToArray();
        }

        private static PropertyInfo[] GetPropertiesWithAttribute(Type configType)
        {
            return configType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(prop => prop.GetCustomAttribute<ConfigVariableAttribute>() != null)
                .Where(prop => prop.CanRead && prop.CanWrite)
                .ToArray();
        }

        private void ProcessMembers<T>(Type configType, T[] members, Func<T, MemberAccessor> accessorFactory) where T : MemberInfo
        {
            foreach (var member in members)
            {
                var attribute = member.GetCustomAttribute<ConfigVariableAttribute>();
                if (attribute == null) continue;

                var memberAccessor = accessorFactory(member);
                if (memberAccessor == null) continue;

                CreateConsoleVariable(configType, memberAccessor, attribute);
            }
        }

        private static MemberAccessor CreateFieldAccessor(FieldInfo field)
        {
            return new FieldAccessor(field);
        }

        private static MemberAccessor CreatePropertyAccessor(PropertyInfo property)
        {
            if (!property.CanRead || !property.CanWrite)
            {
                Debug.LogWarning($"[Ceres] Property {property.Name} on {property.DeclaringType?.Name} must have both getter and setter to be used as console variable");
                return null;
            }

            return new PropertyAccessor(property);
        }

        private void CreateConsoleVariable(Type configType, MemberAccessor memberAccessor, ConfigVariableAttribute attribute)
        {
            var variableName = attribute.Name;
#if !UNITY_EDITOR
            if (attribute.IsEditor)
            {
                return;
            }
#endif

            var memberType = memberAccessor.MemberType;

            try
            {
                var variable = CreateConsoleVariable_Internal(configType, memberAccessor, memberType, variableName);

                if (variable == null)
                {
                    Debug.LogWarning($"[Ceres] Unsupported member type {memberType} for console variable {variableName}");
                    return;
                }

                if (!_variables.TryAdd(variableName, variable))
                {
                    Debug.LogWarning($"[Ceres] A config variable named {variableName} already exists!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Ceres] Failed to create console variable {variableName}: {ex.Message}");
            }
        }

        private static ConfigVariable CreateConsoleVariable_Internal(Type configType, MemberAccessor memberAccessor,
            Type memberType, string variableName)
        {
            // Check if the member type is ReactiveProperty<T>
            Type valueType;
            MemberAccessor accessorToUse = memberAccessor;

            if (memberType.IsGenericType && memberType.GetGenericTypeDefinition() == typeof(ReactiveProperty<>))
            {
                // Extract the generic type argument from ReactiveProperty<T>
                valueType = memberType.GetGenericArguments()[0];

                // Create a wrapper accessor that accesses .Value
                accessorToUse = new ReactivePropertyAccessor(memberAccessor, memberType);
            }
            else
            {
                valueType = memberType;
            }

            // Redirect enum to int
            if (valueType.IsEnum)
            {
                valueType = typeof(int);
            }

            var variable = TypeToConsoleVariableMap.TryGetValue(valueType, out var consoleVariableType)
                ? CreateTypedConsoleVariable(configType, accessorToUse, variableName, consoleVariableType)
                : null;
            return variable;
        }

        private static ConfigVariable CreateTypedConsoleVariable(Type configType, MemberAccessor memberAccessor, string variableName, Type consoleVariableGenericType)
        {
            // Create the generic type with the config type
            var consoleVariableType = consoleVariableGenericType.MakeGenericType(configType);

            // Create instance using constructor
            var constructor = consoleVariableType.GetConstructor(new[] { typeof(string), typeof(MemberAccessor) });
            if (constructor == null)
            {
                Debug.LogError($"Constructor not found for console variable type {consoleVariableType}");
                return null;
            }

            return (ConfigVariable)constructor.Invoke(new object[] { variableName, memberAccessor });
        }

        /// <summary>
        /// Get console variable by name
        /// </summary>
        /// <param name="name">Variable name</param>
        /// <returns>Console variable if found, null otherwise</returns>
        public ConfigVariable GetVariable(string name)
        {
            _variables.TryGetValue(name, out var variable);
            return variable;
        }

        /// <summary>
        /// Try to get console variable by name
        /// </summary>
        /// <param name="name">Variable name</param>
        /// <param name="variable">The found variable</param>
        /// <returns>True if variable exists, false otherwise</returns>
        public bool TryGetVariable(string name, out ConfigVariable variable)
        {
            variable = null;

            if (_variables.TryGetValue(name, out variable))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Load a config if it's not already loaded
        /// </summary>
        /// <param name="configType">Config type to load</param>
        /// <returns>True if config was loaded or already loaded, false otherwise</returns>
        public bool LoadConfig(Type configType)
        {
            // Use reflection to call ConfigSystem.GetConfig<T>() where T is the config type
            var getConfigMethod = typeof(ConfigSystem).GetMethod("GetConfig");
            if (getConfigMethod == null)
                return false;

            var genericMethod = getConfigMethod.MakeGenericMethod(configType);

            try
            {
                var config = genericMethod.Invoke(null, null);
                return config != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get all registered console variables
        /// </summary>
        /// <returns>Dictionary of all variables</returns>
        public ConfigVariable[] GetAllVariables()
        {
            return _variables.Values.ToArray();
        }

        /// <summary>
        /// Check if a variable exists
        /// </summary>
        /// <param name="name">Variable name</param>
        /// <returns>True if variable exists, false otherwise</returns>
        public bool HasVariable(string name)
        {
            return _variables.ContainsKey(name);
        }

        private static bool IsConfigType(Type t)
        {
            if (t.IsAbstract || t.IsGenericType) return false;
            return t.IsInheritedFromGenericDefinition(typeof(Config<>), out _);
        }
    }
}
