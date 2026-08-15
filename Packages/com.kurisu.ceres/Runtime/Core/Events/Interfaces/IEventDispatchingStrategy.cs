using UnityEngine;

namespace Ceres.Events
{
    // determines in which event phase an event handler wants to handle events
    // the handler always gets called if it is the target CallBackHandler
    /// <summary>
    /// The propagation phases of an event.
    /// </summary>
    /// <remarks>
    /// >When an element receives an event, the event propagates from the panel's root element to the target element.
    ///
    /// In the TrickleDown phase, the event is sent from the panel's root element to the target element's parent.
    ///
    /// In the AtTarget phase, the event is sent to the target element.
    ///
    /// In the BubbleUp phase, the event is sent from the target element's parent back to the panel's root element.
    ///
    /// In the last phase, the DefaultAction phase, the event is resent to the target element.
    /// </remarks>
    public enum PropagationPhase
    {
        // Not propagating at the moment.
        /// <summary>
        /// The event is not propagated.
        /// </summary>
        None = 0,

        // Propagation from root of tree to immediate parent of target.
        /// <summary>
        /// The event is sent from the panel's root element to the target element's parent.
        /// </summary>
        TrickleDown = 1,

        // Event is at target.
        /// <summary>
        /// The event is sent to the target.
        /// </summary>
        AtTarget = 2,

        // Execute the default action(s) at target.
        /// <summary>
        /// The event is sent to the target element, which can then execute its default actions for the event at the target phase. Event handlers do not receive the event in this phase. Instead, ExecuteDefaultActionAtTarget is called on the target element.
        /// </summary>
        DefaultActionAtTarget = 5,

        // After the target has gotten the chance to handle the event, the event walks back up the parent hierarchy back to root.
        /// <summary>
        /// The event is sent from the target element's parent back to the panel's root element.
        /// </summary>
        BubbleUp = 3,

        // At last, execute the default action(s).
        /// <summary>
        /// The event is sent to the target element, which can then execute its final default actions for the event. Event handlers do not receive the event in this phase. Instead, ExecuteDefaultAction is called on the target element.
        /// </summary>
        DefaultAction = 4
    }
    public interface IEventDispatchingStrategy
    {
        bool CanDispatchEvent(EventBase evt);
        void DispatchEvent(EventBase evt, IEventCoordinator coordinator);
    }
    public interface IEventDispatchingListener
    {
        void OnPushDispatcherContext();
        void OnPopDispatcherContext();
    }
    internal static class EventDispatchUtilities
    {
        public static void PropagateEvent(EventBase evt)
        {
            // If there is no target or it's somehow not a CallbackEventHandler, we assume the event handling is empty work.
            if (evt.Target is not CallbackEventHandler ve)
                return;

            Debug.Assert(!evt.Dispatch, "Event is being dispatched recursively.");
            evt.Dispatch = true;

            if (!evt.BubblesOrTricklesDown)
            {
                ve.HandleEventAtTargetPhase(evt);
            }
            else
            {
                HandleEventAcrossPropagationPath(evt);
            }

            evt.Dispatch = false;
        }

        private static void HandleEventAcrossPropagationPath(EventBase evt)
        {
            // Build and store propagation path
            var leafTarget = (CallbackEventHandler)evt.LeafTarget;
            var path = PropagationPaths.Build(leafTarget, evt);
            evt.Path = path;
            EventDebugger.LogPropagationPaths(evt, path);

            var coordinator = leafTarget.Coordinator;

            // Phase 1: TrickleDown phase
            // Propagate event from root to target.parent
            if (evt.TricklesDown)
            {
                evt.PropagationPhase = PropagationPhase.TrickleDown;

                for (int i = path.TrickleDownPath.Count - 1; i >= 0; i--)
                {
                    if (evt.IsPropagationStopped)
                        break;

                    var element = path.TrickleDownPath[i];
                    if (evt.Skip(element) || element.Coordinator != coordinator)
                    {
                        continue;
                    }

                    evt.CurrentTarget = element;
                    evt.CurrentTarget.HandleEvent(evt);
                }
            }

            // Phase 2: Target / DefaultActionAtTarget
            // Propagate event from target parent up to root for the target phase

            // Call HandleEvent() even if propagation is stopped, for the default actions at target.
            evt.PropagationPhase = PropagationPhase.AtTarget;
            foreach (var element in path.TargetElements)
            {
                if (evt.Skip(element) || element.Coordinator != coordinator)
                {
                    continue;
                }

                evt.Target = element;
                evt.CurrentTarget = evt.Target;
                evt.CurrentTarget.HandleEvent(evt);
            }

            // Call ExecuteDefaultActionAtTarget
            evt.PropagationPhase = PropagationPhase.DefaultActionAtTarget;
            foreach (var element in path.TargetElements)
            {
                if (evt.Skip(element) || element.Coordinator != coordinator)
                {
                    continue;
                }

                evt.Target = element;
                evt.CurrentTarget = evt.Target;
                evt.CurrentTarget.HandleEvent(evt);
            }

            // Reset target to original target
            evt.Target = evt.LeafTarget;

            // Phase 3: bubble up phase
            // Propagate event from target parent up to root
            if (evt.Bubbles)
            {
                evt.PropagationPhase = PropagationPhase.BubbleUp;

                foreach (var element in path.BubbleUpPath)
                {
                    if (evt.Skip(element) || element.Coordinator != coordinator)
                    {
                        continue;
                    }

                    evt.CurrentTarget = element;
                    evt.CurrentTarget.HandleEvent(evt);
                }
            }

            evt.PropagationPhase = PropagationPhase.None;
            evt.CurrentTarget = null;
        }


        public static void ExecuteDefaultAction(EventBase evt)
        {
            if (evt.Target is CallbackEventHandler)
            {
                evt.Dispatch = true;
                evt.CurrentTarget = evt.Target;
                evt.PropagationPhase = PropagationPhase.DefaultAction;

                evt.CurrentTarget.HandleEvent(evt);

                evt.PropagationPhase = PropagationPhase.None;
                evt.CurrentTarget = null;
                evt.Dispatch = false;
            }
        }
    }
}