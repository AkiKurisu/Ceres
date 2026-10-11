using System;
using Ceres.Modules;
using R3;
using UnityEngine;
using UnityEngine.Scripting;

namespace Ceres.Gameplay.Graphics
{
    [Preserve]
    internal class DisplaySettingsModule : RuntimeModule
    {
        public override void Initialize()
        {
            var config = DisplayConfig.Get();
            if (!Application.isMobilePlatform && !Application.isEditor)
            {
                // A first run adopts the window the player opened with.
                if (config.ResolutionWidth.Value <= 0 || config.ResolutionHeight.Value <= 0)
                {
                    config.FullScreenMode.Value = Screen.fullScreenMode;
                    config.ResolutionWidth.Value = Screen.width;
                    config.ResolutionHeight.Value = Screen.height;
                }
                Observable.Merge(config.FullScreenMode.AsUnitObservable(), config.ResolutionWidth.AsUnitObservable(),
                        config.ResolutionHeight.AsUnitObservable())
                    .Subscribe(_ => ApplyScreen(config));
            }
            Observable.Merge(config.VSync.AsUnitObservable(), config.FrameRateLimit.AsUnitObservable())
                .Subscribe(_ => ApplyFrameRate(config));
        }

        private static void ApplyScreen(DisplayConfig config)
        {
            var mode = config.FullScreenMode.Value;
            bool native = mode == FullScreenMode.FullScreenWindow;
            int width = native ? Display.main.systemWidth : config.ResolutionWidth.Value;
            int height = native ? Display.main.systemHeight : config.ResolutionHeight.Value;
            if (Screen.width == width && Screen.height == height && Screen.fullScreenMode == mode) return;
            Screen.SetResolution(width, height, mode);
        }

        private static void ApplyFrameRate(DisplayConfig config)
        {
            bool vSync = config.VSync.Value && !Application.isMobilePlatform;
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            int limit = config.FrameRateLimit.Value;
            if (vSync)
            {
                Application.targetFrameRate = -1;
            }
            else if (limit > 0)
            {
                Application.targetFrameRate = limit;
            }
            else
            {
                // Mobile players treat -1 as 30 FPS, so unlimited means the display refresh rate there.
                Application.targetFrameRate = Application.isMobilePlatform
                    ? (int)Math.Ceiling(Screen.currentResolution.refreshRateRatio.value)
                    : -1;
            }
        }
    }
}
