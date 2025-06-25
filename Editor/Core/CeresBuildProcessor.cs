using System.IO;
using System.Linq;
using Chris.Serialization;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ceres.Editor
{
    public class CeresBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder { get; }

        private readonly LinkXmlGenerator _linker = LinkXmlGenerator.CreateDefault();

        private static readonly string CeresDirectory = Path.Combine(Application.dataPath, "Ceres");
        
        private static readonly string XMLPath = Path.Combine(CeresDirectory, "link.xml");

        public void OnPostprocessBuild(BuildReport report)
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

        public void OnPreprocessBuild(BuildReport report)
        {
            var preservedTypes = CeresSettings.GetPreservedTypes();
            _linker.AddTypes(preservedTypes.Select(SerializedType.FromString));
            if (!Directory.Exists(CeresDirectory))
            {
                Directory.CreateDirectory(CeresDirectory);
            }
            _linker.Save(XMLPath);
        }
    }
}