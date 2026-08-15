using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.Assertions;
using UnityEngine.Pool;

namespace Ceres.Pool
{
    public class UniParallel : List<UniTask>, IDisposable
    {
        private static readonly ObjectPool<UniParallel> Pool = new(() => new UniParallel(), null, e => e.Clear());
        
        public static UniParallel Get()
        {
            return Pool.Get();
        }
        
        public static UniParallel Create(UniTask task1, UniTask task2)
        {
            var task = Get();
            task.Add(task1);
            task.Add(task2);
            return task;
        }
        
        public static UniParallel Create(UniTask task1, UniTask task2, UniTask task3)
        {
            var task = Get();
            task.Add(task1);
            task.Add(task2);
            task.Add(task3);
            return task;
        }
        
        public static UniParallel Create(UniTask task1, UniTask task2, UniTask task3, UniTask task4)
        {
            var task = Get();
            task.Add(task1);
            task.Add(task2);
            task.Add(task3);
            task.Add(task4);
            return task;
        }
        
        public static UniParallel Create(IEnumerable<UniTask> uniTasks)
        {
            var task = Get();
            task.AddRange(uniTasks);
            return task;
        }
        
        public void Dispose()
        {
            Pool.Release(this);
        }
        
        public UniTask.Awaiter GetAwaiter()
        {
            return UniTask.WhenAll(this).GetAwaiter();
        }
        
        public void Forget()
        {
            UniTask.WhenAll(this).Forget();
        }
    }
    
    public class UniParallel<T> : List<UniTask<T>>, IDisposable
    {
        private static readonly ObjectPool<UniParallel<T>> Pool = new(() => new UniParallel<T>(), null, e => e.Clear());
        
        public static UniParallel<T> Get()
        {
            return Pool.Get();
        }
        
        public static UniParallel<T> Create(UniTask<T> task1, UniTask<T> task2)
        {
            var task = Get();
            task.Add(task1);
            task.Add(task2);
            return task;
        }
        
        public static UniParallel<T> Create(UniTask<T> task1, UniTask<T> task2, UniTask<T> task3)
        {
            var task = Get();
            task.Add(task1);
            task.Add(task2);
            task.Add(task3);
            return task;
        }
        
        public static UniParallel<T> Create(UniTask<T> task1, UniTask<T> task2, UniTask<T> task3, UniTask<T> task4)
        {
            var task = Get();
            task.Add(task1);
            task.Add(task2);
            task.Add(task3);
            task.Add(task4);
            return task;
        }
        
        public static UniParallel<T> Create(IEnumerable<UniTask<T>> uniTasks)
        {
            var task = Get();
            task.AddRange(uniTasks);
            return task;
        }
        
        public void Dispose()
        {
            Pool.Release(this);
        }
        
        public UniTask<T[]>.Awaiter GetAwaiter()
        {
            return UniTask.WhenAll(this).GetAwaiter();
        }
        
        public void Forget()
        {
            UniTask.WhenAll(this).Forget();
        }
    }
    
    public class UniSequence : List<UniTask>, IDisposable
    {
        private static readonly ObjectPool<UniSequence> Pool = new(() => new UniSequence(), null, e => e.Clear());
        
        public static UniSequence Get()
        {
            return Pool.Get();
        }
        
        public static UniSequence Create(UniTask task1, UniTask task2)
        {
            var task = Get();
            task.Add(task1);
            task.Add(task2);
            return task;
        }
        
        public static UniSequence Create(UniTask task1, UniTask task2, UniTask task3)
        {
            var task = Get();
            task.Add(task1);
            task.Add(task2);
            task.Add(task3);
            return task;
        }
        
        public static UniSequence Create(UniTask task1, UniTask task2, UniTask task3, UniTask task4)
        {
            var task = Get();
            task.Add(task1);
            task.Add(task2);
            task.Add(task3);
            task.Add(task4);
            return task;
        }
        
        public static UniSequence Create(IEnumerable<UniTask> uniTasks)
        {
            var task = Get();
            task.AddRange(uniTasks);
            return task;
        }
        
        public void Dispose()
        {
            Pool.Release(this);
        }
        
        public UniTask.Awaiter GetAwaiter()
        {
            return AwaitAsSequence().GetAwaiter();
        }
        
        private async UniTask AwaitAsSequence()
        {
            foreach (var task in this)
            {
                await task;
            };
        }
        
        public void Forget()
        {
            AwaitAsSequence().Forget();
        }
    }
    
    public class SequenceTask<T> : List<UniTask<T>>, IDisposable
    {
        private static readonly ObjectPool<SequenceTask<T>> Pool = new(() => new SequenceTask<T>(), null, e => { e.Clear(); e._results = null; });
        
        private T[] _results;
        
        public static SequenceTask<T> Get()
        {
            return Pool.Get();
        }
        
        public static SequenceTask<T> Create(UniTask<T> task1, UniTask<T> task2)
        {
            var task = Get();
            task.Add(task1);
            task.Add(task2);
            return task;
        }
        
        public static SequenceTask<T> Create(UniTask<T> task1, UniTask<T> task2, UniTask<T> task3)
        {
            var task = Get();
            task.Add(task1);
            task.Add(task2);
            task.Add(task3);
            return task;
        }
        
        public static SequenceTask<T> Create(UniTask<T> task1, UniTask<T> task2, UniTask<T> task3, UniTask<T> task4)
        {
            var task = Get();
            task.Add(task1);
            task.Add(task2);
            task.Add(task3);
            task.Add(task4);
            return task;
        }
        
        public static SequenceTask<T> Create(IEnumerable<UniTask<T>> uniTasks)
        {
            var task = Get();
            task.AddRange(uniTasks);
            return task;
        }
        
        public static SequenceTask<T> GetNonAlloc(T[] results)
        {
            var task = Pool.Get();
            task._results = results;
            return task;
        }
        
        public void Dispose()
        {
            Pool.Release(this);
        }
        
        public UniTask<T[]>.Awaiter GetAwaiter()
        {
            return AwaitAsSequence().GetAwaiter();
        }
        
        private async UniTask<T[]> AwaitAsSequence()
        {
            _results ??= new T[Count];
            Assert.IsTrue(_results.Length >= Count);
            for (int i = 0; i < Count; ++i)
            {
                _results[i] = await this[i];
            };
            return _results;
        }
        
        public void Forget()
        {
            AwaitAsSequence().Forget();
        }
    }
}