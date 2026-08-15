#pragma warning disable CS9258
using System;
using System.Collections;
using System.Collections.Generic;
namespace Ceres.Collections
{
    /// <summary>
    /// Similar to TSparseArray in Unreal.
    /// A list where element indices aren't necessarily contiguous. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SparseArray<T> : IEnumerable<T>
    {
        private struct FreeListLink
        {
            public int Last;
            
            public int Next;
            
            public T Value;
        }
        
        private readonly List<FreeListLink> _data;
        
        private readonly List<bool> _allocationFlags;
        
        private int _firstFreeIndex;
        
        private int _numFreeIndices;
        
        private readonly int _capacity;
        
        public int FirstFreeIndex => _firstFreeIndex;
        
        public int NumFreeIndices => _numFreeIndices;
        
        public int InternalCapacity => _data.Capacity;
        
        public int Capacity => _capacity;
        
        public T this[int index]
        {
            get
            {
                if (!IsAllocated(index)) return default;
                return _data[index].Value;
            }
            set
            {
                if (!IsAllocated(index)) return;
                _data.AsSpan()[index].Value = value;
            }
        }
        
        public int Count => _data.Count - _numFreeIndices;
        
        public SparseArray(int length, int capacity)
        {
            _capacity = capacity;
            _data = new List<FreeListLink>(length);
            _allocationFlags = new List<bool>(length);
            for (int i = 0; i < length; ++i)
            {
                _data.Add(new FreeListLink
                {
                    Last = i - 1,
                    Value = default,
                    Next = i + 1 >= length ? -1 : i + 1
                });
                _allocationFlags.Add(false);
            }
            _firstFreeIndex = 0;
            _numFreeIndices = length;
        }
        
        public int Add(T element)
        {
            int index;
            if (_numFreeIndices > 0)
            {
                var span = _data.AsSpan();
                // update current
                ref var ptr = ref span[_firstFreeIndex];
                int next = ptr.Next;
                ptr.Value = element;
                ptr.Next = -1;
                
                // update next if exist
                if (next != -1)
                {
                    span[next].Last = -1;
                }
                
                // set flag
                _allocationFlags[_firstFreeIndex] = true;
                index = _firstFreeIndex;
                _firstFreeIndex = next;
                _numFreeIndices--;
            }
            else
            {
                index = _data.Count;
                if (_data.Count == _capacity)
                {
                    throw new ArgumentOutOfRangeException($"Sparse array should not exceed capacity {_capacity}!");
                }
                _data.Add(new FreeListLink
                {
                    Value = element,
                    Last = -1,
                    Next = -1
                });
                _allocationFlags.Add(true);
            }
            return index;
        }
        
        public int AddUninitialized()
        {
            return Add(default);
        }
        
        public void RemoveAt(int index)
        {
            var span = _data.AsSpan();
            ref var link = ref span[index];
            link.Value = default;
            link.Last = -1;
            _allocationFlags[index] = false;
            if (_firstFreeIndex == -1)
            {
                // as link list header
                link.Next = -1;
            }
            else
            {
                // link to header
                span[_firstFreeIndex].Last = index;
                link.Next = _firstFreeIndex;
            }
            // update removed link
            _firstFreeIndex = index;
            _numFreeIndices++;
        }
        
        public void Clear()
        {
            _numFreeIndices = _data.Count;
            for (int i = 0; i < _numFreeIndices; ++i)
            {
                _data[i] = new FreeListLink
                {
                    Last = i - 1,
                    Next = i + 1 >= _numFreeIndices ? -1 : i + 1
                };
                _allocationFlags[i] = false;
            }
            _firstFreeIndex = 0;
        }
        
        public void Shrink()
        {
            int firstIndexToRemove = _allocationFlags.LastIndexOf(true) + 1;
            int count = _data.Count;
            if (firstIndexToRemove < count)
            {
                if (NumFreeIndices > 0)
                {
                    var span = _data.AsSpan();
                    int freeIndex = FirstFreeIndex;
                    while (freeIndex != -1)
                    {
                        if (freeIndex >= firstIndexToRemove)
                        {
                            int prevFreeIndex = span[freeIndex].Last;
                            int nextFreeIndex = span[freeIndex].Next;
                            if (nextFreeIndex != -1)
                            {
                                span[nextFreeIndex].Last = prevFreeIndex;
                            }
                            if (prevFreeIndex != -1)
                            {
                                span[prevFreeIndex].Next = nextFreeIndex;
                            }
                            else
                            {
                                _firstFreeIndex = nextFreeIndex;
                            }
                            --_numFreeIndices;

                            freeIndex = nextFreeIndex;
                        }
                        else
                        {
                            freeIndex = span[freeIndex].Next;
                        }
                    }
                }
                _data.RemoveRange(firstIndexToRemove, count - firstIndexToRemove);
                _allocationFlags.RemoveRange(firstIndexToRemove, count - firstIndexToRemove);
            }
            // shrink list
            _data.Capacity = _allocationFlags.Capacity = _data.Count;
        }
        
        public bool IsAllocated(int index)
        {
            if (index < 0 || index >= _allocationFlags.Count) return false;
            return _allocationFlags[index];
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }
        
        private struct Enumerator : IEnumerator<T>
        {
            private SparseArray<T> _sparseArray;
            
            private int _currentIndex;

            public Enumerator(SparseArray<T> array)
            {
                _sparseArray = array;
                _currentIndex = -1;
            }

            public readonly T Current => _sparseArray._data[_currentIndex].Value;

            readonly object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                _currentIndex++;
                while (_currentIndex < _sparseArray._data.Count)
                {
                    if (_sparseArray._allocationFlags[_currentIndex])
                    {
                        return true;
                    }
                    _currentIndex++;
                }
                return false;
            }

            public void Reset()
            {
                _currentIndex = -1;
            }

            public void Dispose()
            {
                _sparseArray = null;
            }
        }
    }
}
#pragma warning restore CS9258