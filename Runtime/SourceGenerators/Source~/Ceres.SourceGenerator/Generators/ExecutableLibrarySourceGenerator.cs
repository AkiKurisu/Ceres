using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
namespace Ceres.SourceGenerator;

[Generator]
public class ExecutableLibrarySourceGenerator : CeresSourceGenerator, ISourceGenerator
{
    private const string DiagnosticId = "Ceres001";

    private static readonly DiagnosticDescriptor DiagnosticDescriptor = new(
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

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not ExecutableLibrarySyntaxReceiver receiver) return;

        if (!ShouldRunGenerator(context))
            return;

        Helpers.SetupContext(context);
        Debug.LogInfo($"Execute assembly {context.Compilation.Assembly.Name}");

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

            if (classDeclaration.Parent is not NamespaceDeclarationSyntax namespaceNode)
            {
                continue;
            }
            var namespaceName = namespaceNode.Name.ToString();

            ExecutableLibraryGeneratorTemplate generatorTemplate = new()
            {
                Namespace = namespaceName,
                ClassName = className,
                FunctionInfos = receiver.Methods[classDeclaration],
                Namespaces = receiver.Namespaces[classDeclaration]
            };
            var generatedCode = generatorTemplate.GenerateCode();
            generatedFiles.Add(new GeneratedFile
            {
                ClassName = className,
                Namespace = namespaceName,
                Code = generatedCode,
                GeneratedFileName = $"{className}.gen.cs"
            });
        }

        GenerateFiles(context, generatedFiles);
    }
}

public class ExecutableLibrarySyntaxReceiver : ISyntaxReceiver
{
    public readonly List<ClassDeclarationSyntax> Candidates = [];

    public readonly Dictionary<ClassDeclarationSyntax, List<GeneratorFunctionInfo>> Methods = new();

    public readonly Dictionary<ClassDeclarationSyntax, HashSet<string>> Namespaces = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is not ClassDeclarationSyntax classNode)
            return;

        if (classNode.BaseList == null || classNode.BaseList.Types.Count == 0)
            return;

        // Check inherit from ExecutableFunctionLibrary
        if (!classNode.BaseList.Types.Any(baseType =>
                baseType.Type is IdentifierNameSyntax { Identifier.Text: "ExecutableFunctionLibrary" }))
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

        var methodInfos = new List<GeneratorFunctionInfo>();
        foreach (var member in classNode.Members)
        {
            if (member is MethodDeclarationSyntax methodNode)
            {
                var methodInfo = new GeneratorFunctionInfo
                {
                    MethodName = methodNode.Identifier.Text
                };

                foreach (var parameter in methodNode.ParameterList.Parameters)
                {
                    var parameterInfo = new GeneratorParameterInfo
                    {
                        ParameterType = parameter.Type!.ToString(),
                        ParameterName = parameter.Identifier.Text
                    };
                    methodInfo.Parameters.Add(parameterInfo);
                }

                var returnParameterInfo = new GeneratorParameterInfo
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