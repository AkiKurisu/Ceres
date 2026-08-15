using Ceres.Configs;
using Ceres.Serialization;
using Newtonsoft.Json;
using R3;

namespace Ceres.Gameplay.Graphics
{
    [PreferJsonConvert]
    [ConfigPath("Ceres.Graphics")]
    public class GraphicsConfig: Config<GraphicsConfig>
    {
        public ReactiveProperty<int> FrameRate { get; set; } = new(0);
        
        public ReactiveProperty<bool> Bloom { get; set; } = new(true);
        
#if UNITY_STANDALONE_WIN
        public ReactiveProperty<bool> DepthOfField { get; set; } = new(true);
#else
        public ReactiveProperty<bool> DepthOfField { get; set; } = new(false);
#endif
            
        public ReactiveProperty<bool> MotionBlur { get; set; } = new(false);
        
#if UNITY_STANDALONE_WIN
        public ReactiveProperty<int> RenderScale { get; set; } = new(3);
#else
        public ReactiveProperty<int> RenderScale { get; set; } = new(2);
#endif
        
        [JsonIgnore]
        public static readonly float[] RenderScalePresets = { 0.7f, 0.8f, 0.9f, 1.0f };
        
        public ReactiveProperty<bool> Vignette { get; set; } = new(true);
        
#if UNITY_STANDALONE_WIN
        public ReactiveProperty<bool> ContactShadows { get; set; } = new(true);
#else
        public ReactiveProperty<bool> ContactShadows { get; set; } = new(false);
#endif
            
#if UNITY_STANDALONE_WIN
        public ReactiveProperty<bool> PercentageCloserSoftShadows { get; set; } = new(true);
#else
        public ReactiveProperty<bool> PercentageCloserSoftShadows { get; set; } = new(false);
#endif
            
        public ReactiveProperty<bool> ScreenSpaceAmbientOcclusion { get; set; } = new(true);
        
#if UNITY_STANDALONE_WIN
        public ReactiveProperty<bool> ScreenSpaceReflection { get; set; } = new(true);
#else
        public ReactiveProperty<bool> ScreenSpaceReflection { get; set; } = new(false);
#endif
            
#if UNITY_STANDALONE_WIN
        public ReactiveProperty<bool> ScreenSpaceGlobalIllumination { get; set; } = new(true);
#else
        public ReactiveProperty<bool> ScreenSpaceGlobalIllumination { get; set; } = new(false);
#endif
            
        public ReactiveProperty<bool> VolumetricFog { get; set; } = new(true);
        
        [JsonProperty]
        [ConfigVariable("r.fps")]
        internal bool DisplayFPS { get; set; }
    }
}