using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace Ceres.SourceGenerator;

[Generator]
public class FlowGraphGenerator : CeresSourceGenerator, ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new CeresSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not CeresSyntaxReceiver receiver) return;

        if (!ShouldRunGenerator(context))
            return;

        Helpers.SetupContext(context);
        Debug.LogInfo($"[FlowGraphGenerator] Execute assembly {context.Compilation.Assembly.Name}");

        // If the attach_debugger key is present (but without value) the returned string is the empty string (not null)
        var debugAssembly = context.GetOptionsString(GlobalOptions.AttachDebugger);
        if (debugAssembly != null)
        {
            Debug.LaunchDebugger(context, debugAssembly);
        }

        List<GeneratedFile> generatedFiles = [];

        foreach (var classDeclaration in receiver.Candidates)
        {
            var className = classDeclaration.Identifier.Text;
            Debug.LogInfo($"[FlowGraphGenerator] Analyze {className}");

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
                if (arg.Key == nameof(FlowGraphGeneratorTemplate.GenerateRuntime))
                {
                    generateRuntime = (bool)arg.Value.Value;
                }
                else if (arg.Key == nameof(FlowGraphGeneratorTemplate.GenerateImplementation))
                {
                    generateImplementation = (bool)arg.Value.Value;
                }
            }

            var namespaceNode = classDeclaration.Parent as NamespaceDeclarationSyntax;
            var namespaceName = namespaceNode.Name.ToString();

            FlowGraphGeneratorTemplate generatorTemplate = new()
            {
                Namespace = namespaceName,
                ClassName = className,
                GenerateImplementation = generateImplementation,
                GenerateRuntime = generateRuntime
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