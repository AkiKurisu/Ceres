using System.Collections.Generic;
namespace Ceres.Events
{
    /// <summary>
    /// Implement to batch event
    /// </summary>
    public interface IBatchEvent
    {
        EventBase CreateBatchedEvent();
        
        void Batch(EventBase eventBase);
    }
    
    public class BatchEventDispatchingStrategy : IEventDispatchingStrategy
    {
        public bool CanDispatchEvent(EventBase evt)
        {
            return evt is IBatchEvent;
        }
        
        public void DispatchEvent(EventBase evt, IEventCoordinator coordinator)
        {
            var listener = coordinator.EventDispatcher.GetEventDispatchingListener<BatchEventDispatchingListener>();
            // Is event already batched, skip
            if (listener.ContainsEvent(evt)) return;
            evt.StopPropagation();
            evt.PreventDefault();
            evt.StopDispatch = true;
            listener.RegisterBatchEvent(evt);
        }
    }
    
    public class BatchEventDispatchingListener : IEventDispatchingListener
    {
        public BatchEventDispatchingListener(IEventCoordinator coordinator, DispatchMode dispatchMode)
        {
            _coordinator = coordinator;
            _dispatchMode = dispatchMode;
        }
        
        private readonly IEventCoordinator _coordinator;
        
        private readonly Dictionary<long, EventBase> _batchEventMap = new();
        
        private readonly HashSet<long> _dispatchedIds = new();

        private readonly DispatchMode _dispatchMode;
        
        public virtual void OnPopDispatcherContext()
        {
            _dispatchedIds.Clear();
        }
        
        public virtual void OnPushDispatcherContext()
        {
            foreach (var pair in _batchEventMap)
            {
                _dispatchedIds.Add(pair.Key);
                _coordinator.EventDispatcher.Dispatch(pair.Value, _coordinator, _dispatchMode);
                pair.Value.Dispose();
            }
            _batchEventMap.Clear();
        }
        
        internal bool ContainsEvent(EventBase eventBase)
        {
            return _dispatchedIds.Contains(eventBase.EventTypeId);
        }
        
        internal void RegisterBatchEvent(EventBase evt)
        {
            if (_batchEventMap.TryGetValue(evt.EventTypeId, out var batchedEvent))
            {
                ((IBatchEvent)batchedEvent).Batch(evt);
                return;
            }
            batchedEvent = ((IBatchEvent)evt).CreateBatchedEvent();
            batchedEvent.Acquire();
            _batchEventMap.Add(evt.EventTypeId, batchedEvent);
        }
    }
    
}