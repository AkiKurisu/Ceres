using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ceres.Pool
{
#pragma warning disable IDE1006
    /// <summary>
    /// Internal simple object pool
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class _ObjectPool<T> where T : new()
#pragma warning restore IDE1006 
    {
        private readonly Stack<T> _stack = new();

        private int _maxSize;

        internal Func<T> CreateFunc;

        public int MaxSize
        {
            get => _maxSize;
            set
            {
                _maxSize = Math.Max(0, value);
                while (Size() > _maxSize)
                {
                    Get();
                }
            }
        }

        public _ObjectPool(Func<T> createFunc, int maxSize = 5000)
        {
            MaxSize = maxSize;

            if (createFunc == null)
            {
                CreateFunc = () => new T();
            }
            else
            {
                CreateFunc = createFunc;
            }
        }

        public int Size()
        {
            return _stack.Count;
        }

        public void Clear()
        {
            _stack.Clear();
        }

        public T Get()
        {
            T evt = _stack.Count == 0 ? CreateFunc() : _stack.Pop();
            return evt;
        }

        public void Release(T element)
        {
#if UNITY_DEBUG
            if (m_Stack.Contains(element)) //this is O(n) and will be come an issue when the pool size is large
#else
            if (_stack.Count > 0 && ReferenceEquals(_stack.Peek(), element))
#endif
                Debug.LogError("Internal error. Trying to destroy object that is already released to pool.");

            if (_stack.Count < MaxSize)
            {
                _stack.Push(element);
            }
#if UNITY_EDITOR
            else
                Debug.LogWarning("Internal error. Pool is already full, try to increase max size.");
#endif
        }
    }
}