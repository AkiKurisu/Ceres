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
        DepthOfField = 1 << 3,
        MotionBlur = 1 << 4
    }
    
    [CreateAssetMenu(fileName = "GraphicsConfig", menuName = "Ceres/Graphics/GraphicsSettingsAsset")]
    public class GraphicsSettingsAsset : ScriptableObject
    {
        // Camera Settings
        [Header("Camera Settings")]
        public float fieldOfView = 23;
            
        public float nearClipPlane = 0.1f;
            
        public float farClipPlane = 5000;

        // Quality Settings
        [Header("Quality Settings")]
        public int[] frameRateOptions = { 30, 60, -1 };

        [SerializeField]
        private GraphicsFeatures disableFeatures;

        public bool IsFeatureSupport(GraphicsFeatures features)
        {
            return (disableFeatures & features) == 0;
        }
    }
}
#endif