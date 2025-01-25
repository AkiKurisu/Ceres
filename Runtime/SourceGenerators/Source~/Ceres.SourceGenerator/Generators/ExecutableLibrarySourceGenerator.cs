using Ceres.SourceGenerator.Generators;
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
    public class ExecutableLibrarySourceGenerator : ISourceGenerator
    {
        public const string DiagnosticId = "Ceres001";

        public static readonly DiagnosticDescriptor DiagnosticDescriptor = new(
               id: DiagnosticId,
               title: "Missing partial modifier on executable function library",
               messageFormat: "The class {0} is missing 'partial' modifier, which is required for source generator.",
               category: "CodeGeneration",
               defaultSeverity: DiagnosticSeverity.Error,
               isEnabledByDefault: true,
               description: "ExecutableFunctionLibrary must add partial modifier."
            );

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new ExecutableLibrarySyntaxReceiver());
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
            var receiver = context.SyntaxReceiver as ExecutableLibrarySyntaxReceiver;
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
                if (!classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptor, classDeclaration.GetLocation(), className));
                    continue;
                }

                var semanticModel = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
                if (classSymbol == null)
                {
                    continue;
                }

                var namespaceNode = classDeclaration.Parent as NamespaceDeclarationSyntax;
                var namespaceName = namespaceNode.Name.ToString();

                ExecutableLibraryGeneratorContext generatorContext = new()
                {
                    Namespace = namespaceName,
                    ClassName = className,
                    FunctionInfos = receiver.Methods[classDeclaration],
                    Namespaces = receiver.Namespaces[classDeclaration]
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

    public class ExecutableFunctionParameterInfo
    {
        public string ParameterType;

        public string ParameterName;
    }

    public class ExecutableFunctionInfo
    {
        public string MethodName;

        public readonly List<ExecutableFunctionParameterInfo> Parameters = new();

        public ExecutableFunctionParameterInfo ReturnParameter;
    }

    public class ExecutableLibrarySyntaxReceiver : ISyntaxReceiver
    {
        public readonly List<ClassDeclarationSyntax> Candidates = [];

        public readonly Dictionary<ClassDeclarationSyntax, List<ExecutableFunctionInfo>> Methods = new();

        public readonly Dictionary<ClassDeclarationSyntax, HashSet<string>> Namespaces = new();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is not ClassDeclarationSyntax classNode)
                return;

            if (classNode.BaseList == null || classNode.BaseList.Types.Count == 0)
                return;

            // Check inherit from ExecutableFunctionLibrary
            if (!classNode.BaseList.Types.Any(baseType =>
                baseType.Type is IdentifierNameSyntax identifierName &&
                identifierName.Identifier.Text == "ExecutableFunctionLibrary"))
            {
                return;
            }

            Candidates.Add(classNode);

            var namespaces = new HashSet<string>();
            var root = classNode.SyntaxTree.GetRoot();
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
            foreach (var usingDirective in usings)
            {
                var namespaceName = usingDirective.ToString();
                namespaces.Add(namespaceName);
            }
            Namespaces[classNode] = namespaces;

            var methodInfos = new List<ExecutableFunctionInfo>();
            foreach (var member in classNode.Members)
            {
                if (member is MethodDeclarationSyntax methodNode)
                {
                    var methodInfo = new ExecutableFunctionInfo
                    {
                        MethodName = methodNode.Identifier.Text
                    };

                    foreach (var parameter in methodNode.ParameterList.Parameters)
                    {
                        var parameterInfo = new ExecutableFunctionParameterInfo
                        {
                            ParameterType = parameter.Type.ToString(),
                            ParameterName = parameter.Identifier.Text
                        };
                        methodInfo.Parameters.Add(parameterInfo);
                    }

                    var returnParameterInfo = new ExecutableFunctionParameterInfo
                    {
                        ParameterType = methodNode.ReturnType.ToString()
                    };

                    methodInfo.ReturnParameter = returnParameterInfo;
                    methodInfos.Add(methodInfo);
                }
            }

            Methods[classNode] = methodInfos;
        }
    }
}