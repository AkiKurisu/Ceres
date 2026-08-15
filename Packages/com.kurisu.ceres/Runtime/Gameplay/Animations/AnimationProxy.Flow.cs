using System;
using System.Collections.Generic;
using Ceres.Flow;
using Ceres.Flow.Annotations;
using Ceres.Tasks;
using UnityEngine;

namespace Ceres.Gameplay.Animations
{
    public partial class AnimationProxy
    {
        private readonly Dictionary<AnimationClip, SequenceTask> _runningSequences = new();

        // ============================== Sequence ========================== //
        /* Following API only works on default layer */
        
        [ExecutableFunction]
        public void Flow_PlayAnimation(
            AnimationClip animationClip, 
            int loopCount = 1,
            float blendInTime = 0.25f, 
            float blendOutTime = 0.25f)
        {
            Flow_StopAnimation(animationClip);
            _runningSequences[animationClip] = CreateSequenceBuilder()
                .Append(animationClip, animationClip.length * loopCount, blendInTime)
                .SetBlendOut(blendOutTime)
                .Build()
                .Run();
            _runningSequences[animationClip].Acquire();
        }
        
        [ExecutableFunction]
        public void Flow_PlayAnimationWithCompleteEvent(
            AnimationClip animationClip, 
            int loopCount = 1,
            float blendInTime = 0.25f, 
            float blendOutTime = 0.25f, 
            EventDelegate onComplete = null)
        {
            Flow_StopAnimation(animationClip);
            Action onCompleteAction = onComplete;
            _runningSequences[animationClip] = CreateSequenceBuilder()
                .Append(animationClip, animationClip.length * loopCount, blendInTime)
                .SetBlendOut(blendOutTime)
                .AppendCallBack(_ => onCompleteAction?.Invoke())
                .Build()
                .Run();
            _runningSequences[animationClip].Acquire();
        }

        [ExecutableFunction]
        public void Flow_StopAnimation(AnimationClip animationClip)
        {
            if (!_runningSequences.TryGetValue(animationClip, out var task)) return;
            task?.Stop();
            task?.Dispose();
            _runningSequences.Remove(animationClip);
        }
        
        [ExecutableFunction]
        public void Flow_StopAllAnimation()
        {
            StopAllAnimationSequences();
        }
        // ============================== Sequence ========================== //

        // ============================== Montage ========================== //
        [ExecutableFunction]
        public LayerHandle Flow_CreateMontageLayer(string layerName, uint layerIndex = 0, bool additive = false, AvatarMask avatarMask = null)
        {
            LayerHandle handle = default;
            CreateLayer(ref handle, layerName, layerIndex, additive, avatarMask);
            return handle;
        }
        // ============================== Montage ========================== //
        
        // ============================== Event ========================== //
        [ExecutableFunction]
        public AnimationNotifier Flow_AddAnimationNotifier(int layer, float normalizedTime = -1, LayerHandle layerHandle = default, EventDelegate onNotify = null)
        {
            AnimationNotifier notifier;
            AddNotifier(notifier = new AnimationNotifier(layer, normalizedTime), layerHandle);
            Action onNotifyAction = onNotify;
            RegisterNotifyCallback(evt =>
            {
                if (evt.Notifier == notifier)
                {
                    onNotifyAction?.Invoke();
                }
            });
            return notifier;
        }
        
        [ExecutableFunction]
        public void Flow_RemoveAnimationNotifier(AnimationNotifier notifier, LayerHandle layerHandle = default)
        {
            RemoveNotifier(notifier, layerHandle);
        }
        // ============================== Event ========================== //

        /// <summary>
        /// Stop all running animation sequences created by this proxy
        /// </summary>
        public void StopAllAnimationSequences()
        {
            foreach (var pair in _runningSequences)
            {
                pair.Value?.Stop();
                pair.Value?.Dispose();
            }
            _runningSequences.Clear();
        }
    }
}
