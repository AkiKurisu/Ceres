using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Chris.Schedulers;
using UnityEngine.Scripting;
namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Executable function library for Chris.Schedulers
    /// </summary>
    [Preserve]
    [CeresGroup("Scheduler")]
    public partial class SchedulerExecutableLibrary: ExecutableFunctionLibrary
    {
        #region Scheduler

        [ExecutableFunction, CeresLabel("Schedule Timer by Event")]
        public static SchedulerHandle Flow_SchedulerDelay(
            float delaySeconds, EventDelegate onComplete, EventDelegate<float> onUpdate, 
            TickFrame tickFrame, bool isLooped, bool ignoreTimeScale)
        {
            var handle = Scheduler.Delay(delaySeconds,onComplete,onUpdate,
                tickFrame, isLooped, ignoreTimeScale);
            return handle;
        }
        
        [ExecutableFunction, CeresLabel("Schedule FrameCounter by Event")]
        public static SchedulerHandle Flow_SchedulerWaitFrame(
            int frame, EventDelegate onComplete, EventDelegate<int> onUpdate,
            TickFrame tickFrame, bool isLooped)
        {
            var handle = Scheduler.WaitFrame(frame, onComplete, onUpdate, tickFrame, isLooped);
            return handle;
        }
        
        [ExecutableFunction, CeresLabel("Cancel Scheduler")]
        public static void Flow_SchedulerCancel(SchedulerHandle handle)
        {
            handle.Cancel();
        }
        
        #endregion Scheduler
    }
}