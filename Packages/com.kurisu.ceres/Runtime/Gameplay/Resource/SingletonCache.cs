using Ceres.Resource;
using R3;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace Ceres.Gameplay.Resource
{
    /// <summary>
    /// Singleton <see cref="ResourceCache{TAsset}"/> for sharing storage in <see cref="GameWorld"/> lifetime scope
    /// </summary>
    /// <typeparam name="TAsset"></typeparam>
    public class SingletonCache<TAsset> : ResourceCache<TAsset> where TAsset: UObject
    {
        private static SingletonCache<TAsset> _cache;

        private bool _isGlobal;

        /// <summary>
        /// Get singleton cache for sharing storage in <see cref="GameWorld"/> lifetime scope
        /// </summary>
        public static ResourceCache<TAsset> Instance => GetOrCreateInstance();
        
        private SingletonCache()
        {
            
        }

        private static ResourceCache<TAsset> GetOrCreateInstance()
        {
            if (!Application.isPlaying)
            {
                return new ResourceCache<TAsset>();
            }

            if (_cache != null) return _cache;
            _cache = new SingletonCache<TAsset>();
            _cache._isGlobal = true;
            Disposable.Create(() =>
            {
                _cache._isGlobal = false;
                _cache.Dispose();
                _cache = null;
            }).AddTo(GameWorld.Get().Cast());
            return _cache;
        }
        
        public override void Dispose()
        {
            /* Singleton should be managed by GameWorld */
            if (_isGlobal) return;
            base.Dispose();
        }
    }
}