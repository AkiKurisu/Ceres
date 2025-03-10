using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ceres.SourceGenerator;

public struct GeneratedFile
{
    public string ClassName;

    public string Namespace;

    public string GeneratedFileName;

    public string Code;
}
        
public class GeneratorParameterInfo
{
    public string ParameterType;

    public string ParameterName;
}

public class GeneratorFunctionInfo
{
    public MethodDeclarationSyntax Syntax;

    public string MethodName;

    public readonly List<GeneratorParameterInfo> Parameters = [];

    public GeneratorParameterInfo ReturnParameter;
}
    
public class CeresSourceGenerator
{
    protected static bool ShouldRunGenerator(GeneratorExecutionContext executionContext)
    {
        // Skip running if no references to ceres are passed to the compilation
        return executionContext.Compilation.Assembly.Name.StartsWith("Ceres", StringComparison.Ordinal) ||
               executionContext.Compilation.ReferencedAssemblyNames.Any(r => r.Name.Equals("Ceres", StringComparison.Ordinal));
    }

    protected static void GenerateFiles(GeneratorExecutionContext context, List<GeneratedFile> generatedFiles)
    {
        // Always delete all the previously generated files
        if (Helpers.CanWriteFiles)
        {
            var outputFolder = Path.Combine(Helpers.GetOutputPath(), $"{context.Compilation.AssemblyName}");
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
            if (generatedFiles.Count != 0)
                Directory.CreateDirectory(outputFolder);
        }

        foreach (var nameAndSource in generatedFiles)
        {
            Debug.LogInfo($"Generate {nameAndSource.GeneratedFileName}");
            var sourceText = SourceText.From(nameAndSource.Code, System.Text.Encoding.UTF8);
            // Normalize filename for hint purpose. Special characters are not supported anymore
            // var hintName = uniqueName.Replace('/', '_').Replace('+', '-');
            // TODO: compute a normalized hash of that name using a common stable hash algorithm
            var sourcePath = Path.Combine($"{context.Compilation.AssemblyName}", nameAndSource.GeneratedFileName);
            var hintName = TypeHash.FNV1A64(sourcePath).ToString();
            context.AddSource(hintName, sourceText.WithInitialLineDirective(sourcePath));
            try
            {
                if (Helpers.CanWriteFiles)
                    File.WriteAllText(Path.Combine(Helpers.GetOutputPath(), sourcePath), nameAndSource.Code);
            }
            catch (Exception e)
            {
                // In the rare event/occasion when this happens, at the very least don't bother the user and move forward
                Debug.LogWarning($"cannot write file {sourcePath}. An exception has been thrown:{e}");
            }
        }
    }
}