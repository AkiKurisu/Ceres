using System.Collections.Generic;
using System;
namespace Ceres.Collections
{
    internal class IOCContainer
    {
        private readonly Dictionary<Type, object> _instances = new();

        /// <summary>
        /// Register instance
        /// </summary>
        /// <param name="instance"></param>
        /// <typeparam name="T"></typeparam>
        public void Register<T>(T instance)
        {
            var type = typeof(T);
            _instances[type] = instance;
        }

        /// <summary>
        /// UnRegister instance
        /// </summary>
        /// <param name="instance"></param>
        /// <typeparam name="T"></typeparam>
        public void Unregister<T>(T instance)
        {
            var type = typeof(T);
            if (_instances.ContainsKey(type) && _instances[type].Equals(instance))
            {
                _instances.Remove(type);
            }
        }

        /// <summary>
        /// Get registered instance
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public T Resolve<T>() where T : class
        {
            var type = typeof(T);
            if (_instances.TryGetValue(type, out var obj))
            {
                return obj as T;
            }
            return null;
        }

        /// <summary>
        /// Clear registered instances
        /// </summary>
        public void Clear()
        {
            _instances.Clear();
        }
    }
}
