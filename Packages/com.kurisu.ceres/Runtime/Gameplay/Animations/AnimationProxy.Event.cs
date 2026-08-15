using System.Collections.Generic;
using Ceres.Events;
using Ceres.Schedulers;
using UnityEngine;
using UnityEngine.Animations;

namespace Ceres.Gameplay.Animations
{
    /// <summary>
    /// Category for AnimationProxy events
    /// </summary>
    public interface IAnimationProxyEvent
    {
        
    }
    
    public partial class AnimationNotifyEvent : EventBase<AnimationNotifyEvent>, IAnimationProxyEvent
    {
        public AnimationNotifier Notifier { get; private set; }
    }
    
    /// <summary>
    /// Class for define a notifier for animation proxy montage
    /// </summary>
    public class AnimationNotifier
    {
        /// <summary>
        /// Animator layer to observe if montage use animator controller
        /// </summary>
        public int Layer { get; }
        
        /// <summary>
        /// Normalized time to observe, do not observe time if less than zero
        /// </summary>
        public float NormalizedTime { get; } = -1;
        
        public AnimationNotifier()
        {

        }
        
        public AnimationNotifier(int layer = 0, float normalizedTime = -1)
        {
            Layer = layer;
            NormalizedTime = normalizedTime;
        }
        
        public virtual bool CanNotify(AnimationProxy animationProxy, LayerHandle layerHandle, float lastTime)
        {
            if (NormalizedTime < 0) return true;
            float currentTime = animationProxy.GetLeafAnimationNormalizedTime(layerHandle, Layer);
            float duration = animationProxy.GetLeafAnimationDuration(layerHandle, Layer);
            if (currentTime >= NormalizedTime)
            {
                if (lastTime < NormalizedTime) return true;
                /* Case when last tick time is in last loop */
                if (lastTime > currentTime)
                {
                    /* Validate if interval is less than 2 frames */
                    if ((1 - lastTime + currentTime) * duration < Time.deltaTime * 2)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
    
    /// <summary>
    /// Notifier with specific animation state hash
    /// </summary>
    public class AnimationStateNotifier : AnimationNotifier
    {
        public readonly int StateHash;

        public AnimationStateNotifier(string stateName, int layer = 0, float normalizedTime = -1)
        : base(layer, normalizedTime)
        {
            StateHash = Animator.StringToHash(stateName);
        }
        
        public AnimationStateNotifier(int stateHash, int layer = 0, float normalizedTime = -1)
        : base(layer, normalizedTime)
        {
            StateHash = stateHash;
        }
        
        public override bool CanNotify(AnimationProxy animationProxy, LayerHandle layerHandle, float lastTime)
        {
            var playable = animationProxy.GetLeafPlayable(layerHandle);
            // Must use animator controller
            if (!playable.IsPlayableOfType<AnimatorControllerPlayable>()) return false;
            // Check time reach
            if (!base.CanNotify(animationProxy, layerHandle, lastTime)) return false;
            // Check state match
            return animationProxy.GetAnimatorControllerInstanceProxy(layerHandle)
                                 .GetCurrentAnimatorStateInfo(Layer).shortNameHash == StateHash;
        }
    }
    
    public partial class AnimationProxy
    {
        private class AnimationNotifierContext
        {
            public AnimationNotifier Notifier;
            
            public LayerHandle LayerHandle;
            
            public float LastTime;
        }
        
        internal partial class AnimationPreStopEvent : EventBase<AnimationPreStopEvent>, IAnimationProxyEvent
        {
            public float BlendOutDuration { get; private set; }
        }
        
        public class AnimationEventHandler : CallbackEventHandler
        {
            public override IEventCoordinator Coordinator => EventSystem.Instance;
            
            public override void SendEvent(EventBase e, DispatchMode dispatchMode = DispatchMode.Default)
            {
                e.Target = this;
                EventSystem.Instance.Dispatch(e, dispatchMode, MonoDispatchType.LateUpdate);
            }
            
            public void SendEvent(EventBase e, DispatchMode dispatchMode, MonoDispatchType monoDispatchType)
            {
                e.Target = this;
                EventSystem.Instance.Dispatch(e, dispatchMode, monoDispatchType);
            }
        }
        
        private readonly List<AnimationNotifierContext> _notifierContexts = new();
        
        private SchedulerHandle _eventTickHandle;
        
        private AnimationEventHandler _eventHandler;
        
        public AnimationEventHandler GetEventHandler()
        {
            if (_eventHandler == null)
            {
                _eventHandler = new AnimationEventHandler();
                _eventHandler.RegisterCallback<AnimationPreStopEvent>(DoStop);
            }
            return _eventHandler;
        }
        
        public void AddNotifier(AnimationNotifier animationNotifier, LayerHandle layerHandle = default)
        {
            _notifierContexts.Add(new AnimationNotifierContext
            {
                Notifier = animationNotifier,
                LayerHandle = layerHandle,
                LastTime = 1
            });
            if (!_eventTickHandle.IsValid())
            {
                Scheduler.WaitFrame(ref _eventTickHandle, 1, TickEvents, TickFrame.LateUpdate, true);
            }
        }
        
        public void RemoveNotifier(AnimationNotifier notifier, LayerHandle layerHandle = default)
        {
            int inLayerIndex = GetLayerIndex(layerHandle);
            for (int i = _notifierContexts.Count - 1; i >= 0; i--)
            {
                if (_notifierContexts[i].Notifier == notifier && GetLayerIndex(_notifierContexts[i].LayerHandle) == inLayerIndex)
                {
                    _notifierContexts.RemoveAt(i);
                    break;
                }
            }
            if (_notifierContexts.Count == 0)
            {
                _eventTickHandle.Cancel();
                _eventTickHandle = default;
            }
        }
        
        private void TickEvents(int frame)
        {
            for (int i = 0; i < _notifierContexts.Count; ++i)
            {
                var context = _notifierContexts[i];
                var notifier = context.Notifier;
                float normalizedTime = GetLeafAnimationNormalizedTime(context.LayerHandle, notifier.Layer);

                /* Whether event should be fired */
                if (notifier.CanNotify(this, context.LayerHandle, context.LastTime))
                {
                    using var evt = AnimationNotifyEvent.Create(notifier);
                    GetEventHandler().SendEvent(evt);
                }
                context.LastTime = normalizedTime;
            }
        }
        
        public void RegisterNotifyCallback(EventCallback<AnimationNotifyEvent> callback)
        {
            GetEventHandler().RegisterCallback(callback);
        }
        
        public void UnregisterNotifyCallback(EventCallback<AnimationNotifyEvent> callback)
        {
            GetEventHandler().UnregisterCallback(callback);
        }
        
        private void DispatchStopEvent(float blendOutDuration)
        {
            using var evt = AnimationPreStopEvent.Create(blendOutDuration);
            GetEventHandler().SendEvent(evt, DispatchMode.Default, MonoDispatchType.Update);
        }
        
        private void DoStop(AnimationPreStopEvent preStopEvent)
        {
            if (RestoreAnimatorControllerOnStop && Animator.runtimeAnimatorController != SourceController)
            {
                Animator.runtimeAnimatorController = SourceController;
            }
            RootMontage.ScheduleBlendOut(preStopEvent.BlendOutDuration, SetOutGraph);
        }
    }
}
