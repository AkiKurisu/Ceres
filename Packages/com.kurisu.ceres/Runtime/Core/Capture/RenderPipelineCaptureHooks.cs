using UnityEngine;

namespace Ceres.Capture
{
    public readonly struct TemporalCaptureStatus
    {
        public readonly bool HasCameraState;

        public readonly uint FrameCount;

        public readonly int RecommendedWarmupFrames;

        public readonly bool IsReady;

        public readonly string Blockers;

        public TemporalCaptureStatus(bool hasCameraState, uint frameCount, int recommendedWarmupFrames, bool isReady, string blockers)
        {
            HasCameraState = hasCameraState;
            FrameCount = frameCount;
            RecommendedWarmupFrames = recommendedWarmupFrames;
            IsReady = isReady;
            Blockers = blockers;
        }
    }

    public interface IRenderPipelineCaptureHooks
    {
        bool TryGetTemporalCaptureStatus(Camera camera, out TemporalCaptureStatus status);

        void CopyCameraData(Camera source, Camera target);
    }

    public static class RenderPipelineCaptureHooks
    {
        public static IRenderPipelineCaptureHooks Current { get; set; }
    }
}
