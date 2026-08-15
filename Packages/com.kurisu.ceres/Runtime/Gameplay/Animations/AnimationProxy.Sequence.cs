using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.Tasks;
using UnityEngine;
using UnityEngine.Pool;
namespace Ceres.Gameplay.Animations
{
    public partial class AnimationProxy
    {
        public delegate void AnimationProxyDelegate(AnimationProxy animationProxy);
        
        /// <summary>
        /// Builder for creating dynamic animation sequence.
        /// </summary>
        public struct AnimationSequenceBuilder : IDisposable
        {
            private List<TaskBase> _taskBuffer;
            
            private SequenceTask _sequence;
            
            private float _blendOutTime;
            
            private bool _isDisposed;
            
            private AnimationProxy _proxy;
            
            internal AnimationSequenceBuilder(AnimationProxy proxy)
            {
                _proxy = proxy;
                _blendOutTime = 0f;
                _sequence = null;
                _isDisposed = false;
                _taskBuffer = ListPool<TaskBase>.Get();
            }
            
            /// <summary>
            /// Append an animation clip
            /// </summary>
            /// <param name="animationClip">Clip to play</param>
            /// <param name="blendInDuration">FadeIn time</param>
            /// <returns></returns>
            public readonly AnimationSequenceBuilder Append(AnimationClip animationClip, float blendInDuration)
            {
                return Append(animationClip, animationClip.length, blendInDuration);
            }
            
            /// <summary>
            /// Append an animation clip
            /// </summary>
            /// <param name="animationClip">Clip to play</param>
            /// <param name="duration">Duration can be infinity as loop</param>
            /// <param name="blendInDuration">FadeIn time</param>
            /// <returns></returns>
            public readonly AnimationSequenceBuilder Append(AnimationClip animationClip, float duration, float blendInDuration)
            {
                if (!IsValid())
                {
                    Debug.LogWarning("Builder is invalid but try to access it");
                    return this;
                }
                _taskBuffer.Add(LoadAnimationClipTask.GetPooled(_proxy, animationClip, duration, blendInDuration));
                return this;
            }

            /// <summary>
            /// Append an animator controller
            /// </summary>
            /// <param name="animatorController"></param>
            /// <param name="duration">Duration can be infinity as loop</param>
            /// <param name="blendInDuration">FadeIn time</param>
            /// <returns></returns>
            public readonly AnimationSequenceBuilder Append(RuntimeAnimatorController animatorController, float duration, float blendInDuration)
            {
                if (!IsValid())
                {
                    Debug.LogWarning("Builder is invalid but try to access it");
                    return this;
                }
                _taskBuffer.Add(LoadAnimatorTask.GetPooled(_proxy, animatorController, duration, blendInDuration));
                return this;
            }
            
            /// <summary>
            /// Append a proxy call back after current last action in the sequence
            /// </summary>
            /// <param name="callBack"></param>
            /// <returns></returns>
            public readonly AnimationSequenceBuilder AppendCallBack(AnimationProxyDelegate callBack)
            {
                var proxy = _proxy;
                _taskBuffer.LastOrDefault()?.RegisterCallback<TaskCompleteEvent>(_ =>
                {
                    callBack(proxy);
                });
                return this;
            }
            
            /// <summary>
            /// Set animation sequence blend out time, default is 0
            /// </summary>
            /// <param name="inBlendOutTime"></param>
            /// <returns></returns>
            public AnimationSequenceBuilder SetBlendOut(float inBlendOutTime)
            {
                if (!IsValid())
                {
                    Debug.LogWarning("Builder is invalid but try to access it");
                    return this;
                }
                _blendOutTime = inBlendOutTime;
                return this;
            }
            
            /// <summary>
            /// Build an animation sequence
            /// </summary>
            public SequenceTask Build()
            {
                if (!IsValid())
                {
                    Debug.LogWarning("Builder is invalid, rebuild is not allowed");
                    return _sequence;
                }
                return BuildInternal(SequenceTask.GetPooled());
            }
            
            /// <summary>
            /// Append animation sequence after an existed sequence
            /// </summary>
            /// <param name="sequenceTask"></param>
            public SequenceTask Build(SequenceTask sequenceTask)
            {
                if (!IsValid())
                {
                    Debug.LogWarning("Builder is invalid, rebuild is not allowed");
                    return null;
                }
                return BuildInternal(sequenceTask);
            }
            
            private SequenceTask BuildInternal(SequenceTask sequenceTask)
            {
                foreach (var task in _taskBuffer)
                {
                    sequenceTask.Append(task);
                }
                float time = _blendOutTime;
                var animProxy = _proxy;
                sequenceTask.RegisterCallback<TaskCompleteEvent>(_ => animProxy.Stop(time));
                _sequence = sequenceTask;
                _taskBuffer.Clear();
                _sequence.Acquire();
                return _sequence;
            }
            
            /// <summary>
            /// Whether builder is valid
            /// </summary>
            /// <returns></returns>
            public readonly bool IsValid()
            {
                return _sequence == null && !_isDisposed;
            }

            /// <summary>
            /// Dispose internal playable graph
            /// </summary> <summary />
            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }
                _isDisposed = true;
                _proxy = null;
                _sequence = null;
                ListPool<TaskBase>.Release(_taskBuffer);
                _taskBuffer = null;
            }
        }
        
        private sealed class LoadAnimationClipTask : PooledTaskBase<LoadAnimationClipTask>
        {
            private AnimationProxy _proxy;
            
            private AnimationClip _animationClip;
            
            private float _blendInTime;
            
            private float _duration;
            
            private double _startTimestamp;
            
            public static LoadAnimationClipTask GetPooled(AnimationProxy proxy, AnimationClip animationClip, float duration, float blendInTime)
            {
                var task = GetPooled();
                task._proxy = proxy;
                task._animationClip = animationClip;
                task._duration = duration;
                task._blendInTime = blendInTime;
                return task;
            }
            
            public override void Tick()
            {
                if (Time.timeSinceLevelLoadAsDouble - _startTimestamp >= _duration)
                {
                    CompleteTask();
                }
            }
            
            public override void Start()
            {
                base.Start();
                _startTimestamp = Time.timeSinceLevelLoadAsDouble;
                _proxy.LoadAnimationClip(_animationClip, _blendInTime);
            }
        }
        
        private sealed class LoadAnimatorTask : PooledTaskBase<LoadAnimatorTask>
        {
            private AnimationProxy _proxy;
            
            private RuntimeAnimatorController _animatorController;
            
            private float _blendInTime;
            
            private float _duration;
            
            private double _startTimestamp;
            
            public static LoadAnimatorTask GetPooled(AnimationProxy proxy, RuntimeAnimatorController animatorController, float duration, float blendInTime)
            {
                var task = GetPooled();
                task._proxy = proxy;
                task._animatorController = animatorController;
                task._duration = duration;
                task._blendInTime = blendInTime;
                return task;
            }
            
            public override void Tick()
            {
                if (Time.timeSinceLevelLoadAsDouble - _startTimestamp >= _duration)
                {
                    CompleteTask();
                }
            }
            
            public override void Start()
            {
                base.Start();
                _startTimestamp = Time.timeSinceLevelLoadAsDouble;
                _proxy.LoadAnimator(_animatorController, _blendInTime);
            }
            
            protected override void Reset()
            {
                base.Reset();
                _proxy = null;
                _animatorController = null;
            }
        }
        
        #region Public API
        
        /// <summary>
        /// Create an <see cref="AnimationSequenceBuilder"/> from this proxy
        /// </summary>
        /// <returns></returns>
        public AnimationSequenceBuilder CreateSequenceBuilder()
        {
            return new AnimationSequenceBuilder(this);
        }
        
        #endregion Public API
    }
}
