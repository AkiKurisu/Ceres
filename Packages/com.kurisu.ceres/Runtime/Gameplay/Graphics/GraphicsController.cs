#if URP_INSTALL
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using R3;
using UnityEngine.Rendering.Universal;
using System.Linq;
using Ceres.Flow.Annotations;
#if ILLUSION_RP_INSTALL
using Illusion.Rendering;
#endif

namespace Ceres.Gameplay.Graphics
{
    /// <summary>
    /// Runtime graphics controller
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    [ExecuteInEditMode]
    public class GraphicsController : MonoBehaviour
    {
        /// <summary>
        /// Define built-in volume type that can can be altered dynamically
        /// </summary>
        private enum BuiltInVolumeType
        {
            Bloom,
            DepthOfField,
            MotionBlur,
            Tonemapping,
            Vignette,
            ScreenSpaceAmbientOcclusion,
            ScreenSpaceReflection,
            ScreenSpaceGlobalIllumination,
            SubsurfaceScattering,
            PercentageCloserSoftShadows,
            ContactShadows,
            VolumetricFog
        }
        
        private readonly Dictionary<string, Volume> _volumes = new();

        public GraphicsSettingsAsset settingsAsset;
        
        private GraphicsConfig _config;
        
        private UniversalRenderPipelineAsset _urpAsset;
        
        internal const string LookDevModeKey = "Graphics.LookDevMode";
                
        private float _deltaTime;

        private GUIStyle _style;
        
        /// <summary>
        /// Get world instance graphics controller if exist
        /// </summary>
        /// <returns></returns>
        public static GraphicsController Get()
        {
            return ContainerSubsystem.Get().Resolve<GraphicsController>();
        }

        private void Awake()
        {
            _style = new GUIStyle
            {
                fontSize = 32,
                normal =
                {
                    textColor = Color.yellow
                }
            };
#if UNITY_EDITOR
            if (!gameObject.scene.IsValid()) return;
#endif
            
            PrepareDynamicVolumes();

            ApplyCameraSettings();
        }

        private void Start()
        {
            InitializeGraphics();
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (!gameObject.scene.IsValid()) return;
            if (!Application.isPlaying) return;
#endif
            const float smoothing = 0.9f;
            _deltaTime = _deltaTime * smoothing + 1f / Time.unscaledDeltaTime * (1f - smoothing);
        }
                
        private void OnDestroy()
        {
#if UNITY_EDITOR
            if (!gameObject.scene.IsValid()) return;
            if (!Application.isPlaying) return;
#endif

            _config?.Save();
            ContainerSubsystem.Get().Unregister(this);
        }

        private void OnGUI()
        {
#if UNITY_EDITOR
            if (!gameObject.scene.IsValid()) return;
            if (!Application.isPlaying) return;
#endif
            if (!GraphicsConfig.Get().DisplayFPS) return;
            int fps = (int)_deltaTime;
            GUI.Label(new Rect(20, 10, 200, 50), $"FPS {fps}", _style);
        }

        private void InitializeGraphics()
        {
#if UNITY_EDITOR
            if (!gameObject.scene.IsValid()) return;
#endif
            _config = GraphicsConfig.Get();
            _urpAsset = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
            
            // Apply dynamic volume if config is set
            if (settingsAsset)
            {
                ApplyDynamicVolumeProfiles();
            }
            
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Reset urp asset modification in editor
                _urpAsset.renderScale = 1;
                return;
            }
#endif
            
            ContainerSubsystem.Get().Register(this);

            // Bind properties
            var d = Disposable.CreateBuilder();
            if (IsVolumeSupport(BuiltInVolumeType.Bloom))
            {
                // URP bloom
                _config.Bloom.Subscribe(new BuiltInVolumeObserver(BuiltInVolumeType.Bloom, this)).AddTo(ref d);
#if ILLUSION_RP_INSTALL
                // Convolution bloom
                _config.Bloom.Subscribe(SetConvolutionBloomEnabled).AddTo(ref d);
#endif
            }
            
            if (IsVolumeSupport(BuiltInVolumeType.DepthOfField))
            {
                _config.DepthOfField.Subscribe(new BuiltInVolumeObserver(BuiltInVolumeType.DepthOfField, this)).AddTo(ref d);
            }
            
            if (IsVolumeSupport(BuiltInVolumeType.MotionBlur))
            {
                _config.MotionBlur.Subscribe(new BuiltInVolumeObserver(BuiltInVolumeType.MotionBlur, this)).AddTo(ref d);
            }

            if (IsVolumeSupport(BuiltInVolumeType.Vignette))
            {
                _config.Vignette.Subscribe(new BuiltInVolumeObserver(BuiltInVolumeType.Vignette, this)).AddTo(ref d);
            }

            _config.RenderScale.Subscribe(SetRenderScale).AddTo(ref d);
            _config.FrameRate.Subscribe(SetFrameRate).AddTo(ref d);
            
#if ILLUSION_RP_INSTALL
            if (IsVolumeSupport(BuiltInVolumeType.ContactShadows))
            {
                _config.ContactShadows.Subscribe(SetContactShadowsEnabled).AddTo(ref d);
            }
            if (IsVolumeSupport(BuiltInVolumeType.PercentageCloserSoftShadows))
            {
                _config.PercentageCloserSoftShadows.Subscribe(SetPercentageCloserSoftShadowsEnabled).AddTo(ref d);
            }
            if (IsVolumeSupport(BuiltInVolumeType.ScreenSpaceAmbientOcclusion))
            {
                _config.ScreenSpaceAmbientOcclusion.Subscribe(SetScreenSpaceAmbientOcclusionEnabled).AddTo(ref d);
            }
            if (IsVolumeSupport(BuiltInVolumeType.ScreenSpaceReflection))
            {
                _config.ScreenSpaceReflection.Subscribe(SetScreenSpaceReflection).AddTo(ref d);
            }
            if (IsVolumeSupport(BuiltInVolumeType.ScreenSpaceGlobalIllumination))
            {
                _config.ScreenSpaceGlobalIllumination.Subscribe(SetScreenSpaceGlobalIllumination).AddTo(ref d);
            }
            if (IsVolumeSupport(BuiltInVolumeType.VolumetricFog))
            {
                _config.VolumetricFog.Subscribe(SetVolumetricFog).AddTo(ref d);
            }
#endif
            d.Build().AddTo(this);
        }

        private void SetFrameRate(int index)
        {
            if (!Application.isPlaying) return;
            if (index >= settingsAsset.frameRateOptions.Length) return;
            
            Application.targetFrameRate = settingsAsset.frameRateOptions[index];
        }

        /// <summary>
        /// Apply camera settings by current graphics config
        /// </summary>
        [ExecutableFunction]
        public void ApplyCameraSettings()
        {
            if (!settingsAsset) return;
            
            var mainCamera = Camera.main;
            if (!mainCamera) return;

            mainCamera.fieldOfView = settingsAsset.fieldOfView;
            mainCamera.nearClipPlane = settingsAsset.nearClipPlane;
            mainCamera.farClipPlane = settingsAsset.farClipPlane;
        }

        private void PrepareDynamicVolumes()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(transform.GetChild(i).gameObject);
                }
            }
#endif
            var manager = DynamicVolumeProfileTableManager.Get();
            foreach (var volumeType in manager.GetDataTable(DynamicVolumeProfileTableManager.TableKey).GetRowMap().Keys)
            {
                var newObject = new GameObject(volumeType)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                newObject.transform.SetParent(transform);
                var volume = newObject.AddComponent<Volume>();
                _volumes.Add(volumeType, volume);
            }
        }

        public void ApplyVolumeProfiles(DynamicVolumePlatform? overridePlatform = null)
        {
#if UNITY_EDITOR
            bool lookDevMode = UnityEditor.EditorPrefs.GetBool(LookDevModeKey, false);
#endif
            var manager = DynamicVolumeProfileTableManager.Get();
            foreach (var volumeType in manager.GetDataTable(DynamicVolumeProfileTableManager.TableKey).GetRowMap().Keys)
            {
                var volume = GetVolume(volumeType);
                ApplyVolumeProfile(volume, manager.GetProfile(volumeType, overridePlatform), manager.GetPriority(volumeType));

#if UNITY_EDITOR
                if (lookDevMode && IsLookDevVolumeType(volumeType))
                {
                    volume.enabled = false;
                    continue;
                }
#endif

                if (Enum.TryParse<BuiltInVolumeType>(volumeType, out var type) && !IsVolumeSupport(type)) 
                {
                    volume.enabled = false;
                    continue;
                }
                
                volume.enabled = true;
            }
        }
        
        /// <summary>
        /// Apply dynamic volume profiles configuration and refresh volumes immediately.
        /// </summary>
        [ExecutableFunction]
        public void ApplyDynamicVolumeProfiles()
        {
            ApplyVolumeProfiles();
        }

        private static void ApplyVolumeProfile(Volume volume, VolumeProfile profile, float priority)
        {
            if (volume.profile != profile)
            {
                volume.profile = profile;
            }

            volume.priority = priority;
        }

        internal static bool IsLookDevVolumeType(string volumeType)
        {
            if (!Enum.TryParse<BuiltInVolumeType>(volumeType, out var type))
            {
                return false;
            }
            
            if (type == BuiltInVolumeType.SubsurfaceScattering) return true;
            return type == BuiltInVolumeType.Tonemapping;
        }

        private void SetRenderScale(int presetId)
        {
            if (presetId >= 0 && presetId < GraphicsConfig.RenderScalePresets.Length)
            {
                _urpAsset.renderScale = GraphicsConfig.RenderScalePresets[presetId];
            }
        }

        /// <summary>
        /// Get Volume by id
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        [ExecutableFunction]
        public Volume GetVolume(string volumeId)
        {
            if (!_volumes.Any())
            {
                PrepareDynamicVolumes();
            }
            return _volumes[volumeId];
        }

        [Conditional("UNITY_EDITOR")]
        internal void InitializeIfNeed()
        {
#if UNITY_EDITOR
            if (!gameObject.scene.IsValid()) return;
#endif
            if (!_volumes.Any())
            {
                PrepareDynamicVolumes();
                InitializeGraphics();
            }
        }
        
        private bool IsVolumeSupport(BuiltInVolumeType builtInBuiltInVolumeType)
        {
            if (builtInBuiltInVolumeType == BuiltInVolumeType.DepthOfField)
            {
                return settingsAsset.IsFeatureSupport(GraphicsFeatures.DepthOfField) && Application.isPlaying;
            }
            
            if (builtInBuiltInVolumeType == BuiltInVolumeType.MotionBlur)
            {
                return settingsAsset.IsFeatureSupport(GraphicsFeatures.MotionBlur) && Application.isPlaying;
            }
            
            if (builtInBuiltInVolumeType == BuiltInVolumeType.ScreenSpaceReflection)
            {
                return settingsAsset.IsFeatureSupport(GraphicsFeatures.ScreenSpaceReflection);
            }
            
            if (builtInBuiltInVolumeType == BuiltInVolumeType.ScreenSpaceGlobalIllumination)
            {
                return settingsAsset.IsFeatureSupport(GraphicsFeatures.ScreenSpaceGlobalIllumination);
            }
            
            if (builtInBuiltInVolumeType == BuiltInVolumeType.ScreenSpaceAmbientOcclusion)
            {
                return settingsAsset.IsFeatureSupport(GraphicsFeatures.ScreenSpaceAmbientOcclusion);
            }

            return true;
        }
        
        private class BuiltInVolumeObserver : Observer<bool>
        {
            private readonly string _volumeType;

            private readonly GraphicsController _graphicsController;
            
            public BuiltInVolumeObserver(BuiltInVolumeType volumeType, GraphicsController graphicsController)
            {
                _volumeType = volumeType.ToString();
                _graphicsController = graphicsController;
            }

            protected override void OnNextCore(bool value)
            {
                _graphicsController.GetVolume(_volumeType).weight = value ? 1 : 0;
            }

            protected override void OnErrorResumeCore(Exception error)
            {
                
            }

            protected override void OnCompletedCore(Result result)
            {
               
            }
        }
        
#if ILLUSION_RP_INSTALL
        // For IllusionRP features, we can disable them directly.
        private static void SetConvolutionBloomEnabled(bool isEnabled)
        {
            IllusionRuntimeRenderingConfig.Get().EnableConvolutionBloom = isEnabled;
        }
        
        private static void SetScreenSpaceAmbientOcclusionEnabled(bool isEnabled)
        {
            IllusionRuntimeRenderingConfig.Get().EnableScreenSpaceAmbientOcclusion = isEnabled;
        }
        
        private static void SetContactShadowsEnabled(bool isEnabled)
        {
            IllusionRuntimeRenderingConfig.Get().EnableContactShadows = isEnabled;
        }
        
        private static void SetPercentageCloserSoftShadowsEnabled(bool isEnabled)
        {
            IllusionRuntimeRenderingConfig.Get().EnablePercentageCloserSoftShadows = isEnabled;
        }
        
        private static void SetScreenSpaceReflection(bool isEnabled)
        {
            IllusionRuntimeRenderingConfig.Get().EnableScreenSpaceReflection = isEnabled;
        }
        
        private static void SetScreenSpaceGlobalIllumination(bool isEnabled)
        {
            IllusionRuntimeRenderingConfig.Get().EnableScreenSpaceGlobalIllumination = isEnabled;
        }
        
        private static void SetVolumetricFog(bool isEnabled)
        {
            IllusionRuntimeRenderingConfig.Get().EnableVolumetricFog = isEnabled;
        }
#endif
    }
}
#endif