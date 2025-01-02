using System.IO;
using UnityEditor;
using UnityEngine;
namespace Ceres.Graph.Flow.CodeGen
{
    internal static class SourceGeneratorSettings
    {
        /// <summary>
        /// Create the Default.globalconfig file in the Assets folder root.
        /// </summary>
        /// <returns></returns>
        [MenuItem("Assets/Create/Ceres/SourceGenerator AnalyzerConfig", priority = 1)]
        public static void CreateGlobalConfig()
        {
            var assetPath = Path.Combine(Application.dataPath, "Default.globalconfig");
            if(File.Exists(assetPath))
                return;
            using var streamWriter = File.CreateText(assetPath);
            streamWriter.WriteLine("# global config file must have the is_global=true present in the first line.");
            streamWriter.WriteLine("is_global=true");
            streamWriter.WriteLine("");
            streamWriter.WriteLine("# enabe/disable the Ceres source generator files output in the temp folder. 0 disable, empty or 1 enable.");
            streamWriter.WriteLine("ceres.sourcegenerator.write_files_to_disk=1");
            streamWriter.WriteLine("");
            streamWriter.WriteLine("# enable/disable Ceres source generator logs output to the Temp/CeresGenerated/sourcegenerato.log file. 0 disable, empty or 1 enable.");
            streamWriter.WriteLine("ceres.sourcegenerator.write_logs_to_disk=1");
            streamWriter.WriteLine("");
            streamWriter.WriteLine("# the default Ceres source generator logging level is info.");
            streamWriter.WriteLine("ceres.sourcegenerator.logging_level=info");
            streamWriter.WriteLine("");
            streamWriter.WriteLine("# Ceres source generator will emit profile timings. 0 disable, empty or 1 enable.");
            streamWriter.WriteLine("ceres.sourcegenerator.emit_timing=0");
            streamWriter.WriteLine("");
            streamWriter.WriteLine("# Ceres source generator will wait attaching the debugger before processing the specified assembly (or all if the value is empty). Keep commented to avoid the debugger to attach");
            streamWriter.WriteLine("#ceres.sourcegenerator.attach_debugger=ASSEMBLY_NAME_OR_EMPTY");
            streamWriter.Flush();
            AssetDatabase.Refresh();
        }
    }
}
