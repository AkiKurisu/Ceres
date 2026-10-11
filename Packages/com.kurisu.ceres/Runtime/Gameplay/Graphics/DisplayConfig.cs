using Ceres.Configs;
using Ceres.Serialization;
using R3;
using UnityEngine;

namespace Ceres.Gameplay.Graphics
{
    [PreferJsonConvert]
    [ConfigPath("Ceres.Display")]
    public class DisplayConfig : Config<DisplayConfig>
    {
        public ReactiveProperty<FullScreenMode> FullScreenMode { get; set; } = new(UnityEngine.FullScreenMode.FullScreenWindow);

        /// <summary>
        /// Window or fullscreen width; zero keeps the current resolution
        /// </summary>
        public ReactiveProperty<int> ResolutionWidth { get; set; } = new(0);

        public ReactiveProperty<int> ResolutionHeight { get; set; } = new(0);

        public ReactiveProperty<bool> VSync { get; set; } = new(false);

        /// <summary>
        /// Target frame rate while vertical sync is off; zero or below is unlimited
        /// </summary>
        public ReactiveProperty<int> FrameRateLimit { get; set; } = new(60);

        [ConfigVariable("r.fps")]
        public ReactiveProperty<bool> ShowFrameRate { get; set; } = new(false);
    }
}
