using System;
using System.Collections.Generic;
using Ceres.Schedulers;
using R3.Ceres;
using UnityEngine;
using UnityEngine.Pool;

namespace Ceres.Pool
{
    /// <summary>
    /// Wrapper for pooling gameObject
    /// </summary>
    public class PooledGameObject : IDisposable, IDisposableUnregister
    {
        private static readonly _ObjectPool<PooledGameObject> Pool = new(() => new PooledGameObject());
        
        public GameObject GameObject { get; protected set; }
        
        public Transform Transform { get; protected set; }

        protected bool IsDisposed { get; set; }
        
        /// <summary>
        /// Disposable managed by pooling scope
        /// </summary>
        /// <returns></returns>
        private readonly List<IDisposable> _disposables = new();
        
        private List<SchedulerHandle> _schedulerHandles;
        
        /// <summary>
        /// Key to GameObject pool
        /// </summary>
        /// <value></value>
        protected PoolKey PoolKey { get; set; }
        
        public static void SetMaxSize(int size)
        {
            Pool.MaxSize = size;
        }
        
        /// <summary>
        /// Get or create empty pooled gameObject by address
        /// </summary>
        /// <param name="address"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static PooledGameObject Get(string address, Transform parent = null)
        {
            var pooledObject = Pool.Get();
            pooledObject.PoolKey = new PoolKey(address);
            pooledObject.GameObject = GameObjectPoolManager.Get(pooledObject.PoolKey, out _, parent, createEmptyIfNotExist: true);
            pooledObject.Init();
            return pooledObject;
        }

        /// <summary>
        /// Should not use AddTo(GameObject) since gameObject will not be destroyed until pool manager cleanup.
        /// </summary>
        /// <param name="disposable"></param>
        /// <remarks>
        /// Implement of <see cref="IDisposableUnregister"/> to manage <see cref="IDisposable"/> in pooling scope.
        /// </remarks>
        void IDisposableUnregister.Register(IDisposable disposable)
        {
            _disposables.Add(disposable);
        }
        
        /// <summary>
        /// Add <see cref="SchedulerHandle"/> manually to reduce allocation
        /// </summary>
        /// <param name="handle"></param>
        protected void Add(SchedulerHandle handle)
        {
            _schedulerHandles ??= ListPool<SchedulerHandle>.Get();
            _schedulerHandles.Add(handle);
        }
        
        protected virtual void Init()
        {
            LocalInit();
        }
        
        private void LocalInit()
        {
            IsDisposed = false;
            Transform = GameObject.transform;
            InitDisposables();
        }
        
        public virtual void Dispose()
        {
            if (IsDisposed) return;
            ReleaseDisposables();
            if (GameObjectPoolManager.IsInstantiated)
                GameObjectPoolManager.Release(GameObject, PoolKey);
            IsDisposed = true;
            Pool.Release(this);
        }
        
        protected void InitDisposables()
        {
            _disposables.Clear();
        }
        
        protected void ReleaseDisposables()
        {
            foreach (var disposable in _disposables)
                disposable.Dispose();
            _disposables.Clear();
            if (_schedulerHandles == null) return;
            foreach (var schedulerHandle in _schedulerHandles)
                schedulerHandle.Cancel();
            ListPool<SchedulerHandle>.Release(_schedulerHandles);
            _schedulerHandles = null;
        }
        
        public unsafe void Destroy(float t = 0f)
        {
            if (t >= 0f)
                Add(Scheduler.DelayUnsafe(t, new SchedulerUnsafeBinding(this, &Dispose_Imp)));
            else
                Dispose();
        }
        
        private static void Dispose_Imp(object @object)
        {
            ((IDisposable)@object).Dispose();
        }
    }
}