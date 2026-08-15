using System;
using System.Collections.Generic;
using Ceres.Collections;
using UnityEngine.Assertions;

namespace Ceres.Gameplay
{
    public interface IContainerSubsystem
    {
        /// <summary>
        /// Register target type instance
        /// </summary>
        /// <param name="instance"></param>
        /// <typeparam name="T"></typeparam>
        void Register<T>(T instance);
        
        /// <summary>
        /// Unregister target type instance
        /// </summary>
        /// <param name="instance"></param>
        /// <typeparam name="T"></typeparam>
        void Unregister<T>(T instance);

        /// <summary>
        /// Get target type instance
        /// </summary>
        /// <typeparam name="T"></typeparam>
        T Resolve<T>() where T : class;
        
        /// <summary>
        /// Register a callback when target type instance is registered
        /// </summary>
        /// <param name="callback"></param>
        /// <typeparam name="T"></typeparam>
        void RegisterCallback<T>(Action<T> callback);
    }
    
    /// <summary>
    /// World lifetime scope IOC subsystem
    /// </summary>
    [InitializeOnWorldCreate]
    public class ContainerSubsystem : WorldSubsystem, IContainerSubsystem
    {
        private class Empty : IContainerSubsystem
        {
            public static readonly IContainerSubsystem Instance = new Empty();
            
            public void Register<T>(T instance)
            {
                
            }

            public void Unregister<T>(T instance)
            {
                
            }

            public T Resolve<T>() where T : class
            {
                return null;
            }

            public void RegisterCallback<T>(Action<T> callback)
            {
                
            }
        }
        
        private readonly IOCContainer _container = new();
        
        private readonly Dictionary<Type, Action<object>> _typeCallbackMap = new();

        public static IContainerSubsystem Get()
        {
            return GameWorld.Get().GetSubsystem<ContainerSubsystem>() ?? Empty.Instance;
        }
        
        void IContainerSubsystem.RegisterCallback<T>(Action<T> callback)
        {
            Assert.IsNotNull(callback, "[ContainerSubsystem] Instance callback is null, which is not expected.");
            var type = typeof(T);
            if (!_typeCallbackMap.ContainsKey(type))
            {
                _typeCallbackMap[type] = obj => callback((T)obj);
            }
            else
            {
                _typeCallbackMap[type] += obj => callback((T)obj);
            }
        }
        
        void IContainerSubsystem.Register<T>(T instance)
        {
            _container.Register(instance);
            var type = typeof(T);
            if (_typeCallbackMap.TryGetValue(type, out var callBack))
            {
                callBack?.Invoke(instance);
            }
        }
        
        void IContainerSubsystem.Unregister<T>(T instance)
        {
            _container.Unregister(instance);
        }
        
        T IContainerSubsystem.Resolve<T>() where T : class
        {
            return _container.Resolve<T>();
        }
        
        protected override void Release()
        {
            _container.Clear();
            _typeCallbackMap.Clear();
        }
    }
}