using System;
using Cysharp.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ILLUSION_RP_INSTALL
using Illusion.Rendering;
using Illusion.Rendering.Shadows;
#endif
#if URP_INSTALL
using UnityEngine.Rendering.Universal;
#endif
using UGraphics = UnityEngine.Graphics;

namespace Ceres.Gameplay.Capture
{
    public enum ScreenshotMode
    {
        /// <summary>
        /// Take raw screenshot from camera.
        /// </summary>
        Camera,
        /// <summary>
        /// Take screenshot from current screen color buffer.
        /// </summary>
        Screen
    }
    
    public struct ScreenshotRequest
    {
        /// <summary>
        /// Whether capture raw frame from renderer
        /// </summary>
        public ScreenshotMode Mode;
        
        /// <summary>
        /// Capture camera used when enable <see cref="ScreenshotMode.Camera"/>
        /// </summary>
        public Camera Camera;

        /// <summary>
        /// Define capture destination
        /// </summary>
        public RenderTexture Destination;

        /// <summary>
        /// Define minimum camera capture warmup frames.
        /// </summary>
        public int DelayFrames;

        /// <summary>
        /// Maximum camera capture warmup frames.
        /// </summary>
        public int MaxWarmupFrames;
    }
    
    public static class ScreenshotUtility
    {
#if UNITY_EDITOR
        /// <summary>
        /// Pumps an Editor update so camera warmup can progress when the Game view is not repainting.
        /// </summary>
        private static UniTask WaitForNextEditorTickAsync()
        {
            if (Application.isBatchMode)
            {
                return UniTask.CompletedTask;
            }

            var completionSource = new UniTaskCompletionSource();

            void OnUpdate()
            {
                EditorApplication.update -= OnUpdate;
                completionSource.TrySetResult();
            }

            EditorApplication.update += OnUpdate;
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            return completionSource.Task;
        }
#endif

        [BurstCompile]
        private struct LinearToGammaConvertJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<float> Data;

            public void Execute(int index)
            {
                Data[index] = Mathf.LinearToGammaSpace(Data[index]);
            }
        }
        
        public abstract class ScreenshotHandler : IDisposable
        {
            protected RenderTexture RenderTarget { get; }

            protected ScreenshotHandler(RenderTexture renderTarget)
            {
                RenderTarget = renderTarget;
            }
            
            public abstract void Execute();

            public virtual async UniTask ExecuteAsync(ScreenshotRequest request)
            {
                Execute();
                await UniTask.WaitForEndOfFrame();
                if (request.DelayFrames > 0)
                {
                    await UniTask.DelayFrame(request.DelayFrames, PlayerLoopTiming.PostLateUpdate);
                }
            }

            public abstract void Dispose();
        }

        private sealed class CameraScreenshotHandler : ScreenshotHandler
        {
            private readonly Camera _camera;
            
            private readonly bool _async;

            private Camera _renderCamera;

            private GameObject _captureCameraObject;

            private static bool UseEditorManualRenderLoop
            {
                get
                {
#if UNITY_EDITOR
                    // In the Editor, WaitForEndOfFrame can stall outside the Game view.
                    return Application.isEditor;
#else
                    return false;
#endif
                }
            }

            public CameraScreenshotHandler(Camera camera, RenderTexture destination, bool async)
                : base(destination)
            {
                _camera = camera;
                _async = async;
            }

            public override void Execute()
            {
                _renderCamera = CreateIsolatedCamera(_camera);
                SyncRenderCamera();
                if (!_async)
                {
                    _renderCamera.Render();
                }
            }

            public override async UniTask ExecuteAsync(ScreenshotRequest request)
            {
                Execute();

                int waitedFrames = 0;
                int fixedDelayFrames = Mathf.Max(0, request.DelayFrames);
                int maxWarmupFrames = Mathf.Max(fixedDelayFrames, request.MaxWarmupFrames > 0 ? request.MaxWarmupFrames : 32);
                string lastBlockers = "None";

                while (true)
                {
                    SyncRenderCamera();
                    await RenderWarmupFrameAsync();
                    waitedFrames++;

                    if (IsStableTemporalReady(request, waitedFrames, out lastBlockers))
                    {
                        break;
                    }

                    if (waitedFrames >= maxWarmupFrames)
                    {
                        Debug.LogWarning($"Screenshot camera capture reached max warmup frames ({maxWarmupFrames}). Last blockers: {lastBlockers}.");
                        break;
                    }
                }
            }

            public override void Dispose()
            {
                if (_renderCamera && _renderCamera.targetTexture == RenderTarget)
                {
                    _renderCamera.targetTexture = null;
                }

                if (_captureCameraObject)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(_captureCameraObject);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(_captureCameraObject);
                    }

                    _captureCameraObject = null;
                }

                _renderCamera = null;
            }

            private void SyncRenderCamera()
            {
                if (!_renderCamera)
                {
                    return;
                }

                _renderCamera.CopyFrom(_camera);
                _renderCamera.cameraType = CameraType.Game;
                // Editor warmup renders explicitly via Camera.Render to avoid waiting on Game view repaint.
                _renderCamera.enabled = !UseEditorManualRenderLoop;
                _renderCamera.transform.SetPositionAndRotation(_camera.transform.position, _camera.transform.rotation);
                CopyRenderPipelineCameraData(_camera, _renderCamera);

                _renderCamera.targetTexture = RenderTarget;
            }

            private Camera CreateIsolatedCamera(Camera source)
            {
                var captureObject = new GameObject($"{source.name} Screenshot Camera")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _captureCameraObject = captureObject;
                var camera = captureObject.AddComponent<Camera>();
                camera.CopyFrom(source);
                camera.cameraType = CameraType.Game;
                camera.enabled = !UseEditorManualRenderLoop;
                camera.targetTexture = RenderTarget;
                CopyRenderPipelineCameraData(source, camera);
                return camera;
            }

            private async UniTask RenderWarmupFrameAsync()
            {
#if UNITY_EDITOR
                if (UseEditorManualRenderLoop)
                {
                    // Drive one editor update, then render the hidden camera once into the capture target.
                    await WaitForNextEditorTickAsync();
                    SyncRenderCamera();
                    _renderCamera.Render();
                    return;
                }
#endif
                await UniTask.WaitForEndOfFrame();
            }

            private bool IsStableTemporalReady(ScreenshotRequest request, int waitedFrames, out string blockers)
            {
                blockers = "None";
                if (waitedFrames < Mathf.Max(0, request.DelayFrames))
                {
                    blockers = "WarmingUp";
                    return false;
                }

#if ILLUSION_RP_INSTALL
                if (IllusionRendererData.Active != null
                    && IllusionRendererData.Active.TryGetTemporalCaptureStatus(_renderCamera, out var status))
                {
                    var statusBlockers = status.Blockers;
                    if (status.FrameCount < Mathf.Max(status.RecommendedWarmupFrames, request.DelayFrames))
                    {
                        statusBlockers |= IllusionTemporalCaptureBlockers.WarmingUp;
                    }
                    blockers = statusBlockers.ToString();
                    return statusBlockers == IllusionTemporalCaptureBlockers.None;
                }
#endif
                return waitedFrames >= Mathf.Max(1, request.DelayFrames);
            }

            private static void CopyRenderPipelineCameraData(Camera source, Camera target)
            {
                CopyUniversalAdditionalCameraData(source, target);
#if ILLUSION_RP_INSTALL
                CopyPerObjectShadowLightSource(source, target);
#endif
            }

#if ILLUSION_RP_INSTALL
            private static void CopyPerObjectShadowLightSource(Camera source, Camera target)
            {
                var sourceData = source.GetComponent<PerObjectShadowLightSource>();
                var targetData = target.GetComponent<PerObjectShadowLightSource>();
                if (!sourceData)
                {
                    if (targetData)
                    {
                        targetData.Source = null;
                        targetData.enabled = false;
                    }
                    return;
                }

                if (!targetData)
                {
                    targetData = target.gameObject.AddComponent<PerObjectShadowLightSource>();
                }

                targetData.Source = sourceData.Source;
                targetData.enabled = sourceData.isActiveAndEnabled;
            }
#endif

#if URP_INSTALL
            private static void CopyUniversalAdditionalCameraData(Camera source, Camera target)
            {
                if (!source.TryGetComponent(out UniversalAdditionalCameraData sourceData))
                {
                    return;
                }

                var targetData = target.GetComponent<UniversalAdditionalCameraData>();
                if (!targetData)
                {
                    targetData = target.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }

                targetData.renderShadows = sourceData.renderShadows;
                targetData.requiresDepthOption = sourceData.requiresDepthOption;
                targetData.requiresColorOption = sourceData.requiresColorOption;
                targetData.renderType = CameraRenderType.Base;
                targetData.cameraStack?.Clear();
                targetData.volumeLayerMask = sourceData.volumeLayerMask;
                targetData.volumeTrigger = sourceData.volumeTrigger ? sourceData.volumeTrigger : source.transform;
                targetData.renderPostProcessing = sourceData.renderPostProcessing;
                targetData.antialiasing = sourceData.antialiasing;
                targetData.antialiasingQuality = sourceData.antialiasingQuality;
                targetData.stopNaN = sourceData.stopNaN;
                targetData.dithering = sourceData.dithering;
                targetData.allowXRRendering = false;
                targetData.allowHDROutput = sourceData.allowHDROutput;
                targetData.useScreenCoordOverride = sourceData.useScreenCoordOverride;
                targetData.screenSizeOverride = sourceData.screenSizeOverride;
                targetData.screenCoordScaleBias = sourceData.screenCoordScaleBias;
                CopyTemporalAASettings(sourceData, targetData);
                CopyRenderer(sourceData, targetData);
            }

            private static void CopyTemporalAASettings(UniversalAdditionalCameraData sourceData,
                UniversalAdditionalCameraData targetData)
            {
                ref var sourceSettings = ref sourceData.taaSettings;
                ref var targetSettings = ref targetData.taaSettings;
                targetSettings.quality = sourceSettings.quality;
                targetSettings.baseBlendFactor = sourceSettings.baseBlendFactor;
                targetSettings.jitterScale = sourceSettings.jitterScale;
                targetSettings.mipBias = sourceSettings.mipBias;
                targetSettings.varianceClampScale = sourceSettings.varianceClampScale;
                targetSettings.contrastAdaptiveSharpening = sourceSettings.contrastAdaptiveSharpening;
            }

            private static void CopyRenderer(UniversalAdditionalCameraData sourceData,
                UniversalAdditionalCameraData targetData)
            {
                var asset = UniversalRenderPipeline.asset;
                var renderer = sourceData.scriptableRenderer;
                if (!asset || renderer == null)
                {
                    return;
                }

                var renderers = asset.renderers;
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == renderer)
                    {
                        targetData.SetRenderer(i);
                        return;
                    }
                }
            }
#else
            private static void CopyUniversalAdditionalCameraData(Camera source, Camera target)
            {
                // No URP camera data to copy.
            }
#endif
        }

        private sealed class ScreenScreenshotHandler : ScreenshotHandler
        {
            private RenderTexture _scratch;
            
            private bool _disposed;

            public ScreenScreenshotHandler(RenderTexture destination): base(destination)
            {

            }

            public override void Execute()
            {
                var screenSize = GameViewUtils.GetSizeOfMainGameView();
                _scratch = RenderTexture.GetTemporary((int)screenSize.x, (int)screenSize.y,
                    0, RenderTextureFormat.ARGB32);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(_scratch);
                UGraphics.Blit(_scratch, RenderTarget, new Vector2(1f, -1f), new Vector2(0.0f, 1f)); // Flip in DX12 and Vulkan
            }

            public override async UniTask ExecuteAsync(ScreenshotRequest request)
            {
                await UniTask.WaitForEndOfFrame();
                Execute();
            }

            public override void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                if (_scratch)
                {
                    RenderTexture.ReleaseTemporary(_scratch);
                    _scratch = null;
                }
            }
        }

        private static ScreenshotHandler CreateHandler(ScreenshotRequest request, bool async)
        {
            if (request.Mode == ScreenshotMode.Camera)
            {
                return new CameraScreenshotHandler(request.Camera, request.Destination, async);
            }

            return new ScreenScreenshotHandler(request.Destination);
        }
        
        public static ScreenshotHandler CaptureScreenshot(ScreenshotRequest request)
        {
            var destination = request.Destination;
            Assert.IsTrue((bool)destination);
            var handler = CreateHandler(request, false);
            handler.Execute();
            return handler;
        }
        
        public static async UniTask<ScreenshotHandler> CaptureScreenshotAsync(ScreenshotRequest request)
        {
            var destination = request.Destination;
            Assert.IsTrue((bool)destination);
            var handler = CreateHandler(request, true);
            await handler.ExecuteAsync(request);
            return handler;
        }

        public static Texture2D CaptureActiveRenderTexture(int width, int height, TextureFormat format = TextureFormat.RGBA32)
        {
            Texture2D destination = new Texture2D(width, height, format, false);
            Rect rect = new Rect(0, 0, width, height);
            destination.ReadPixels(rect, 0, 0, false);
            return destination;
        }

        private static bool IsHDR(RenderTextureFormat renderTextureFormat)
        {
            return renderTextureFormat is RenderTextureFormat.ARGBHalf or RenderTextureFormat.ARGBFloat;
        }

        public static TextureFormat GetTextureFormat(RenderTextureFormat renderTextureFormat)
        {
            if (renderTextureFormat == RenderTextureFormat.ARGBHalf)
            {
                return TextureFormat.RGBAHalf;
            }
            
            if (renderTextureFormat == RenderTextureFormat.ARGBFloat)
            {
                return TextureFormat.RGBAFloat;
            }

            return TextureFormat.ARGB32;
        }

        private static void LinearToGamma(Texture2D texture2D)
        {
            var rawData = texture2D.GetRawTextureData<float>();
            var job = new LinearToGammaConvertJob
            {
                Data = rawData
            };
            JobHandle handle = job.Schedule(rawData.Length, 64);
            handle.Complete();
        }
        
        private static async UniTask LinearToGammaAsync(Texture2D texture2D)
        {
            var rawData = texture2D.GetRawTextureData<float>();
            var job = new LinearToGammaConvertJob
            {
                Data = rawData
            };
            var handle = job.Schedule(rawData.Length, 64);
            await handle.ToUniTask(PlayerLoopTiming.LastPostLateUpdate);
            handle.Complete();
        }
        
        public static Texture2D ToTexture2D(this RenderTexture renderTexture)
        {
            RenderTexture original = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var destination = CaptureActiveRenderTexture(renderTexture.width, renderTexture.height, GetTextureFormat(renderTexture.format));
            RenderTexture.active = original;
            if (IsHDR(renderTexture.format))
            {
                LinearToGamma(destination);
            }
            return destination;
        }
        
        public static void ToTexture2DAsync(this RenderTexture renderTexture, Action<Texture2D> callback)
        {
            Assert.IsNotNull(callback);
            var textureFormat = GetTextureFormat(renderTexture.format);
            var destination = new Texture2D(renderTexture.width, renderTexture.height, textureFormat, false);

            AsyncGPUReadback.Request(renderTexture, 0, 
                0, destination.width, 0, destination.height, 0, 1, textureFormat, request => ReadbackAsync(request).Forget());
            return;

            async UniTask ReadbackAsync(AsyncGPUReadbackRequest request)
            {
                var rawData = request.GetData<byte>();
                var processedData = destination.GetRawTextureData<byte>();
                var slice = new NativeSlice<byte>(processedData, 0, rawData.Length);
                slice.CopyFrom(rawData);
                if (IsHDR(renderTexture.format))
                {
                    await LinearToGammaAsync(destination);
                }
                callback.Invoke(destination);
            }
        }
        
                
        /// <summary>
        /// Capture raw screenshot from renderer to a new <see cref="Texture2D"/>.
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="size"></param>
        /// <param name="depthBuffer"></param>
        /// <param name="renderTextureFormat"></param>
        /// <returns></returns>
        public static Texture2D CaptureRawScreenshot(Camera camera, Vector2 size, 
            int depthBuffer = 24, RenderTextureFormat renderTextureFormat = RenderTextureFormat.ARGB32)
        {
            int antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing);
            var screenTexture = RenderTexture.GetTemporary((int)size.x, (int)size.y, 
                depthBuffer, renderTextureFormat, RenderTextureReadWrite.Default, antiAliasing);
            var handler = CaptureScreenshot(new ScreenshotRequest
            {
                Camera = camera,
                Destination = screenTexture,
                Mode = ScreenshotMode.Camera
            });
            try
            {
                return screenTexture.ToTexture2D();
            }
            finally
            {
                handler.Dispose();
                RenderTexture.ReleaseTemporary(screenTexture);
            }
        }

        /// <summary>
        /// Capture raw screenshot from renderer to a new <see cref="Texture2D"/> in async.
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="size"></param>
        /// <param name="depthBuffer"></param>
        /// <param name="renderTextureFormat"></param>
        /// <param name="delayFrames"></param>
        /// <param name="onComplete"></param>
        /// <param name="maxWarmupFrames"></param>
        /// <returns></returns>
        public static async UniTask CaptureRawScreenshotAsync(Camera camera, Vector2 size, 
            int depthBuffer = 24, RenderTextureFormat renderTextureFormat = RenderTextureFormat.ARGB32, int delayFrames = 1,
            Action<Texture2D> onComplete = null, int maxWarmupFrames = 32)
        {
            int antiAliasing = 1;
            var screenTexture = RenderTexture.GetTemporary((int)size.x, (int)size.y, 
                depthBuffer, renderTextureFormat, RenderTextureReadWrite.Default, antiAliasing);
            var handler = await CaptureScreenshotAsync(new ScreenshotRequest
            {
                Camera = camera,
                Destination = screenTexture,
                Mode = ScreenshotMode.Camera,
                DelayFrames = delayFrames,
                MaxWarmupFrames = maxWarmupFrames
            });
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                try
                {
                    var result = screenTexture.ToTexture2D();
                    onComplete?.Invoke(result);
                }
                finally
                {
                    handler.Dispose();
                    RenderTexture.ReleaseTemporary(screenTexture);
                }
                return;
            }
#endif
            screenTexture.ToTexture2DAsync(result =>
            {
                onComplete?.Invoke(result);
                handler.Dispose();
                RenderTexture.ReleaseTemporary(screenTexture);
            });
        }
        
        /// <summary>
        /// Capture screenshot to a new <see cref="Texture2D"/>. Need be called at the end of frame.
        /// </summary>
        /// <returns></returns>
        public static Texture2D CaptureScreenShotFromScreen()
        {
            var screenSize = GameViewUtils.GetSizeOfMainGameView();
#if UNITY_EDITOR
            return CaptureActiveRenderTexture((int)screenSize.x, (int)screenSize.y);
#else
            int antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing);
            var screenTexture = RenderTexture.GetTemporary((int)screenSize.x, (int)screenSize.y, 
                24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, antiAliasing);
            var handler = CaptureScreenshot(new ScreenshotRequest
            {
                Destination = screenTexture,
                Mode = ScreenshotMode.Screen
            });
            try
            {
                return screenTexture.ToTexture2D();
            }
            finally
            {
                handler.Dispose();
                RenderTexture.ReleaseTemporary(screenTexture);
            }
#endif
        }
        
        /// <summary>
        /// Capture screenshot to a new <see cref="Texture2D"/> in async. Need be called at the end of frame.
        /// </summary>
        /// <param name="onComplete"></param>
        public static async UniTask CaptureScreenshotAsync(Action<Texture2D> onComplete)
        {
            Assert.IsNotNull(onComplete);
            var screenSize = GameViewUtils.GetSizeOfMainGameView();
#if UNITY_EDITOR
            onComplete(CaptureActiveRenderTexture((int)screenSize.x, (int)screenSize.y));
            await UniTask.Yield();
#else
            int antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing);
            var screenTexture = RenderTexture.GetTemporary((int)screenSize.x, (int)screenSize.y, 
                24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, antiAliasing);
            var handler = await CaptureScreenshotAsync(new ScreenshotRequest
            {
                Destination = screenTexture,
                Mode = ScreenshotMode.Screen
            });
            screenTexture.ToTexture2DAsync(result =>
            {
                onComplete.Invoke(result);
                handler.Dispose();
                RenderTexture.ReleaseTemporary(screenTexture);
            });
#endif
        }
    }
}
