using System.Threading;
using Cysharp.Threading.Tasks;
namespace Ceres.Schedulers
{
    public static class SchedulerExtensions
    {
        /// <summary>
        /// Async api for scheduler
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="disposeSchedulerWhenCancelled">Also dispose scheduler when cancellation happen</param>
        /// <returns></returns>
        public static UniTask WaitAsync(this SchedulerHandle handle, CancellationToken cancellationToken = default, bool disposeSchedulerWhenCancelled = true)
        {
            if (disposeSchedulerWhenCancelled)
                handle.AddTo(cancellationToken);
            return UniTask.WaitUntil(handle.IsDone, cancellationToken: cancellationToken);
        }
        
        internal static void Assign(this IScheduled scheduled, ref SchedulerHandle handle)
        {
            handle.Dispose();
            handle = scheduled.Handle;
        }
    }
}