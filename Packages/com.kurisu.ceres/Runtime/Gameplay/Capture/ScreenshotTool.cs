using System.Collections;
using Ceres.Flow;
using Ceres.Flow.Annotations;
using Ceres.RuntimeConsole;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.Scripting;
using UObject = UnityEngine.Object;

namespace Ceres.Gameplay.Capture
{
    public sealed class ScreenshotTool : 
        FlowGraphObject
    {
        [Range(1, 4)]
        [SerializeField]
        private int superSize = 1;

        public int SuperSize
        {
            get => superSize;
            set => superSize = Mathf.Clamp(value, 1, 4);
        }

        [SerializeField] 
        private Camera sourceCamera;

        public Camera SourceCamera
        {
            get => sourceCamera;
            set => sourceCamera = value;
        }

        [SerializeField] 
        private ScreenshotMode screenshotMode;

        public ScreenshotMode ScreenshotMode
        {
            get => screenshotMode;
            set => screenshotMode = value;
        }

        [SerializeField] 
        private bool enableHDR = true;

        public bool EnableHDR
        {
            get => enableHDR;
            set => enableHDR = value;
        }
        
        [Range(1, 30)]
        [SerializeField] 
        private int delayFrames = 1;

        public int DelayFrames
        {
            get => delayFrames;
            set => delayFrames = value;
        }

        [Range(1, 120)]
        [SerializeField]
        private int maxWarmupFrames = 32;

        public int MaxWarmupFrames
        {
            get => maxWarmupFrames;
            set => maxWarmupFrames = Mathf.Clamp(value, 1, 120);
        }

        private Texture2D _captureTex;

#if UNITY_EDITOR
        [SerializeField] 
        private bool openFolderAfterCapture = true;
#endif
        
        private static readonly Subject<Unit> OnScreenshotStartSubject = new();
        
        private static readonly Subject<Unit> OnScreenshotEndSubject = new();
        
        /// <summary>
        /// Fired on any screenshot start.
        /// </summary>
        public static Observable<Unit> OnScreenshotStart => OnScreenshotStartSubject;

        /// <summary>
        /// Fired on any screenshot end.
        /// </summary>
        public static Observable<Unit> OnScreenshotEnd => OnScreenshotEndSubject;

        private void OnDestroy()
        {
            DestroySafe(_captureTex);
        }

        private Camera GetCamera()
        {
            if (!sourceCamera) sourceCamera = Camera.main;
            return sourceCamera;
        }

        private IEnumerator TakeScreenshotCoroutine()
        {
            DestroySafe(_captureTex);

            // Can modify settings here
            OnTakeScreenshotStart();
            
            // Capture
            if (ScreenshotMode == ScreenshotMode.Screen)
            {
                yield return new WaitForEndOfFrame();
                ScreenshotUtility.CaptureScreenshotAsync(ProcessPicture).Forget();
            }
            else
            {
                var screenSize = GameViewUtils.GetSizeOfMainGameView() * SuperSize;
                ScreenshotUtility.CaptureRawScreenshotAsync(GetCamera(), screenSize,
                    renderTextureFormat: EnableHDR ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGB32,
                    delayFrames: DelayFrames,
                    onComplete: ProcessPicture,
                    maxWarmupFrames: MaxWarmupFrames).Forget();
            }
        }

        private void ProcessPicture(Texture2D target)
        {
            // Encode
            _captureTex = target;
            var byteArray = target.EncodeToPNG();
            GalleryUtility.SavePngToGallery(byteArray);

            OnTakeScreenshotEnd();

#if UNITY_EDITOR
            if (openFolderAfterCapture && Application.isEditor)
            {
                System.Diagnostics.Process.Start(GalleryUtility.SnapshotFolderPath);
            }

            DestroySafe(_captureTex);
            _captureTex = null;
#endif
        }

        private static void DestroySafe(UObject uObject)
        {
            if (!uObject)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(uObject);
            }
            else
            {
                DestroyImmediate(uObject);
            }
        }

        /// <summary>
        /// Take a screenshot by tool current settings.
        /// </summary>
        [ExecutableFunction]
        public void TakeScreenshot()
        {
            StartCoroutine(TakeScreenshotCoroutine());
        }

        /// <summary>
        /// Get last screenshot if it exists.
        /// </summary>
        /// <returns></returns>
        [ExecutableFunction]
        public Texture2D GetLastScreenshot()
        {
            return _captureTex;
        }

        /// <summary>
        /// Process before taken screenshot
        /// </summary>
        [ImplementableEvent]
        public void OnTakeScreenshotStart()
        {
            OnScreenshotStartSubject.OnNext(Unit.Default);
        }

        /// <summary>
        /// Process after taken screenshot
        /// </summary>
        [ImplementableEvent]
        public void OnTakeScreenshotEnd()
        {
            OnScreenshotEndSubject.OnNext(Unit.Default);
        }
        
        [Preserve]
        public static class ScreenshotCommands
        {
            [Preserve]
            [ConsoleMethod("screenshot", "Take a screenshot")]
            public static void TakeScreenshot()
            {
                TakeScreenshot(false, 1);
            }

            [Preserve]
            [ConsoleMethod("screenshot", "Take a screenshot with more settings. " +
                                             "includeUI: Whether to include overlay ui; " +
                                             "superSize: Image supersize when `includeUI` is off.")]
            public static void TakeScreenshot(bool includeUI, int superSize)
            {
                var tool = new GameObject().AddComponent<ScreenshotTool>();
                tool.superSize = superSize;
                tool.screenshotMode = includeUI ? ScreenshotMode.Screen : ScreenshotMode.Camera;
                tool.SetGraphData(new FlowGraphData()); // Empty graph
                tool.TakeScreenshot();
                OnScreenshotEnd.DelayFrame(1).Subscribe(_ => Destroy(tool.gameObject)).AddTo(tool);
            }
        }
    }
}
