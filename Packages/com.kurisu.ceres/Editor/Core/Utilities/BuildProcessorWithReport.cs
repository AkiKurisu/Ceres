using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Ceres.Editor
{
    public abstract class BuildProcessorWithReport : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public virtual int callbackOrder { get; }

        private bool _hasPostProcessed;
        
        void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
        {
            PreprocessBuild(report);
            // PostprocessBuild will not be called when build cancelled or failed.
            // https://discussions.unity.com/t/ipostprocessbuildwithreport-and-qa-embarrasing-answer-about-a-serious-bug/791031/9
            EditorApplication.update += CheckBuildProcess;
        }
        
        protected virtual void PreprocessBuild(BuildReport report)
        {
            
        }
        
        void IPostprocessBuildWithReport.OnPostprocessBuild(BuildReport report)
        {
            if (_hasPostProcessed) return;
            _hasPostProcessed = true;
            PostprocessBuild(report);
        }
        
        private void CheckBuildProcess()
        {
            if (!BuildPipeline.isBuildingPlayer && !_hasPostProcessed)
            {
                EditorApplication.update -= CheckBuildProcess;
                _hasPostProcessed = true;
                PostprocessBuild(null);
            }
        }

        protected virtual void PostprocessBuild(BuildReport report)
        {
            
        }
    }
}