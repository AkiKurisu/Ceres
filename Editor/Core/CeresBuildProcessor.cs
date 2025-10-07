using System.IO;
using System.Linq;
using Chris.Editor;
using Chris.Resource.Editor;
using Chris.Serialization;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ceres.Editor
{
    public class CeresBuildProcessor : BuildProcessorWithReport
    {
        private readonly LinkXmlGenerator _linker = LinkXmlGenerator.CreateDefault();

        private static readonly string CeresDirectory = Path.Combine(Application.dataPath, "Ceres");

        private static readonly string XMLPath = Path.Combine(CeresDirectory, "link.xml");

        protected override void PreprocessBuild(BuildReport report)
        {
            Directory.CreateDirectory(CeresDirectory);
            _linker.AddTypes(CeresSettings.GetPreservedTypes().Select(SerializedType.FromString));
            _linker.Save(XMLPath);
        }

        protected override void PostprocessBuild(BuildReport report)
        {
            ResourceEditorUtils.DeleteAsset(XMLPath);
            ResourceEditorUtils.DeleteDirectory(CeresDirectory);
        }
    }
}
