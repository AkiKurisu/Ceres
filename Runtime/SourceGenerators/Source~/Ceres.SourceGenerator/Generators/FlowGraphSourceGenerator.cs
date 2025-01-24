using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Ceres.SourceGenerator
{
    [Generator]
    public class FlowGraphSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new CeresSyntaxReceiver());
        }

        private static bool ShouldRunGenerator(GeneratorExecutionContext executionContext)
        {
            // Skip running if no references to ceres are passed to the compilation
            return executionContext.Compilation.Assembly.Name.StartsWith("Ceres", StringComparison.Ordinal) ||
                   executionContext.Compilation.ReferencedAssemblyNames.Any(r => r.Name.Equals("Ceres", StringComparison.Ordinal));
        }

        public struct GeneratedFile
        {
            public string ClassName;

            public string Namespace;

            public string GeneratedFileName;

            public string Code;
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var receiver = context.SyntaxReceiver as CeresSyntaxReceiver;
            if (receiver == null) return;

            if (!ShouldRunGenerator(context))
                return;

            Helpers.SetupContext(context);
            Debug.LogInfo($"Execute assmebly {context.Compilation.Assembly.Name}");

            //If the attach_debugger key is present (but without value) the returned string is the empty string (not null)
            var debugAssembly = context.GetOptionsString(GlobalOptions.AttachDebugger);
            if (debugAssembly != null)
            {
                Debug.LaunchDebugger(context, debugAssembly);
            }

            List<GeneratedFile> generatedFiles = [];

            foreach (var classDeclaration in receiver.Candidates)
            {
                var className = classDeclaration.Identifier.Text;
                Debug.LogInfo($"Analyze {className}");

                var semanticModel = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

                if (classSymbol == null)
                {
                    continue;
                }

                var generateAttribute = classSymbol.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == "Ceres.Graph.Flow.Annotations.GenerateFlowAttribute");
                if (generateAttribute == null)
                {
                    continue;
                }
                bool generateImplementation = true;
                bool generateRuntime = true;
                var namedArgs = generateAttribute.NamedArguments;
                foreach (var arg in namedArgs)
                {
                    if (arg.Key == nameof(FlowGeneratorContext.GenerateRuntime))
                    {
                        generateRuntime = (bool)arg.Value.Value;
                    }
                    else if (arg.Key == nameof(FlowGeneratorContext.GenerateImplementation))
                    {
                        generateImplementation = (bool)arg.Value.Value;
                    }
                }

                var namespaceNode = classDeclaration.Parent as NamespaceDeclarationSyntax;
                var namespaceName = namespaceNode.Name.ToString();

                FlowGeneratorContext generatorContext = new()
                {
                    Namespace = namespaceName,
                    ClassName = className,
                    GenerateImplementation = generateImplementation,
                    GenerateRuntime = generateRuntime
                };
                var generatedCode = generatorContext.GenerateCode();
                generatedFiles.Add(new GeneratedFile
                {
                    ClassName = className,
                    Namespace = namespaceName,
                    Code = generatedCode,
                    GeneratedFileName = $"{className}.gen.cs"
                });
            }

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
                    // In the rare event/occasion when this happen, at the very least don't bother the user and move forward
                    Debug.LogWarning($"cannot write file {sourcePath}. An exception has been thrown:{e}");
                }
            }
        }
    }

    public class CeresSyntaxReceiver : ISyntaxReceiver
    {
        public readonly List<ClassDeclarationSyntax> Candidates = [];

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is not ClassDeclarationSyntax classNode)
                return;

            if (classNode.BaseList == null || classNode.BaseList.Types.Count == 0)
                return;

            if (!classNode.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                return;
            }

            Candidates.Add(classNode);
        }
    }
}