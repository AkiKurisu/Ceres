using System.IO;
using System.Linq;
using Ceres.Editor;
using Ceres.Resource.Editor;
using Ceres.Serialization;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ceres.Editor.Graph
{
    public class CeresBuildProcessor : BuildProcessorWithReport
    {
        private readonly LinkXmlGenerator _linker = LinkXmlGenerator.CreateDefault();

        private static readonly string CeresDirectory = Path.Combine(Application.dataPath, "Ceres");

        private static readonly string XMLPath = Path.Combine(CeresDirectory, "link.xml");

        protected override void PreprocessBuild(BuildReport report)
        {
            Directory.CreateDirectory(CeresDirectory);
            _linker.AddTypes(CeresGraphSettings.GetPreservedTypes().Select(SerializedType.FromString));
            _linker.Save(XMLPath);
        }

        protected override void PostprocessBuild(BuildReport report)
        {
            ResourceEditorUtils.DeleteAsset(XMLPath);
            ResourceEditorUtils.DeleteDirectory(CeresDirectory);
        }
    }
}
