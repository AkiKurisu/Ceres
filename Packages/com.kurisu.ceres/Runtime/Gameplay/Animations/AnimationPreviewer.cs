using UnityEngine;
using UnityEngine.Playables;

namespace Ceres.Gameplay.Animations
{
    /// <summary>
    /// MonoBehaviour to preview animation pose both in editor and play mode
    /// </summary>
    public class AnimationPreviewer : MonoBehaviour
    {
        public Animator animator;
        
        public AnimationClip animationClip;
        
        [HideInInspector]
        public float normalizedTime;
        
        private AnimationProxy _animationProxy;
        
        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }
        
        private void OnDestroy()
        {
            Release();
        }
        
        private void Release()
        {
            _animationProxy?.Dispose();
        }
        
        #region Runtime Rreview
        
        public void Preview()
        {
            _animationProxy ??= new AnimationProxy(animator);
            _animationProxy.LoadAnimationClip(animationClip, 0);
        }

        public void SetTime(float time)
        {
            _animationProxy.GetLeafPlayable().SetTime(time);
        }
        
        public void Stop()
        {
            _animationProxy.Stop(0);
        }

        public bool IsPlaying()
        {
            return _animationProxy is { IsPlaying: true };
        }
        
        #endregion Runtime Rreview
    }
}
