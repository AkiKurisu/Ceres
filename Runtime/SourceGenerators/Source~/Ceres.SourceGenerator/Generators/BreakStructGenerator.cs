using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace Ceres.SourceGenerator;

[Generator]
public class BreakStructGenerator : CeresSourceGenerator, ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new BreakStructSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not BreakStructSyntaxReceiver receiver) return;

        if (!ShouldRunGenerator(context))
            return;

        Helpers.SetupContext(context);
        Debug.LogInfo($"[BreakStructGenerator] Execute assembly {context.Compilation.Assembly.Name}");

        // If the attach_debugger key is present (but without value) the returned string is the empty string (not null)
        var debugAssembly = context.GetOptionsString(GlobalOptions.AttachDebugger);
        if (debugAssembly != null)
        {
            Debug.LaunchDebugger(context, debugAssembly);
        }

        List<GeneratedFile> generatedFiles = [];

        foreach (var structDeclaration in receiver.Candidates)
        {
            var structName = structDeclaration.Identifier.Text;
            Debug.LogInfo($"[BreakStructGenerator] Analyze {structName}");

            var semanticModel = context.Compilation.GetSemanticModel(structDeclaration.SyntaxTree);
            var structSymbol = semanticModel.GetDeclaredSymbol(structDeclaration);
            if (structSymbol == null)
            {
                Debug.LogInfo($"[BreakStructGenerator] Can not get declared struct from {structName}");
                continue;
            }

            var generateAttribute = structSymbol
                .GetAttributes()
                .FirstOrDefault(x =>
                    x.AttributeClass?.ToDisplayString() == "Ceres.Graph.Flow.Annotations.ExecutableEventAttribute");
            if (generateAttribute == null)
            {
                Debug.LogInfo($"[BreakStructGenerator] Can not find ExecutableEventAttribute from {structName}");
                continue;
            }

            if (structDeclaration.Parent is not NamespaceDeclarationSyntax namespaceNode)
            {
                Debug.LogInfo($"[BreakStructGenerator] Namespace was not declared in {structName}");
                continue;
            }
            var namespaceName = namespaceNode.Name.ToString();

            // Generate Break node for struct
            if (receiver.PublicFields.TryGetValue(structDeclaration, out var publicFields))
            {
                BreakStructGeneratorTemplate generatorTemplate = new()
                {
                    Namespace = namespaceName,
                    StructName = structName,
                    PublicFields = publicFields,
                    Namespaces = receiver.Namespaces[structDeclaration]
                };
                var generatedCode = generatorTemplate.GenerateCode();
                generatedFiles.Add(new GeneratedFile
                {
                    ClassName = $"FlowNode_Break_{structName}",
                    Namespace = namespaceName,
                    Code = generatedCode,
                    GeneratedFileName = $"FlowNode_Break_{structName}.gen.cs"
                });
            }
        }

        GenerateFiles(context, generatedFiles);
    }
}

public class BreakStructSyntaxReceiver : ISyntaxReceiver
{
    public readonly List<StructDeclarationSyntax> Candidates = [];

    public readonly Dictionary<StructDeclarationSyntax, List<GeneratorParameterInfo>> PublicFields = new();

    public readonly Dictionary<StructDeclarationSyntax, HashSet<string>> Namespaces = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is not StructDeclarationSyntax structNode)
            return;

        var hasEventAttribute = structNode
            .AttributeLists
            .SelectMany(a => a.Attributes)
            .Any(attr => attr.Name.ToString().Contains("ExecutableEvent"));

        if (!hasEventAttribute)
        {
            return;
        }

        /* Skip generic types not specify argument */
        if (structNode.TypeParameterList?.Parameters.Count > 0)
        {
            return;
        }

        Candidates.Add(structNode);

        var namespaces = new HashSet<string>();
        var root = structNode.SyntaxTree.GetRoot();
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
        foreach (var usingDirective in usings)
        {
            var namespaceName = usingDirective.ToString();
            namespaces.Add(namespaceName);
        }
        Namespaces[structNode] = namespaces;

        var fields = new List<GeneratorParameterInfo>();
        foreach (var member in structNode.Members)
        {
            if (member is FieldDeclarationSyntax fieldDeclaration)
            {
                if (!HasPublicField(fieldDeclaration)) continue;

                // Handle multiple variable declarations (e.g., public int x, y;)
                foreach (var variable in fieldDeclaration.Declaration.Variables)
                {
                    var fieldInfo = new GeneratorParameterInfo
                    {
                        ParameterType = fieldDeclaration.Declaration.Type!.ToString(),
                        ParameterName = variable.Identifier.Text
                    };
                    fields.Add(fieldInfo);
                }
            }
        }
        PublicFields[structNode] = fields;
    }

    private static bool HasPublicField(FieldDeclarationSyntax fieldDeclaration)
    {
        // Check if field has public modifier and is not static
        if (!fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
        {
            return false;
        }

        if (fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            return false;
        }

        return true;
    }
}

