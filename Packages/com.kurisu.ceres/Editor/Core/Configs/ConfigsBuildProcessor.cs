using System.IO;
using Ceres.Editor;
using Ceres.Resource.Editor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ceres.Configs.Editor
{
    internal class ConfigsBuildProcessor : BuildProcessorWithReport
    {
        private bool _isStreamingAssetsPathEmpty;
        
        protected override void PreprocessBuild(BuildReport report)
        {
            _isStreamingAssetsPathEmpty = false;
            if (!Directory.Exists(Application.streamingAssetsPath))
            {
                _isStreamingAssetsPathEmpty = true;
                Directory.CreateDirectory(Application.streamingAssetsPath);
            }
            ConfigsEditorUtils.ExportAndArchiveConfigs();
        }
        
        protected override void PostprocessBuild(BuildReport report)
        {
            if (_isStreamingAssetsPathEmpty)
            {
                ResourceEditorUtils.DeleteDirectory(Application.streamingAssetsPath);
            }
        }
    }
}
