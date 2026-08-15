using UnityEngine;
using UObject = UnityEngine.Object;

namespace Ceres.Pool
{
    public class PooledComponent<T, TComponent> : PooledGameObject 
        where TComponent : Component 
        where T : PooledComponent<T, TComponent>, new()
    {
        /// <summary>
        /// Cache component as meta data to reduce allocation
        /// </summary>
        public class ComponentCache : IPooledMetadata
        {
            public TComponent Component;
        }
        
        public TComponent Component => Cache.Component;
        
        protected ComponentCache Cache { get; set; }
        
        private static readonly PoolKey ComponentKey;
        
        static PooledComponent()
        {
            ComponentKey = new PoolKey(typeof(T).FullName);
        }
        
        internal static readonly _ObjectPool<T> Pool = new(() => new T());
        
        public new static void SetMaxSize(int size)
        {
            Pool.MaxSize = size;
        }

        /// <summary>
        /// Get or create empty pooled component
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static T Get(Transform parent = null)
        {
            var pooledComponent = Pool.Get();
            pooledComponent.PoolKey = ComponentKey;
            pooledComponent.GameObject = GameObjectPoolManager.Get(ComponentKey, out var metadata, parent, createEmptyIfNotExist: true);
            pooledComponent.Cache = metadata as ComponentCache;
            pooledComponent.Init();
            return pooledComponent;
        }
        
        private const string Prefix = "Prefab";
        
        private static IPooledMetadata _metadata;
        
        public static PoolKey GetPooledKey(GameObject prefab)
        {
            // append instance id since prefabs may have same name
            return new PoolKey(Prefix, prefab.GetInstanceID());
        }
        
        /// <summary>
        /// Instantiate pooled component by prefab, optimized version of <see cref="Object.Instantiate(Object, Transform)"/> 
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static T Instantiate(GameObject prefab, Transform parent = null)
        {
            var pooledComponent = Pool.Get();
            var key = GetPooledKey(prefab);
            pooledComponent.PoolKey = key;
            var @object = GameObjectPoolManager.Get(key, out _metadata, parent, createEmptyIfNotExist: false);
            if (!@object)
            {
                @object = UObject.Instantiate(prefab, parent);
            }
            pooledComponent.Cache = _metadata as ComponentCache;
            pooledComponent.GameObject = @object;
            pooledComponent.Init();
            return pooledComponent;
        }
        
        /// <summary>
        /// Instantiate pooled component by prefab, optimized version of <see cref="Object.Instantiate(Object, Vector3, Quaternion, Transform)"/> 
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="parent">The parent attached to. If parent exists, it will use prefab's scale as local scale instead of lossy scale</param>
        /// <param name="useLocalPosition">Whether to use local position instead of world position, default is false</param>
        /// <returns></returns>
        public static T Instantiate(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null, bool useLocalPosition = false)
        {
            var pooledComponent = Instantiate(prefab, parent);
            if (useLocalPosition)
                pooledComponent.GameObject.transform.SetLocalPositionAndRotation(position, rotation);
            else
                pooledComponent.GameObject.transform.SetPositionAndRotation(position, rotation);
            return pooledComponent;
        }
        
        protected override void Init()
        {
            IsDisposed = false;
            InitDisposables();
            Transform = GameObject.transform;
            Cache ??= new ComponentCache();
            if (!Cache.Component)
            {
                // allocate few to get component from gameObject
                Cache.Component = GameObject.GetOrAddComponent<TComponent>();
            }
        }
        
        public sealed override void Dispose()
        {
            if (IsDisposed) return;
            OnDispose();
            ReleaseDisposables();
            if (GameObjectPoolManager.IsInstantiated)
                GameObjectPoolManager.Release(GameObject, PoolKey, Cache);
            IsDisposed = true;
            Pool.Release((T)this);
        }
        
        protected virtual void OnDispose() { }
    }
}