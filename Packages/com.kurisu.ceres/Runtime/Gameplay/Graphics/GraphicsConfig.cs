using Ceres.Configs;
using Ceres.Serialization;
using R3;

namespace Ceres.Gameplay.Graphics
{
    [PreferJsonConvert]
    [ConfigPath("Ceres.Graphics")]
    public class GraphicsConfig: Config<GraphicsConfig>
    {
        [BindConfigVariable("r.bloom")]
        public ReactiveProperty<bool> Bloom { get; set; } = new(true);
        
#if UNITY_STANDALONE_WIN
        public ReactiveProperty<bool> DepthOfField { get; set; } = new(true);
#else
        public ReactiveProperty<bool> DepthOfField { get; set; } = new(false);
#endif
            
        public ReactiveProperty<bool> MotionBlur { get; set; } = new(false);
        
#if UNITY_STANDALONE_WIN
        public ReactiveProperty<float> RenderScaleRatio { get; set; } = new(1f);
#else
        public ReactiveProperty<float> RenderScaleRatio { get; set; } = new(0.9f);
#endif
        
        /// <summary>
        /// Global texture mipmap limit; 0 keeps full resolution
        /// </summary>
        public ReactiveProperty<int> TextureMipmapLimit { get; set; } = new(0);
        
#if UNITY_STANDALONE_WIN
        [BindConfigVariable("r.contactshadows")]
        public ReactiveProperty<bool> ContactShadows { get; set; } = new(true);
#else
        [BindConfigVariable("r.contactshadows")]
        public ReactiveProperty<bool> ContactShadows { get; set; } = new(false);
#endif
            
#if UNITY_STANDALONE_WIN
        [BindConfigVariable("r.pcss")]
        public ReactiveProperty<bool> PercentageCloserSoftShadows { get; set; } = new(true);
#else
        [BindConfigVariable("r.pcss")]
        public ReactiveProperty<bool> PercentageCloserSoftShadows { get; set; } = new(false);
#endif
            
        [BindConfigVariable("r.ssao")]
        public ReactiveProperty<bool> ScreenSpaceAmbientOcclusion { get; set; } = new(true);
        
#if UNITY_STANDALONE_WIN
        [BindConfigVariable("r.ssr")]
        public ReactiveProperty<bool> ScreenSpaceReflection { get; set; } = new(true);
#else
        [BindConfigVariable("r.ssr")]
        public ReactiveProperty<bool> ScreenSpaceReflection { get; set; } = new(false);
#endif
            
#if UNITY_STANDALONE_WIN
        [BindConfigVariable("r.ssgi")]
        public ReactiveProperty<bool> ScreenSpaceGlobalIllumination { get; set; } = new(true);
#else
        [BindConfigVariable("r.ssgi")]
        public ReactiveProperty<bool> ScreenSpaceGlobalIllumination { get; set; } = new(false);
#endif
            
        [BindConfigVariable("r.rtgi")]
        public ReactiveProperty<bool> RayTracedGlobalIllumination { get; set; } = new(false);

        [BindConfigVariable("r.pathtracing")]
        public ReactiveProperty<bool> PathTracing { get; set; } = new(false);
        
        [BindConfigVariable("r.volumetricfog")]
        public ReactiveProperty<bool> VolumetricFog { get; set; } = new(true);
        
        [BindConfigVariable("r.sunshafts")]
        public ReactiveProperty<bool> SunShafts { get; set; } = new(true);
        
#if UNITY_STANDALONE_WIN
        [BindConfigVariable("r.arealights")]
        public ReactiveProperty<bool> AreaLights { get; set; } = new(true);
#else
        [BindConfigVariable("r.arealights")]
        public ReactiveProperty<bool> AreaLights { get; set; } = new(false);
#endif
            
#if UNITY_STANDALONE_WIN
        [BindConfigVariable("r.dlssnr")]
        public ReactiveProperty<bool> DLSSNeuralRendering { get; set; } = new(true);
#else
        [BindConfigVariable("r.dlssnr")]
        public ReactiveProperty<bool> DLSSNeuralRendering { get; set; } = new(false);
#endif
    }
}