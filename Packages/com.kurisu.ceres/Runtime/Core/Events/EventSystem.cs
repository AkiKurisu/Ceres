using UnityEngine;
namespace Ceres.Events
{
    public class EventSystem : MonoEventCoordinator
    {
#pragma warning disable IDE1006
        private sealed class _CallbackEventHandler : CallbackEventHandler, IBehaviourScope
#pragma warning restore IDE1006
        {
            public override bool IsCompositeRoot => true;

            private readonly EventSystem _eventCoordinator;

            public override IEventCoordinator Coordinator => _eventCoordinator;

            public MonoBehaviour Behaviour { get; }

            public _CallbackEventHandler(EventSystem eventCoordinator)
            {
                Behaviour = eventCoordinator;
                _eventCoordinator = eventCoordinator;
            }

            public override void SendEvent(EventBase e, DispatchMode dispatchMode = DispatchMode.Default)
            {
                e.Target = this;
                _eventCoordinator.Dispatch(e, dispatchMode, MonoDispatchType.Update);
            }

            public void SendMonoEvent(EventBase e, DispatchMode dispatchMode, MonoDispatchType monoDispatchType)
            {
                e.Target = this;
                _eventCoordinator.Dispatch(e, dispatchMode, monoDispatchType);
            }
        }

        private static EventSystem _instance;

        public static EventSystem Instance => _instance != null ? _instance : GetInstance();

        private CallbackEventHandler _eventHandler;

        /// <summary>
        /// Get event system <see cref="CallbackEventHandler"/>
        /// </summary>
        public static CallbackEventHandler EventHandler => Instance._eventHandler;

        private static EventSystem GetInstance()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return null;
#endif
            if (_instance == null)
            {
                GameObject managerObject = new() { name = nameof(EventSystem) };
                _instance = managerObject.AddComponent<EventSystem>();
                DontDestroyOnLoad(_instance);
            }
            return _instance;
        }

        protected override void Awake()
        {
            base.Awake();
            _eventHandler = new _CallbackEventHandler(this);
        }

        /// <summary>
        /// Adds an event handler to the instance. If the event handler has already been registered for the same phase (either TrickleDown or BubbleUp) then this method has no effect.
        /// </summary>
        /// <param name="callback">The event handler to add.</param>
        /// <param name="useTrickleDown"></param>
        public static void RegisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown) where TEventType : EventBase<TEventType>, new()
        {
            EventHandler.RegisterCallback(callback, useTrickleDown);
        }

        /// <summary>
        /// Remove callback from the instance.
        /// </summary>
        /// <param name="callback">The callback to remove. If this callback was never registered, nothing happens.</param>
        /// <param name="useTrickleDown">Set this parameter to true to remove the callback from the TrickleDown phase. Set this parameter false to remove the callback from the BubbleUp phase.</param>
        public static void UnregisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown) where TEventType : EventBase<TEventType>, new()
        {
            if (!_instance) return;
            EventHandler.UnregisterCallback(callback, useTrickleDown);
        }

        /// <summary>
        /// Sends an event to the event handler.
        /// </summary>
        /// <param name="eventBase">The event to send.</param>
        /// <param name="dispatchMode">The event dispatch mode.</param>
        /// <param name="monoDispatchType">Dispatch event on which <see cref="MonoBehaviour"/> tick frame。</param>
        public static void SendEvent(EventBase eventBase, DispatchMode dispatchMode = DispatchMode.Default, MonoDispatchType monoDispatchType = MonoDispatchType.Update)
        {
            ((_CallbackEventHandler)EventHandler).SendMonoEvent(eventBase, dispatchMode, monoDispatchType);
        }

        public sealed override CallbackEventHandler GetCallbackEventHandler()
        {
            return _eventHandler;
        }
    }
}