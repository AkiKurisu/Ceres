using UnityEditor.AddressableAssets.Build;

namespace Ceres.Resource.Editor
{
    public sealed class ResourceExportResult
    {
        public string BuildPath { get; internal set; }

        public string ZipPath { get; internal set; }

        public string Error { get; internal set; }

        public AddressablesPlayerBuildResult AddressablesResult { get; internal set; }

        public bool Succeeded => string.IsNullOrEmpty(Error);
    }
}
