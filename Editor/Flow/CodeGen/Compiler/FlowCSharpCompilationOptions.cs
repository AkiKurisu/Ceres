using Ceres.Graph.Flow;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal readonly struct FlowCSharpCompilationOptions
    {
        public readonly FlowGeneratedRuntimeProfile Profile;

        public readonly FlowGeneratedRuntimeCancellationMode CancellationMode;

        public readonly FlowGeneratedRuntimeVariableStorageMode VariableStorageMode;

        public readonly FlowGeneratedRuntimeSerializedTypeMode SerializedTypeMode;

        public FlowCSharpCompilationOptions(FlowGeneratedRuntimeProfile profile,
            FlowGeneratedRuntimeCancellationMode cancellationMode,
            FlowGeneratedRuntimeVariableStorageMode variableStorageMode,
            FlowGeneratedRuntimeSerializedTypeMode serializedTypeMode)
        {
            Profile = profile;
            CancellationMode = cancellationMode;
            VariableStorageMode = variableStorageMode;
            SerializedTypeMode = serializedTypeMode;
        }

        public static FlowCSharpCompilationOptions Current()
        {
            return new FlowCSharpCompilationOptions(
                FlowGeneratedRuntimeUtility.CurrentGeneratedRuntimeProfile,
                FlowGeneratedRuntimeUtility.CurrentGeneratedRuntimeCancellationMode,
                FlowGeneratedRuntimeUtility.CurrentGeneratedRuntimeVariableStorageMode,
                FlowGeneratedRuntimeUtility.CurrentGeneratedRuntimeSerializedTypeMode);
        }

        public bool UsesGeneratedRuntimeProgram =>
            Profile == FlowGeneratedRuntimeProfile.OptimizedSafe ||
            Profile == FlowGeneratedRuntimeProfile.OptimizedAggressive;

        public bool IsAggressive => Profile == FlowGeneratedRuntimeProfile.OptimizedAggressive;
    }
}
