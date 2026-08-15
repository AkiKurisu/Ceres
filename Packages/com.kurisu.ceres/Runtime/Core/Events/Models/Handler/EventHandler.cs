namespace Ceres.Events
{
    /// <summary>
    /// Interface for classes capable of having callbacks to handle events.
    /// </summary>
    public abstract class CallbackEventHandler : IEventHandler
    {
        public virtual bool IsCompositeRoot => false;
        
        public abstract IEventCoordinator Coordinator { get; }
        
        /// <summary>
        /// Get and set parent callBack handler
        /// </summary>
        /// <value></value>
        public CallbackEventHandler Parent { get; set; }
        
        private EventCallbackRegistry _callbackRegistry;

        /// <summary>
        /// Adds an event handler to the instance. If the event handler has already been registered for the same phase (either TrickleDown or BubbleUp) then this method has no effect.
        /// </summary>
        /// <param name="callback">The event handler to add.</param>
        /// <param name="useTrickleDown"></param>
        public void RegisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown) where TEventType : EventBase<TEventType>, new()
        {
            _callbackRegistry ??= new EventCallbackRegistry();

            _callbackRegistry.RegisterCallback(callback, useTrickleDown, default);
#if UNITY_EDITOR
            GlobalCallbackRegistry.RegisterListeners<TEventType>(this, callback, useTrickleDown);
#endif
            // AddEventCategories<TEventType>();
        }

        //TODO: Encode event categories
        // private void AddEventCategories<TEventType>() where TEventType : EventBase<TEventType>, new()
        // {

        // }

        /// <summary>
        /// Adds an event handler to the instance. If the event handler has already been registered for the same phase then this method has no effect.
        /// </summary>
        /// <param name="callback">The event handler to add.</param>
        /// <param name="userArgs">Data to pass to the callback.</param>
        public void RegisterCallback<TEventType, TUserArgsType>(EventCallback<TEventType, TUserArgsType> callback, TUserArgsType userArgs, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown) where TEventType : EventBase<TEventType>, new()
        {
            _callbackRegistry ??= new EventCallbackRegistry();

            _callbackRegistry.RegisterCallback(callback, userArgs, useTrickleDown, default);
#if UNITY_EDITOR
            GlobalCallbackRegistry.RegisterListeners<TEventType>(this, callback, useTrickleDown);
#endif
            // AddEventCategories<TEventType>();
        }
        internal void RegisterCallback<TEventType>(EventCallback<TEventType> callback, InvokePolicy invokePolicy, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown) where TEventType : EventBase<TEventType>, new()
        {
            _callbackRegistry ??= new EventCallbackRegistry();

            _callbackRegistry.RegisterCallback(callback, useTrickleDown, invokePolicy);

#if UNITY_EDITOR
            GlobalCallbackRegistry.RegisterListeners<TEventType>(this, callback, useTrickleDown);
#endif

            // AddEventCategories<TEventType>();
        }

        /// <summary>
        /// Remove callback from the instance.
        /// </summary>
        /// <param name="callback">The callback to remove. If this callback was never registered, nothing happens.</param>
        /// <param name="useTrickleDown">Set this parameter to true to remove the callback from the TrickleDown phase. Set this parameter to false to remove the callback from the BubbleUp phase.</param>
        public void UnregisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown) where TEventType : EventBase<TEventType>, new()
        {
            _callbackRegistry?.UnregisterCallback(callback, useTrickleDown);

#if UNITY_EDITOR
            GlobalCallbackRegistry.UnregisterListeners<TEventType>(this, callback);
#endif
        }

        /// <summary>
        /// Remove callback from the instance.
        /// </summary>
        /// <param name="callback">The callback to remove. If this callback was never registered, nothing happens.</param>
        /// <param name="useTrickleDown">Set this parameter to true to remove the callback from the TrickleDown phase. Set this parameter to false to remove the callback from the BubbleUp phase.</param>
        public void UnregisterCallback<TEventType, TUserArgsType>(EventCallback<TEventType, TUserArgsType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown) where TEventType : EventBase<TEventType>, new()
        {
            _callbackRegistry?.UnregisterCallback(callback, useTrickleDown);

#if UNITY_EDITOR
            GlobalCallbackRegistry.UnregisterListeners<TEventType>(this, callback);
#endif
        }

        internal bool TryGetUserArgs<TEventType, TCallbackArgs>(EventCallback<TEventType, TCallbackArgs> callback, TrickleDown useTrickleDown, out TCallbackArgs userData) where TEventType : EventBase<TEventType>, new()
        {
            userData = default;

            if (_callbackRegistry != null)
            {
                return _callbackRegistry.TryGetUserArgs(callback, useTrickleDown, out userData);
            }

            return false;
        }

        /// <summary>
        /// Sends an event to the event handler.
        /// </summary>
        /// <param name="e">The event to send.</param>
        /// <param name="dispatchMode">The event dispatch mode.</param>
        public abstract void SendEvent(EventBase e, DispatchMode dispatchMode = DispatchMode.Default);

        public void HandleEventAtTargetPhase(EventBase evt)
        {
            evt.CurrentTarget = evt.Target;
            evt.PropagationPhase = PropagationPhase.AtTarget;
            HandleEventAtCurrentTargetAndPhase(evt);
            evt.PropagationPhase = PropagationPhase.DefaultActionAtTarget;
            HandleEventAtCurrentTargetAndPhase(evt);
        }

        public void HandleEventAtTargetAndDefaultPhase(EventBase evt)
        {
            HandleEventAtTargetPhase(evt);
            evt.PropagationPhase = PropagationPhase.DefaultAction;
            HandleEventAtCurrentTargetAndPhase(evt);
        }

        public void HandleEventAtCurrentTargetAndPhase(EventBase evt)
        {
            if (evt == null)
                return;

            switch (evt.PropagationPhase)
            {
                case PropagationPhase.TrickleDown:
                case PropagationPhase.BubbleUp:
                    {
                        if (!evt.IsPropagationStopped)
                        {
                            _callbackRegistry?.InvokeCallbacks(evt, evt.PropagationPhase);
                        }
                        break;
                    }
                case PropagationPhase.AtTarget:
                    {
                        //We make sure we invoke callbacks from the TrickleDownPhase before the BubbleUp ones when we are directly at target
                        if (!evt.IsPropagationStopped)
                        {
                            _callbackRegistry?.InvokeCallbacks(evt, PropagationPhase.TrickleDown);
                        }
                        if (!evt.IsPropagationStopped)
                        {
                            _callbackRegistry?.InvokeCallbacks(evt, PropagationPhase.BubbleUp);
                        }
                    }
                    break;
                case PropagationPhase.DefaultActionAtTarget:
                    {
                        if (!evt.IsDefaultPrevented)
                        {
                            using (new EventDebuggerLogExecuteDefaultAction(evt))
                            {
                                if (evt.SkipDisabledElements && this is IBehaviourScope bs && !bs.Behaviour.isActiveAndEnabled)
                                    ExecuteDefaultActionDisabledAtTarget(evt);
                                else
                                    ExecuteDefaultActionAtTarget(evt);
                            }
                        }
                        break;
                    }

                case PropagationPhase.DefaultAction:
                    {
                        if (!evt.IsDefaultPrevented)
                        {
                            using (new EventDebuggerLogExecuteDefaultAction(evt))
                            {
                                if (evt.SkipDisabledElements && this is IBehaviourScope bs && !bs.Behaviour.isActiveAndEnabled)
                                    ExecuteDefaultActionDisabled(evt);
                                else
                                    ExecuteDefaultAction(evt);
                            }
                        }
                        break;
                    }
            }
        }


        void IEventHandler.HandleEvent(EventBase evt)
        {
            HandleEventAtCurrentTargetAndPhase(evt);
        }

        /// <summary>
        /// Returns true if event handlers, for the event propagation TrickleDown phase, are attached to this object.
        /// </summary>
        /// <returns>True if object has event handlers for the TrickleDown phase.</returns>
        public bool HasTrickleDownHandlers()
        {
            return _callbackRegistry != null && _callbackRegistry.HasTrickleDownHandlers();
        }

        /// <summary>
        /// Return true if event handlers for the event propagation BubbleUp phase have been attached on this object.
        /// </summary>
        /// <returns>True if object has event handlers for the BubbleUp phase.</returns>
        public bool HasBubbleUpHandlers()
        {
            return _callbackRegistry != null && _callbackRegistry.HasBubbleHandlers();
        }
        /// <summary>
        /// Executes logic after the callbacks registered on the event target have executed,
        /// unless the event is marked to prevent its default behaviour.
        /// <see cref="EventBase.PreventDefault"/>.
        /// </summary>
        /// <param name="evt">The event instance.</param>
        protected virtual void ExecuteDefaultActionAtTarget(EventBase evt) { }

        /// <summary>
        /// Executes logic after the callbacks registered on the event target have executed,
        /// unless the event has been marked to prevent its default behaviour.
        /// <see cref="EventBase.PreventDefault"/>.
        /// </summary>
        /// <remarks>
        /// This method is designed to be overridden by subclasses. Use it to implement event handling without
        /// registering callbacks which guarantees precedences of callbacks registered by users of the subclass.
        /// Unlike <see cref="ExecuteDefaultActionAtTarget"/>, this method is called after the callbacks registered
        /// on the element
        /// </remarks>
        /// <param name="evt">The event instance.</param>
        protected virtual void ExecuteDefaultAction(EventBase evt) { }

        protected virtual void ExecuteDefaultActionDisabledAtTarget(EventBase evt) { }

        protected virtual void ExecuteDefaultActionDisabled(EventBase evt) { }
    }
}
