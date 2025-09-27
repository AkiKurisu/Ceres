using System.IO;
using System.Linq;
using Chris.Editor;
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
            var preservedTypes = CeresSettings.GetPreservedTypes();
            _linker.AddTypes(preservedTypes.Select(SerializedType.FromString));
            if (!Directory.Exists(CeresDirectory))
            {
                Directory.CreateDirectory(CeresDirectory);
            }

            _linker.Save(XMLPath);
        }

        protected override void PostprocessBuild(BuildReport report)
        {
            if (File.Exists(XMLPath))
            {
                File.Delete(XMLPath);
                File.Delete(XMLPath + ".meta");
            }

            if (Directory.Exists(CeresDirectory))
            {
                Directory.Delete(CeresDirectory);
                File.Delete(CeresDirectory + ".meta");
            }
        }
    }
}
