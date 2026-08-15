using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.Flow.Annotations;
using UnityEngine;
using UnityEngine.Animations;
using UAnimator = UnityEngine.Animator;
namespace Ceres.Gameplay.Animations
{
    /// <summary>
    /// Proxy layer handle
    /// </summary>
    public readonly struct LayerHandle : IEquatable<LayerHandle>
    {
        public readonly int Id;

        public LayerHandle(string layerName)
        {
            Id = UAnimator.StringToHash(layerName);
        }
        
        public LayerHandle(int layerId)
        {
            Id = layerId;
        }
        
        public static bool operator ==(LayerHandle left, LayerHandle right)
        {
            return left.Id == right.Id;
        }
        public static bool operator !=(LayerHandle left, LayerHandle right)
        {
            return left.Id != right.Id;
        }
        public override bool Equals(object obj)
        {
            if (obj is not LayerHandle handle) return false;
            return handle.Id == Id;
        }
        public bool Equals(LayerHandle other)
        {
            return other.Id == Id;
        }
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
        public bool IsValid()
        {
            return Id != 0;
        }
    }
    
    /// <summary>
    /// Proxy layer descriptor
    /// </summary>
    public struct LayerDescriptor : IEquatable<LayerDescriptor>
    {
        public string Name;
        
        public uint Index;
        
        public bool Additive;
        
        public AvatarMask AvatarMask;

        public readonly bool Equals(LayerDescriptor other)
        {
            return Index == other.Index && Additive == other.Additive
                    && AvatarMask == other.AvatarMask && Name == other.Name;
        }
    }
    
    public partial class AnimationProxy
    {
        /// <summary>
        /// Runtime proxy layer context
        /// </summary>
        public class LayerContext
        {
            public LayerHandle Handle;
            
            public LayerDescriptor Descriptor;
            
            public AnimationLayerMontageNode MontageNode;
            
            public static readonly LayerContext Empty = new();
        }
        
        public readonly Dictionary<LayerHandle, LayerContext> LayerContexts = new();

        /// <summary>
        /// Get or create a new montage layer
        /// </summary>
        /// <param name="layerHandle"></param>
        /// <param name="layerName"></param>
        /// <param name="layerIndex"></param>
        /// <param name="additive"></param>
        /// <param name="avatarMask"></param>
        public void CreateLayer(ref LayerHandle layerHandle, string layerName, 
            uint layerIndex = 0, bool additive = false,
            AvatarMask avatarMask = null)
        {
            var descriptor = new LayerDescriptor
            {
                Name = layerName,
                Index = layerIndex,
                Additive = additive,
                AvatarMask = avatarMask
            };
            CreateLayer(ref layerHandle, descriptor);
        }
        
        /// <summary>
        ///  Get or create a new montage layer
        /// </summary>
        /// <param name="layerHandle"></param>
        /// <param name="layerDescriptor"></param>
        public void CreateLayer(ref LayerHandle layerHandle, LayerDescriptor layerDescriptor)
        {
            layerHandle = new LayerHandle(layerDescriptor.Name);
            if (LayerContexts.TryGetValue(layerHandle, out var context))
            {
                /* Skip if is matched with current layer */
                if (context.Descriptor.Equals(layerDescriptor))
                {
                    return;
                }
                /* We should not modify graph when executing montage with previews descriptor */
                if (context.MontageNode != null && context.MontageNode.IsValid())
                {
                    Debug.LogError($"[AnimationProxy] Can not create new layer when another layer named {layerDescriptor.Name} is executing");
                    return;
                }
                LayerContexts.Remove(layerHandle);
            }
            context = new LayerContext
            {
                Handle = layerHandle,
                Descriptor = layerDescriptor,
                MontageNode = null,
            };
            LayerContexts.Add(layerHandle, context);
        }
        
        public LayerContext GetLayerContext(LayerHandle handle)
        {
            return LayerContexts.TryGetValue(handle, out var layerContext) ? layerContext : LayerContext.Empty;
        }
        
        public LayerContext[] GetAllLayerContexts()
        {
            return LayerContexts.Values.OrderBy(x => x.Descriptor.Index).ToArray();
        }
        
        public int GetLayerIndex(LayerHandle layerHandle)
        {
            if (RootMontage is AnimationLayerMontageNode)
            {
                var context = GetLayerContext(layerHandle);
                if (context.Handle.IsValid())
                    return (int)context.Descriptor.Index;
            }
            return DefaultLayerIndex;
        }
        
        public readonly struct AnimationClipInstanceProxy
        {
            private readonly AnimationClipPlayable _clipPlayable;
            
            internal AnimationClipInstanceProxy(AnimationClipPlayable playable)
            {
                _clipPlayable = playable;
            }
            
            #region Wrapper
            
            /// <summary>
            /// Returns the AnimationClip stored in the AnimationClipPlayable.
            /// </summary>
            /// <returns></returns>
            [ExecutableFunction]
            public AnimationClip GetAnimationClip()
            {
                return _clipPlayable.GetAnimationClip();
            }

            /// <summary>
            /// Returns the state of the ApplyFootIK flag.
            /// </summary>
            /// <returns></returns>
            [ExecutableFunction]
            public bool GetApplyFootIK()
            {
                return _clipPlayable.GetApplyFootIK();
            }

            /// <summary>
            ///  Sets the value of the ApplyFootIK flag.
            /// </summary>
            /// <param name="value"></param>
            [ExecutableFunction]
            public void SetApplyFootIK(bool value)
            {
                _clipPlayable.SetApplyFootIK(value);
            }

            /// <summary>
            /// Returns the state of the ApplyPlayableIK flag.
            /// </summary>
            /// <returns></returns>
            [ExecutableFunction]
            public bool GetApplyPlayableIK()
            {
                return _clipPlayable.GetApplyPlayableIK();
            }

            /// <summary>
            /// Requests OnAnimatorIK to be called on the animated GameObject.
            /// </summary>
            /// <param name="value"></param>
            [ExecutableFunction]
            public void SetApplyPlayableIK(bool value)
            {
                _clipPlayable.SetApplyPlayableIK(value);
            }
            
            #endregion Wrapper
        }
        
        public readonly struct AnimatorControllerInstanceProxy
        {
            private readonly AnimatorControllerPlayable _animatorPlayable;
            
            private readonly RuntimeAnimatorController _animatorController;
            
            internal AnimatorControllerInstanceProxy(AnimatorControllerPlayable playable, RuntimeAnimatorController sourceController)
            {
                _animatorPlayable = playable;
                _animatorController = sourceController;
            }
            
            public RuntimeAnimatorController GetAnimatorController()
            {
                return _animatorController;
            }
            
            #region Wrapper
            
            [ExecutableFunction]
            public float GetFloat(string name)
            {
                return _animatorPlayable.GetFloat(name);
            }

            [ExecutableFunction]
            public float GetFloat(int id)
            {
                return _animatorPlayable.GetFloat(id);
            }
            
            [ExecutableFunction]
            public void SetFloat(string name, float value)
            {
                _animatorPlayable.SetFloat(name, value);
            }
            
            [ExecutableFunction]
            public void SetFloat(int id, float value)
            {
                _animatorPlayable.SetFloat(id, value);
            }
            
            [ExecutableFunction]
            public bool GetBool(string name)
            {
                return _animatorPlayable.GetBool(name);
            }
            
            [ExecutableFunction]
            public bool GetBool(int id)
            {
                return _animatorPlayable.GetBool(id);
            }
            
            [ExecutableFunction]
            public void SetBool(string name, bool value)
            {
                _animatorPlayable.SetBool(name, value);
            }

            [ExecutableFunction]
            public void SetBool(int id, bool value)
            {
                _animatorPlayable.SetBool(id, value);
            }

            [ExecutableFunction]
            public int GetInteger(string name)
            {
                return _animatorPlayable.GetInteger(name);
            }
            
            [ExecutableFunction]
            public int GetInteger(int id)
            {
                return _animatorPlayable.GetInteger(id);
            }
            
            [ExecutableFunction]
            public void SetInteger(string name, int value)
            {
                _animatorPlayable.SetInteger(name, value);
            }

            [ExecutableFunction]
            public void SetInteger(int id, int value)
            {
                _animatorPlayable.SetInteger(id, value);
            }
            
            [ExecutableFunction]
            public void SetTrigger(string name)
            {
                _animatorPlayable.SetTrigger(name);
            }

            [ExecutableFunction]
            public void SetTrigger(int id)
            {
                _animatorPlayable.SetTrigger(id);
            }

            [ExecutableFunction]
            public void ResetTrigger(string name)
            {
                _animatorPlayable.ResetTrigger(name);
            }
            
            [ExecutableFunction]
            public void ResetTrigger(int id)
            {
                _animatorPlayable.ResetTrigger(id);
            }
            
            [ExecutableFunction]
            public bool IsParameterControlledByCurve(string name)
            {
                return _animatorPlayable.IsParameterControlledByCurve(name);
            }
            
            [ExecutableFunction]
            public bool IsParameterControlledByCurve(int id)
            {
                return _animatorPlayable.IsParameterControlledByCurve(id);
            }

            [ExecutableFunction]
            public int GetLayerCount()
            {
                return _animatorPlayable.GetLayerCount();
            }

            [ExecutableFunction]
            public string GetLayerName(int layerIndex)
            {
                return _animatorPlayable.GetLayerName(layerIndex);
            }

            [ExecutableFunction]
            public int GetLayerIndex(string layerName)
            {
                return _animatorPlayable.GetLayerIndex(layerName);
            }
            
            [ExecutableFunction]
            public float GetLayerWeight(int layerIndex)
            {
                return _animatorPlayable.GetLayerWeight(layerIndex);
            }
            
            [ExecutableFunction]
            public void SetLayerWeight(int layerIndex, float weight)
            {
                _animatorPlayable.SetLayerWeight(layerIndex, weight);
            }

            [ExecutableFunction]
            public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex)
            {
                return _animatorPlayable.GetCurrentAnimatorStateInfo(layerIndex);
            }

            [ExecutableFunction]
            public AnimatorStateInfo GetNextAnimatorStateInfo(int layerIndex)
            {
                return _animatorPlayable.GetNextAnimatorStateInfo(layerIndex);
            }

            [ExecutableFunction]
            public AnimatorTransitionInfo GetAnimatorTransitionInfo(int layerIndex)
            {
                return _animatorPlayable.GetAnimatorTransitionInfo(layerIndex);
            }

            [ExecutableFunction]
            public AnimatorClipInfo[] GetCurrentAnimatorClipInfo(int layerIndex)
            {
                return _animatorPlayable.GetCurrentAnimatorClipInfo(layerIndex);
            }

            [ExecutableFunction]
            public void GetCurrentAnimatorClipInfo(int layerIndex, List<AnimatorClipInfo> clips)
            {
                _animatorPlayable.GetCurrentAnimatorClipInfo(layerIndex, clips);
            }

            [ExecutableFunction]
            public void GetNextAnimatorClipInfo(int layerIndex, List<AnimatorClipInfo> clips)
            {
                _animatorPlayable.GetNextAnimatorClipInfo(layerIndex, clips);
            }
            
            [ExecutableFunction]
            public int GetCurrentAnimatorClipInfoCount(int layerIndex)
            {
                return _animatorPlayable.GetCurrentAnimatorClipInfoCount(layerIndex);
            }
            
            [ExecutableFunction]
            public int GetNextAnimatorClipInfoCount(int layerIndex)
            {
                return _animatorPlayable.GetNextAnimatorClipInfoCount(layerIndex);
            }

            [ExecutableFunction]
            public AnimatorClipInfo[] GetNextAnimatorClipInfo(int layerIndex)
            {
                return _animatorPlayable.GetNextAnimatorClipInfo(layerIndex);
            }
            
            [ExecutableFunction]
            public bool IsInTransition(int layerIndex)
            {
                return _animatorPlayable.IsInTransition(layerIndex);
            }

            [ExecutableFunction]
            public int GetParameterCount()
            {
                return _animatorPlayable.GetParameterCount();
            }

            [ExecutableFunction]
            public AnimatorControllerParameter GetParameter(int index)
            {
                return _animatorPlayable.GetParameter(index);
            }

            [ExecutableFunction]
            public void CrossFadeInFixedTime(string stateName, float transitionDuration, int layer = -1, float fixedTime = 0f)
            {
                _animatorPlayable.CrossFadeInFixedTime(stateName, transitionDuration, layer, fixedTime);
            }
            
            [ExecutableFunction]
            public void CrossFadeInFixedTime(int stateNameHash, float transitionDuration, int layer = -1, float fixedTime = 0.0f)
            {
                _animatorPlayable.CrossFadeInFixedTime(stateNameHash, transitionDuration, layer, fixedTime);
            }
            
            [ExecutableFunction]
            public void CrossFade(string stateName, float transitionDuration, int layer = -1, float normalizedTime = float.NegativeInfinity)
            {
                _animatorPlayable.CrossFade(stateName, transitionDuration, layer, normalizedTime);
            }

            [ExecutableFunction]
            public void CrossFade(int stateNameHash, float transitionDuration, int layer = -1, float normalizedTime = float.NegativeInfinity)
            {
                _animatorPlayable.CrossFade(stateNameHash, transitionDuration, layer, normalizedTime);
            }
            
            [ExecutableFunction]
            public void PlayInFixedTime(string stateName, int layer = -1, float fixedTime = float.NegativeInfinity)
            {
                _animatorPlayable.PlayInFixedTime(stateName, layer, fixedTime);
            }

            [ExecutableFunction]
            public void PlayInFixedTime(int stateNameHash, int layer = -1, float fixedTime = float.NegativeInfinity)
            {
                _animatorPlayable.PlayInFixedTime(stateNameHash, layer, fixedTime);
            }

            [ExecutableFunction]
            public void Play(string stateName, int layer = -1, float normalizedTime = float.NegativeInfinity)
            {
                _animatorPlayable.Play(stateName, layer, normalizedTime);
            }

            [ExecutableFunction]
            public void Play(int stateNameHash, int layer = -1, float normalizedTime = float.NegativeInfinity)
            {
                _animatorPlayable.Play(stateNameHash, layer, normalizedTime);
            }

            [ExecutableFunction]
            public bool HasState(int layerIndex, int stateID)
            {
                return _animatorPlayable.HasState(layerIndex, stateID);
            }
            #endregion Public API
        }
    }
}