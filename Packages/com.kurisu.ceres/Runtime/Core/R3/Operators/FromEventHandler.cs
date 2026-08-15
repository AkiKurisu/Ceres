using System;
using System.Threading;
using Ceres.Events;

namespace R3.Ceres
{
    internal sealed class FromEventHandler<T> : Observable<T> where T : EventBase<T>, new()
    {
        private readonly Func<Action<T>, EventCallback<T>> conversion;
        private readonly Action<EventCallback<T>> addHandler;
        private readonly Action<EventCallback<T>> removeHandler;
        private readonly CancellationToken cancellationToken;
        
        public FromEventHandler(Func<Action<T>, EventCallback<T>> conversion, Action<EventCallback<T>> addHandler, Action<EventCallback<T>> removeHandler, CancellationToken cancellationToken)
        {
            this.conversion = conversion; ;
            this.addHandler = addHandler;
            this.removeHandler = removeHandler;
            this.cancellationToken = cancellationToken;
        }
        
        protected override IDisposable SubscribeCore(Observer<T> observer)
        {
            return new FromEventHandlerPattern(conversion, addHandler, removeHandler, observer, cancellationToken);
        }
        
#nullable enable
        sealed class FromEventHandlerPattern : IDisposable
        {
            private Observer<T>? observer;
            private Action<EventCallback<T>>? removeHandler;
            private readonly EventCallback<T> registeredHandler;
            private CancellationTokenRegistration cancellationTokenRegistration;
            public FromEventHandlerPattern(Func<Action<T>, EventCallback<T>> conversion, Action<EventCallback<T>> addHandler, Action<EventCallback<T>> removeHandler, Observer<T> observer, CancellationToken cancellationToken)
            {
                this.observer = observer;
                this.removeHandler = removeHandler;
                registeredHandler = conversion(OnNext);
                addHandler(registeredHandler);

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationTokenRegistration = cancellationToken.Register(static state =>
                    {
                        var s = (FromEventHandlerPattern)state!;
                        s.CompleteDispose();
                    }, this, false);
                }
            }
            
            private void OnNext(T value)
            {
                //Prevent eventBase pooled => needs manually call dispose once
                value.Acquire();
                observer?.OnNext(value);
            }

            private void CompleteDispose()
            {
                observer?.OnCompleted();
                Dispose();
            }

            public void Dispose()
            {
                var handler = Interlocked.Exchange(ref removeHandler, null);
                if (handler != null)
                {
                    observer = null;
                    removeHandler = null;
                    cancellationTokenRegistration.Dispose();
                    handler(registeredHandler);
                }
            }
        }
    }
#nullable disable
}