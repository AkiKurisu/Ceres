using System;

namespace R3.Ceres
{
    /// <summary>
    /// Unregister scope interface for managing <see cref="IDisposable"/> 
    /// </summary>
    public interface IDisposableUnregister
    {
        /// <summary>
        /// Register new disposable to this unregister scope
        /// </summary>
        /// <param name="disposable"></param>
        void Register(IDisposable disposable);
    }

    public static class DisposableExtensions
    {
        public static T AddTo<T>(this T disposable, IDisposableUnregister unRegister) where T : IDisposable
        {
            unRegister.Register(disposable);
            return disposable;
        }
    }
}
