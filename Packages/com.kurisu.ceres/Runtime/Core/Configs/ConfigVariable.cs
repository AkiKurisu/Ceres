using System;
using System.Reflection;
using UnityEngine;

namespace Ceres.Configs
{
    /// <summary>
    /// Abstraction for accessing both fields and properties
    /// </summary>
    public abstract class MemberAccessor
    {
        public abstract Type MemberType { get; }

        public abstract string Name { get; }

        public abstract object GetValue(object target);

        public abstract void SetValue(object target, object value);
    }

    /// <summary>
    /// Field accessor implementation
    /// </summary>
    public class FieldAccessor : MemberAccessor
    {
        private readonly FieldInfo _fieldInfo;

        public FieldAccessor(FieldInfo fieldInfo)
        {
            _fieldInfo = fieldInfo;
        }

        public override Type MemberType => _fieldInfo.FieldType;

        public override string Name => _fieldInfo.Name;

        public override object GetValue(object target)
        {
            return _fieldInfo.GetValue(target);
        }

        public override void SetValue(object target, object value)
        {
            _fieldInfo.SetValue(target, value);
        }
    }

    /// <summary>
    /// Property accessor implementation
    /// </summary>
    public class PropertyAccessor : MemberAccessor
    {
        private readonly PropertyInfo _propertyInfo;

        public PropertyAccessor(PropertyInfo propertyInfo)
        {
            _propertyInfo = propertyInfo;
        }

        public override Type MemberType => _propertyInfo.PropertyType;

        public override string Name => _propertyInfo.Name;

        public override object GetValue(object target)
        {
            return _propertyInfo.GetValue(target);
        }

        public override void SetValue(object target, object value)
        {
            _propertyInfo.SetValue(target, value);
        }
    }

    /// <summary>
    /// Accessor for ReactiveProperty&lt;T&gt; that accesses the .Value property
    /// </summary>
    public class ReactivePropertyAccessor : MemberAccessor
    {
        private readonly MemberAccessor _innerAccessor;
        
        private readonly PropertyInfo _valueProperty;
        
        private readonly Type _valueType;

        public ReactivePropertyAccessor(MemberAccessor innerAccessor, Type reactivePropertyType)
        {
            _innerAccessor = innerAccessor;

            // Extract the generic type argument from ReactiveProperty<T>
            _valueType = reactivePropertyType.GetGenericArguments()[0];

            // Get the Value property from ReactiveProperty<T>
            _valueProperty = reactivePropertyType.GetProperty("Value");
            if (_valueProperty == null)
            {
                throw new InvalidOperationException($"ReactiveProperty type {reactivePropertyType} does not have a Value property");
            }
        }

        public override Type MemberType => _valueType;

        public override string Name => _innerAccessor.Name;

        public override object GetValue(object target)
        {
            // Get the ReactiveProperty<T> instance
            var reactiveProperty = _innerAccessor.GetValue(target);
            if (reactiveProperty == null)
            {
                return null;
            }

            // Get the .Value property from ReactiveProperty<T>
            return _valueProperty.GetValue(reactiveProperty);
        }

        public override void SetValue(object target, object value)
        {
            // Get the ReactiveProperty<T> instance
            var reactiveProperty = _innerAccessor.GetValue(target);
            if (reactiveProperty == null)
            {
                Debug.LogWarning($"[Ceres] ReactiveProperty {Name} is null, cannot set value");
                return;
            }

            // Set the .Value property on ReactiveProperty<T>
            _valueProperty.SetValue(reactiveProperty, value);
        }
    }

    /// <summary>
    /// Base class for config variables that can be modified through console
    /// </summary>
    public abstract class ConfigVariable
    {
        public string Name { get; }

        public Type ConfigType { get; protected set; }

        protected MemberAccessor MemberAccessor { get; }

        protected ConfigVariable(string name, Type configType, MemberAccessor memberAccessor)
        {
            Name = name;
            ConfigType = configType;
            MemberAccessor = memberAccessor;
        }

        /// <summary>
        /// Get the current value of the variable
        /// </summary>
        /// <returns>Current value</returns>
        public abstract object GetValue();

        /// <summary>
        /// Set the value of the variable
        /// </summary>
        /// <param name="value">New value</param>
        public void SetValue(object value)
        {
            SetValue(value, true);
        }

        public abstract void SetValue(object value, bool save);

        /// <summary>
        /// Get the type of the variable
        /// </summary>
        /// <returns>Variable type</returns>
        public virtual Type GetValueType()
        {
            return MemberAccessor.MemberType;
        }
    }

    /// <summary>
    /// Generic typed console variable
    /// </summary>
    /// <typeparam name="TConfig">Config type</typeparam>
    /// <typeparam name="TValue">Value type</typeparam>
    public abstract class ConfigVariable<TConfig, TValue> : ConfigVariable
        where TConfig : Config<TConfig>, new()
    {
        protected ConfigVariable(string name, MemberAccessor memberAccessor) : base(name, typeof(TConfig), memberAccessor)
        {
        }

        private static TConfig GetConfig()
        {
            return ConfigSystem.GetConfig<TConfig>();
        }

        public override object GetValue()
        {
            var config = GetConfig();
            return MemberAccessor.GetValue(config);
        }

        public override void SetValue(object value, bool save)
        {
            var config = GetConfig();
            if (value is TValue typedValue)
            {
                MemberAccessor.SetValue(config, typedValue);
                if (save) config.Save();
            }
            else
            {
                // Try to convert the value
                try
                {
                    var convertedValue = Convert.ChangeType(value, typeof(TValue));
                    MemberAccessor.SetValue(config, convertedValue);
                    if (save) config.Save();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Ceres] Failed to convert value {value} to type {typeof(TValue)}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Integer console variable
    /// </summary>
    /// <typeparam name="TConfig">Config type</typeparam>
    public class IntConfigVariable<TConfig> : ConfigVariable<TConfig, int>
        where TConfig : Config<TConfig>, new()
    {
        public IntConfigVariable(string name, MemberAccessor memberAccessor) : base(name, memberAccessor)
        {
        }
    }

    /// <summary>
    /// Float console variable
    /// </summary>
    /// <typeparam name="TConfig">Config type</typeparam>
    public class FloatConfigVariable<TConfig> : ConfigVariable<TConfig, float>
        where TConfig : Config<TConfig>, new()
    {
        public FloatConfigVariable(string name, MemberAccessor memberAccessor) : base(name, memberAccessor)
        {
        }
    }

    /// <summary>
    /// Boolean console variable
    /// </summary>
    /// <typeparam name="TConfig">Config type</typeparam>
    public class BoolConfigVariable<TConfig> : ConfigVariable<TConfig, bool>
        where TConfig : Config<TConfig>, new()
    {
        public BoolConfigVariable(string name, MemberAccessor memberAccessor) : base(name, memberAccessor)
        {
        }
    }

    /// <summary>
    /// String console variable
    /// </summary>
    /// <typeparam name="TConfig">Config type</typeparam>
    public class StringConfigVariable<TConfig> : ConfigVariable<TConfig, string>
        where TConfig : Config<TConfig>, new()
    {
        public StringConfigVariable(string name, MemberAccessor memberAccessor) : base(name, memberAccessor)
        {
        }
    }
}