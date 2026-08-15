using System;

namespace Ceres.Schedulers
{
    /// <summary>
    /// Interface for task can be scheduled
    /// </summary>
    internal interface IScheduled : IDisposable
    {
        /// <summary>
        /// Get handle of task
        /// </summary>
        /// <value></value>
        SchedulerHandle Handle { get; }
        
        /// <summary>
        /// Get whether the task has finished running for any reason.
        /// </summary>
        bool IsDone { get; }
        
        /// <summary>
        /// Whether the task is currently paused.
        /// </summary>
        bool IsPaused { get; }
        
        /// <summary>
        /// Update task
        /// </summary>
        void Update();
        
        /// <summary>
        /// Stop a task that is in-progress or paused. The task's on completion callback will not be called.
        /// </summary>
        void Cancel();
        
        /// <summary>
        /// Pause a running scheduler. A paused task can be resumed from the same point it was paused.
        /// </summary>
        void Pause();
        
        /// <summary>
        /// Continue a paused task. Does nothing if the task has not been paused.
        /// </summary>
        void Resume();
    }
}