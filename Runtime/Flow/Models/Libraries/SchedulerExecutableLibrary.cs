using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Chris.Schedulers;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Executable function library for Chris.Schedulers
    /// </summary>
    [CeresGroup("Scheduler")]
    public partial class SchedulerExecutableLibrary: ExecutableFunctionLibrary
    {
        #region Scheduler

        /// <summary>
        /// Delay some time and invoke callBack.
        /// </summary>
        /// <param name="delaySeconds"></param>
        /// <param name="onComplete"></param>
        /// <param name="onUpdate"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <param name="ignoreTimeScale"></param>
        /// <returns></returns>
        [ExecutableFunction, CeresLabel("Schedule Timer by Event")]
        public static SchedulerHandle Flow_SchedulerDelay(
            float delaySeconds, EventDelegate onComplete, EventDelegate<float> onUpdate, 
            TickFrame tickFrame, bool isLooped, bool ignoreTimeScale)
        {
            var handle = Scheduler.Delay(delaySeconds,onComplete,onUpdate,
                tickFrame, isLooped, ignoreTimeScale);
            return handle;
        }
        
        /// <summary>
        /// Wait some frames and invoke callBack.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="onComplete"></param>
        /// <param name="onUpdate"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <returns></returns>
        [ExecutableFunction, CeresLabel("Schedule FrameCounter by Event")]
        public static SchedulerHandle Flow_SchedulerWaitFrame(
            int frame, EventDelegate onComplete, EventDelegate<int> onUpdate,
            TickFrame tickFrame, bool isLooped)
        {
            var handle = Scheduler.WaitFrame(frame, onComplete, onUpdate, tickFrame, isLooped);
            return handle;
        }
        
        /// <summary>
        /// Cancel a scheduled task if is valid.
        /// </summary>
        /// <param name="handle"></param>
        [ExecutableFunction, CeresLabel("Cancel Scheduler")]
        public static void Flow_SchedulerCancel(SchedulerHandle handle)
        {
            handle.Cancel();
        }
        
        #endregion Scheduler
    }
}