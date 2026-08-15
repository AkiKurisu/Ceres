using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Random = System.Random;

namespace Ceres.Collections
{
    public class RandomList<T>: IReadOnlyList<T>
    {
        private readonly List<T> _items;
        
        private readonly List<double> _weights;
        
        private readonly Random _random;
        
        private T _lastSelected;
        
        public int Count => _items.Count;
        
        public T this[int index] => _items[index];
        
        public RandomList(int capacity)
        {
            _items = new List<T>(capacity);
            _weights = new List<double>(capacity);
            _random = new Random();
        }
        
        public RandomList()
        {
            _items = new List<T>();
            _weights = new List<double>();
            _random = new Random();
        }
        
        public void Add(T item, double weight = 1)
        {
            _items.Add(item);
            _weights.Add(weight);
        }

        public T GetNext(double decayFactor = 0.9)
        {
            while (true)
            {
                double totalWeight = _weights.Sum() - (_lastSelected != null ? _weights[_items.IndexOf(_lastSelected)] : 0);
                double randomNumber = _random.NextDouble() * totalWeight;
                double cumulativeWeight = 0;
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].Equals(_lastSelected))
                    {
                        // Skip the last selected item
                        continue;
                    }

                    cumulativeWeight += _weights[i];
                    if (randomNumber < cumulativeWeight)
                    {
                        T selected = _items[i];
                        // Decrease the weight of the selected item for future selections
                        _weights[i] *= decayFactor;
                        // Update the last selected item
                        return _lastSelected = selected;
                    }
                }

                // If all items are the last selected item, reset the lastSelected to default
                _lastSelected = default;
                // Perform the selection again
            }
        }
        
        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}