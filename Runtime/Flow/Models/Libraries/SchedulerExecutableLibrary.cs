using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Chris.Schedulers;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Provides scheduler timer and frame counter helpers for Flow graphs.
    /// </summary>
    [CeresGroup("Scheduler")]
    public partial class SchedulerExecutableLibrary: ExecutableFunctionLibrary
    {
        #region Scheduler

        /// <summary>
        /// Schedules a timer and invokes callbacks as it updates and completes.
        /// </summary>
        /// <param name="delaySeconds"></param>
        /// <param name="onComplete"></param>
        /// <param name="onUpdate"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <param name="ignoreTimeScale"></param>
        /// <returns></returns>
        [ExecutableFunction, CeresLabel("Schedule Timer By Event")]
        public static SchedulerHandle Flow_SchedulerDelay(
            float delaySeconds, EventDelegate onComplete, EventDelegate<float> onUpdate, 
            TickFrame tickFrame, bool isLooped, bool ignoreTimeScale)
        {
            var handle = Scheduler.Delay(delaySeconds,onComplete,onUpdate,
                tickFrame, isLooped, ignoreTimeScale);
            return handle;
        }
        
        /// <summary>
        /// Schedules a frame counter and invokes callbacks as it updates and completes.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="onComplete"></param>
        /// <param name="onUpdate"></param>
        /// <param name="tickFrame"></param>
        /// <param name="isLooped"></param>
        /// <returns></returns>
        [ExecutableFunction(SearchAliases = "Schedule FrameCounter By Event, FrameCounter"), CeresLabel("Schedule Frame Counter By Event")]
        public static SchedulerHandle Flow_SchedulerWaitFrame(
            int frame, EventDelegate onComplete, EventDelegate<int> onUpdate,
            TickFrame tickFrame, bool isLooped)
        {
            var handle = Scheduler.WaitFrame(frame, onComplete, onUpdate, tickFrame, isLooped);
            return handle;
        }
        
        /// <summary>
        /// Cancels a scheduled task if the handle is valid.
        /// </summary>
        /// <param name="handle"></param>
        [ExecutableFunction(IsScriptMethod = true), CeresLabel("Cancel Scheduler")]
        public static void Flow_SchedulerCancel(SchedulerHandle handle)
        {
            handle.Cancel();
        }
        
        #endregion Scheduler
    }
}
