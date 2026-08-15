using Ceres.Schedulers;

namespace Ceres.Tasks
{
    /// <summary>
    /// Represent a delay task use scheduler so that can be tracked
    /// </summary>
    public class DelayTask : PooledTaskBase<DelayTask>
    {
        private SchedulerHandle _handle;
        
        [StackTraceFrame]
        public static unsafe DelayTask GetPooled(float delay)
        {
            var task = GetPooled();
            task._handle = Scheduler.DelayUnsafe(delay, new SchedulerUnsafeBinding(task, &StopDelayTask));
            return task;
        }
        
        protected override void Init()
        {
            base.Init();
            _handle = default;
        }
        
        protected override void Reset()
        {
            base.Reset();
            _handle.Dispose();
            _handle = default;
        }
        
        private static void StopDelayTask(object instance)
        {
            ((DelayTask)instance).Status = TaskStatus.Completed;
        }
    }
}
