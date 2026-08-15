using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ceres.Pool
{
    /// <summary>
    /// Interface for pooled object metadata
    /// </summary>
    public interface IPooledMetadata { }
    
    /// <summary>
    /// Struct based pool key without allocation
    /// </summary>
    public readonly struct PoolKey
    {
        private readonly string _key;
        
        private readonly string _subKey;
        
        private readonly int _id;
        
        public PoolKey(string key)
        {
            _key = key;
            _subKey = null;
            _id = 0;
        }
        
        public PoolKey(string key, string subKey)
        {
            _key = key;
            _subKey = subKey;
            _id = 0;
        }
        
        public PoolKey(string key, int id)
        {
            _key = key;
            _subKey = null;
            _id = id;
        }
        
        public bool IsNull()
        {
            if (string.IsNullOrEmpty(_key)) return true;
            bool isNull = true;
            isNull &= !string.IsNullOrEmpty(_subKey);
            isNull &= _id != 0;
            return isNull;
        }
        
        public override string ToString()
        {
            if (IsNull()) return string.Empty;
            if (string.IsNullOrEmpty(_subKey))
            {
                if (_id != 0)
                    return $"{_key} {_id}";
                return _key;
            }
            if (_id != 0)
                return $"{_key} {_subKey} {_id}";
            return $"{_key} {_subKey}";
        }
        
        public class Comparer : IEqualityComparer<PoolKey>
        {
            public bool Equals(PoolKey x, PoolKey y)
            {
                return x._id == y._id && x._key == y._key && x._subKey == y._subKey;
            }

            public int GetHashCode(PoolKey key)
            {
                return HashCode.Combine(key._id, key._key, key._subKey);
            }
        }
    }
    
    public sealed class GameObjectPoolManager : MonoBehaviour
    {
        internal static GameObjectPoolManager Instance
        {
            get
            {
                if (!_instance)
                {
                    GameObject managerObject = new()
                    {
                        name = nameof(GameObjectPoolManager),
                        hideFlags = HideFlags.DontSave | HideFlags.NotEditable
                    };
                    _instance = managerObject.AddComponent<GameObjectPoolManager>();
                }
                return _instance;
            }
        }
        public static bool IsInstantiated => _instance;
        
        private static GameObjectPoolManager _instance;
        
        private readonly Dictionary<PoolKey, GameObjectPool> _poolDic = new(new PoolKey.Comparer());
        
        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            LocalReleaseAll();
        }
        
        /// <summary>
        /// Get pooled gameObject
        /// </summary>
        /// <param name="address"></param>
        /// <param name="pooledMetadata"></param>
        /// <param name="parent"></param>
        /// <param name="createEmptyIfNotExist"></param>
        /// <returns></returns>
        public static GameObject Get(PoolKey address, out IPooledMetadata pooledMetadata, 
            Transform parent = null, bool createEmptyIfNotExist = true)
        {
            GameObject obj = null;
            pooledMetadata = null;
            if (Instance._poolDic.TryGetValue(address, out var poolData) && poolData.PoolQueue.Count > 0)
            {
                obj = poolData.GetObj(parent, out pooledMetadata);
            }
            else if (createEmptyIfNotExist)
            {
                obj = new GameObject(address.ToString());
                obj.transform.SetParent(parent);
            }
            return obj;
        }
        
        /// <summary>
        /// Release gameObject to pool
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="address"></param>
        /// <param name="pooledMetadata"></param>
        public static void Release(GameObject obj, PoolKey address = default, IPooledMetadata pooledMetadata = null)
        {
            if (address.IsNull())
                address = new PoolKey(obj.name);
            if (!Instance._poolDic.TryGetValue(address, out GameObjectPool poolData))
            {
                poolData = Instance._poolDic[address] = new GameObjectPool(Instance.transform);
            }
            poolData.PushObj(obj, pooledMetadata);
        }
        
        /// <summary>
        /// Release addressable gameObject pool
        /// </summary>
        /// <param name="address"></param>
        public static void ReleasePool(PoolKey address)
        {
            if (Instance._poolDic.TryGetValue(address, out var pool))
            {
                pool.Release();
                Instance._poolDic.Remove(address);
            }
        }
        
        private void LocalReleaseAll()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            _poolDic.Clear();
        }
        
        /// <summary>
        /// Release all pooled gameObjects
        /// </summary>
        public static void ReleaseAll()
        {
            Instance.LocalReleaseAll();
        }
        
        private class GameObjectPool
        {
            private readonly Transform _root;
            
            public readonly Queue<GameObject> PoolQueue = new();
            
            private readonly Dictionary<GameObject, IPooledMetadata> _metaData = new();
            
            public GameObjectPool(Transform poolRoot)
            {
                _root = poolRoot;
            }
            
            public void PushObj(GameObject obj, IPooledMetadata pooledMetadata)
            {
                PoolQueue.Enqueue(obj);
                _metaData[obj] = pooledMetadata;
                if (_root != obj.transform.parent)
                {
                    obj.transform.SetParent(_root);
                }
                obj.SetActive(false);
            }
            
            public GameObject GetObj(Transform parent, out IPooledMetadata pooledMetadata)
            {
                var obj = PoolQueue.Dequeue();
                _metaData.Remove(obj, out pooledMetadata);
                obj.SetActive(true);
                if (parent != obj.transform.parent)
                {
                    obj.transform.SetParent(parent);
                }
                if (!parent)
                {
                    SceneManager.MoveGameObjectToScene(obj, SceneManager.GetActiveScene());
                }
                return obj;
            }

            public void Release()
            {
                while (PoolQueue.TryDequeue(out var instance))
                {
                    Destroy(instance);
                }
                PoolQueue.Clear();
            }
        }
    }
}