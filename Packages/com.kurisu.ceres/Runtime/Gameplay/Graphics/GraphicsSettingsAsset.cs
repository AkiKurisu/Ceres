#if URP_INSTALL
using System;
using UnityEngine;

namespace Ceres.Gameplay.Graphics
{
    [Flags]
    public enum GraphicsFeatures
    {
        None = 0,
        ScreenSpaceReflection = 1 << 0,
        ScreenSpaceGlobalIllumination = 1 << 1,
        ScreenSpaceAmbientOcclusion = 1 << 2,
        MotionBlur = 1 << 3
    }
    
    [CreateAssetMenu(fileName = "GraphicsConfig", menuName = "Ceres/Graphics/GraphicsSettingsAsset")]
    public class GraphicsSettingsAsset : ScriptableObject
    {
        // Camera Settings
        [Header("Camera Settings")]
        public float fieldOfView = 23;
            
        public float nearClipPlane = 0.1f;
            
        public float farClipPlane = 5000;

        [Header("Quality Settings")]
        [SerializeField]
        private GraphicsFeatures disableFeatures;

        public bool IsFeatureSupport(GraphicsFeatures features)
        {
            return (disableFeatures & features) == 0;
        }
    }
}
#endif
