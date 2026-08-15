using System;
using UnityEngine.Assertions;
using UnityEngine.ResourceManagement.AsyncOperations;
using UObject = UnityEngine.Object;

namespace Ceres.Resource
{
    /// <summary>
    /// Handle for streaming resource.
    /// A lightweight encapsulation of <see cref="AsyncOperationHandle"/>.
    /// </summary>
    public readonly struct ResourceHandle : IEquatable<ResourceHandle>, IDisposable
    {
        internal readonly uint Version;
        
        internal readonly int Index;
        
        internal readonly byte OperationType;
        
        internal AsyncOperationHandle InternalHandle => ResourceSystem.CastOperationHandle(Version, Index);
        
        public object Result => InternalHandle.Result;
        
        public ResourceHandle(uint version, int index, byte operationType)
        {
            Version = version;
            Index = index;
            OperationType = operationType;
        }
        
        /// <summary>
        /// Register completed result callback, no need to unregister since delegate list is clear after fire event
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterCallback(Action<UObject> callback)
        {
            Assert.IsNotNull(callback);
            InternalHandle.Completed += h => callback((UObject)h.Result);
        }
        
        /// <summary>
        /// Register completed result callback, no need to unregister since delegate list is clear after fire event
        /// </summary>
        /// <returns></returns>
        public UObject WaitForCompletion()
        {
            return InternalHandle.WaitForCompletion() as UObject;
        }
        
        /// <summary>
        /// Converts handle to be typed.
        /// To convert back to non-typed, implicit conversion is available.
        /// </summary>
        /// <typeparam name="T">The type of the handle.</typeparam>
        /// <returns>A new handle that is typed.</returns>
        public ResourceHandle<T> Convert<T>()
        {
            return this;
        }
        public bool Equals(ResourceHandle other)
        {
            return other.Index == Index && other.InternalHandle.Equals(InternalHandle);
        }
        /// <summary>
        /// Implement of <see cref="IDisposable"/> to release resource
        /// </summary>
        public void Dispose()
        {
            ResourceSystem.Release(this);
        }
    }
    /// <summary>
    /// Handle for streaming resource.
    /// A lightweight replacement of <see cref="AsyncOperationHandle{T}"/>.
    /// </summary>
    public readonly struct ResourceHandle<T> : IEquatable<ResourceHandle<T>>, IDisposable
    {
        internal readonly uint Version;
        
        internal readonly int Index;
        
        internal readonly byte OperationType;
        
        internal AsyncOperationHandle<T> InternalHandle => ResourceSystem.CastOperationHandle<T>(Version, Index);
        
        public T Result => InternalHandle.Result;
        
        public ResourceHandle(uint version, int index, byte operationType)
        {
            Version = version;
            Index = index;
            OperationType = operationType;
        }
        
        public static implicit operator ResourceHandle(ResourceHandle<T> obj)
        {
            return new ResourceHandle(obj.Version, obj.Index, obj.OperationType);
        }
        
        public static implicit operator ResourceHandle<T>(ResourceHandle obj)
        {
            return new ResourceHandle<T>(obj.Version, obj.Index, obj.OperationType);
        }
        
        /// <summary>
        /// Register completed result callback, no need to unregister since delegate list is clear after fire event
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterCallback(Action<T> callback)
        {
            Assert.IsNotNull(callback);
            InternalHandle.Completed += h => callback(h.Result);
        }
        
        /// <summary>
        /// Register completed result callback, no need to unregister since delegate list is clear after fire event
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterCallback(Action callback)
        {
            Assert.IsNotNull(callback);
            InternalHandle.Completed += _ => callback();
        }
        
        public T WaitForCompletion()
        {
            return InternalHandle.WaitForCompletion();
        }

        public bool Equals(ResourceHandle<T> other)
        {
            return other.Index == Index && other.InternalHandle.Equals(InternalHandle);
        }
        
        /// <summary>
        /// Implement of <see cref="IDisposable"/> to release resource
        /// </summary>
        public void Dispose()
        {
            ResourceSystem.Release(this);
        }
    }
}
