#if URP_INSTALL
using System.Reflection;
using Ceres.Configs;
using Ceres.Modules;
using R3;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Scripting;

namespace Ceres.Gameplay.Graphics
{
    [Preserve]
    internal class GraphicsSettingsModule : RuntimeModule
    {
        private UniversalRenderPipelineAsset _scaledAsset;

        private float _originalRenderScale;

        public override void Initialize()
        {
            var config = GraphicsConfig.Get();
            ForwardConfigVariables(config);
            config.RenderScaleRatio.Subscribe(_ => ApplyRenderScale(config));
            config.TextureMipmapLimit.Subscribe(limit => QualitySettings.globalTextureMipmapLimit = Mathf.Max(0, limit));
            QualitySettings.activeQualityLevelChanged += (_, _) => ApplyRenderScale(config);
#if UNITY_EDITOR
            Application.quitting += RestoreRenderScale;
#endif
        }

        private static void ForwardConfigVariables(GraphicsConfig config)
        {
            var registry = ConfigVariableRegistry.Get();
            foreach (var property in typeof(GraphicsConfig).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var binding = property.GetCustomAttribute<BindConfigVariableAttribute>();
                if (binding == null || property.PropertyType != typeof(ReactiveProperty<bool>)) continue;
                if (!registry.TryGetVariable(binding.Name, out var variable))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[Ceres] Config variable {binding.Name} bound by {nameof(GraphicsConfig)}.{property.Name} is not registered.");
#endif
                    continue;
                }

                var source = (ReactiveProperty<bool>)property.GetValue(config);
                source.Subscribe(value => variable.SetValue(value, false));
            }
        }

        private void ApplyRenderScale(GraphicsConfig config)
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset asset) return;
            RestoreRenderScale();
            _scaledAsset = asset;
            _originalRenderScale = asset.renderScale;
            asset.renderScale = Mathf.Clamp(config.RenderScaleRatio.Value, 0.1f, 2f);
        }

        // Pipeline assets are project assets; leave them as authored when the scale moves to another asset or Play Mode ends.
        private void RestoreRenderScale()
        {
            if (!_scaledAsset) return;
            _scaledAsset.renderScale = _originalRenderScale;
            _scaledAsset = null;
        }
    }
}
#endif
