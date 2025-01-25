using UnityEngine;
using System;
using Chris;
namespace Ceres
{
    /// <summary>
    /// Variable can be shared between behaviors in behavior tree
    /// </summary>
    [Serializable]
    public abstract class SharedVariable : ICloneable, IDisposable
    {
        /// <summary>
        /// Whether variable is shared
        /// </summary>
        /// <value></value>
        public bool IsShared
        {
            get => isShared;
            set => isShared = value;
        }
        
        [SerializeField]
        private bool isShared;
        
        /// <summary>
        /// Whether variable is global
        /// </summary>
        /// <value></value>
        public bool IsGlobal
        {
            get => isGlobal;
            set => isGlobal = value;
        }
        
        [SerializeField]
        private bool isGlobal;
        
        /// <summary>
		/// Whether variable is exposed to editor
		/// </summary>
		/// <value></value>
		public bool IsExposed
        {
            get => isExposed;
            set => isExposed = value;
        }
        
        [SerializeField]
        private bool isExposed;
        
        public string Name
        {
            get => mName;
            set => mName = value;
        }
        
        public abstract object GetValue();
        
        public abstract void SetValue(object newValue);
        
        /// <summary>
        /// Bind to other sharedVariable
        /// </summary>
        /// <param name="other"></param>
        public abstract void Bind(SharedVariable other);
        
        /// <summary>
        /// Unbind self
        /// </summary>
        public abstract void Dispose();
        
        /// <summary>
        /// Clone shared variable by deep copy, an option here is to override for preventing using reflection
        /// </summary>
        /// <returns></returns>
        public virtual SharedVariable Clone()
        {
            return ReflectionUtility.DeepCopy(this);
        }

        [SerializeField]
        private string mName;
        
        /// <summary>
        /// Create a observe proxy variable
        /// </summary>
        /// <returns></returns>
        public abstract ObserveProxyVariable Observe();

        public virtual Type GetValueType()
        {
            return typeof(object);
        }

        object ICloneable.Clone()
        {
            return Clone();
        }
    }
    [Serializable]
    public abstract class SharedVariable<T> : SharedVariable, IBindableVariable<T>
    {
        public T Value
        {
            get => Getter == null ? value : Getter();
            set
            {
                if (Setter != null)
                {
                    Setter(value);
                }
                else
                {
                    this.value = value;
                }
            }
        }
        
        public sealed override object GetValue()
        {
            return Value;
        }
        
        public sealed override void SetValue(object newValue)
        {
            if (Setter != null)
            {
                Setter((T)newValue);
            }
            else if (newValue is IConvertible)
            {
                value = (T)Convert.ChangeType(newValue, typeof(T));
            }
            else
            {
                value = (T)newValue;
            }
        }
        
        protected Func<T> Getter;
        
        protected Action<T> Setter;
        
        public void Bind(IBindableVariable<T> other)
        {
            Getter = () => other.Value;
            Setter = (evt) => other.Value = evt;
        }
        
        public override void Bind(SharedVariable other)
        {
            if (other is IBindableVariable<T> variable)
            {
                Bind(variable);
            }
            else
            {
                Debug.LogError($"Variable named with {Name} bind failed!");
            }
        }
        
        public override void Dispose()
        {
            Getter = null;
            Setter = null;
        }
        
        [SerializeField]
        protected T value;
        
        public override ObserveProxyVariable Observe()
        {
            return ObserveT();
        }
        
        public ObserveProxyVariable<T> ObserveT()
        {
            Setter ??= (evt) => { value = evt; };
            var wrapper = new SetterWrapper<T>((w) => { Setter -= w.Invoke; });
            var proxy = new ObserveProxyVariable<T>(this, in wrapper);
            Setter += wrapper.Invoke;
            return proxy;
        }
        
        public sealed override SharedVariable Clone()
        {
            var variable = CloneT();
            variable.CopyProperty(this);
            return variable;
        }
        
        protected virtual SharedVariable<T> CloneT()
        {
            return ReflectionUtility.DeepCopy(this);
        }
        
        protected void CopyProperty(SharedVariable other)
        {
            IsGlobal = other.IsGlobal;
            IsExposed = other.IsExposed;
            IsShared = other.IsShared;
            Name = other.Name;
        }
        
        public override Type GetValueType()
        {
            return typeof(T);
        }
    }
    public class SetterWrapper<T> : IDisposable
    {
        private readonly Action<SetterWrapper<T>> _unregister;
        
        public Action<T> Setter;
        
        public void Invoke(T value)
        {
            Setter(value);
        }
        
        public void Dispose()
        {
            _unregister(this);
        }
        
        public SetterWrapper(Action<SetterWrapper<T>> unRegister)
        {
            _unregister = unRegister;
        }
    }
    
    /// <summary>
    /// Proxy variable to observe value change
    /// </summary>
    public abstract class ObserveProxyVariable : IDisposable
    {
        public abstract void Register(Action<object> onValueChangeCallback);
        
        public abstract void Dispose();
    }
    
    public class ObserveProxyVariable<T> : ObserveProxyVariable, IBindableVariable<T>
    {
        public T Value
        {
            get => _getter();
            set => _setter(value);
        }
        
        private Func<T> _getter;
        
        private Action<T> _setter;
        
        private readonly SetterWrapper<T> _setterWrapper;
        
        public void Bind(IBindableVariable<T> other)
        {
            _getter = () => other.Value;
            _setter = evt => other.Value = evt;
        }
        
        public ObserveProxyVariable(SharedVariable<T> variable, in SetterWrapper<T> setterWrapper)
        {
            _setterWrapper = setterWrapper;
            setterWrapper.Setter = Notify;
            Bind(variable);
        }
        
        public event Action<T> OnValueChange;
        
        private void Notify(T value)
        {
            OnValueChange?.Invoke(value);
        }
        
        public sealed override void Dispose()
        {
            _setterWrapper.Dispose();
            _getter = null;
            _setter = null;
        }

        public override void Register(Action<object> onValueChangeCallback)
        {
            OnValueChange += x => onValueChangeCallback?.Invoke(x);
        }
    }
}