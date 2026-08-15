using System.Collections;
using System.Collections.Generic;

namespace Ceres.Tasks
{
    /// <summary>
    /// Sequence to composite tasks
    /// </summary>
    public class SequenceTask : PooledTaskBase<SequenceTask>, IEnumerable<TaskBase>
    {
        private readonly Queue<TaskBase> _tasks = new();
        
        private TaskBase _runningTask;
        
        public static SequenceTask GetPooled(TaskBase firstTask)
        {
            var task = GetPooled();
            task.Append(firstTask);
            return task;
        }
        
        public static SequenceTask GetPooled(IReadOnlyList<TaskBase> sequence)
        {
            var task = GetPooled();
            foreach (var tb in sequence)
                task.Append(tb);
            return task;
        }
        
        protected override void Reset()
        {
            base.Reset();
            _runningTask = null;
            Status = TaskStatus.Stopped;
            _tasks.Clear();
        }
        
        /// <summary>
        /// Append a task to the end of sequence
        /// </summary>
        /// <param name="task"></param>
        public SequenceTask Append(TaskBase task)
        {
            _tasks.Enqueue(task);
            task.Acquire();
            return this;
        }
        
        public SequenceTask AppendRange(IEnumerable<TaskBase> enumerable)
        {
            foreach (var task in enumerable)
                Append(task);
            return this;
        }

        public override void Tick()
        {
            while (true)
            {
                if (_runningTask == null)
                {
                    _tasks.TryPeek(out _runningTask);
                    _runningTask.Start();
                }

                if (_runningTask != null)
                {
                    _runningTask.Tick();
                    var status = _runningTask.GetStatus();
                    if (status is TaskStatus.Completed or TaskStatus.Stopped)
                    {
                        if (status == TaskStatus.Completed)
                        {
                            _runningTask.PostComplete();
                        }

                        _tasks.Dequeue().Dispose();
                        _runningTask = null;

                        if (_tasks.Count == 0)
                        {
                            CompleteTask();
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
                else
                {
                    CompleteTask();
                }

                break;
            }
        }

        public IEnumerator<TaskBase> GetEnumerator()
        {
            return _tasks.GetEnumerator();
        }
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _tasks.GetEnumerator();
        }
    }
}
