using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
namespace Ceres.SourceGenerator;

[Generator]
public class CustomEventGenerator : CeresSourceGenerator, ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new ExecutableEventSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not ExecutableEventSyntaxReceiver receiver) return;

        if (!ShouldRunGenerator(context))
            return;

        Helpers.SetupContext(context);
        Debug.LogInfo($"[CustomEventGenerator] Execute assembly {context.Compilation.Assembly.Name}");

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
            Debug.LogInfo($"[CustomEventGenerator] Analyze {className}");
                
            var semanticModel = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);
            var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
            if (classSymbol == null)
            {
                Debug.LogInfo($"[CustomEventGenerator] Can not get declared class from {className}");
                continue;
            }
            var generateAttribute = classSymbol
                .GetAttributes()
                .FirstOrDefault(x => 
                    x.AttributeClass?.ToDisplayString() == "Ceres.Graph.Flow.Annotations.ExecutableEventAttribute");
            if (generateAttribute == null)
            {
                Debug.LogInfo($"[CustomEventGenerator] Can not find ExecutableEventAttribute from {className}");
                continue;
            }

            if (classDeclaration.Parent is not NamespaceDeclarationSyntax namespaceNode)
            {
                Debug.LogInfo($"[CustomEventGenerator] Namespace was not declared in {className}");
                continue;
            }
            var namespaceName = namespaceNode.Name.ToString();

            /* Generate utility node for creating pooled custom event */
            if (receiver.Method.TryGetValue(classDeclaration, out var createFunctionInfo))
            {
                CreateEventGeneratorTemplate generatorTemplate = new()
                {
                    Namespace = namespaceName,
                    ClassName = className,
                    FunctionInfo = createFunctionInfo,
                    Namespaces = receiver.Namespaces[classDeclaration]
                };
                var generatedCode = generatorTemplate.GenerateCode();
                generatedFiles.Add(new GeneratedFile
                {
                    ClassName = className,
                    Namespace = namespaceName,
                    Code = generatedCode,
                    GeneratedFileName = $"{className}.gen1.cs"
                });
            }

            /* Generate executable event for custom event */
            if (receiver.PublicGetters.TryGetValue(classDeclaration, out var publicGetters))
            {
                CustomEventGeneratorTemplate generatorTemplate = new()
                {
                    Namespace = namespaceName,
                    ClassName = className,
                    PublicGetters = publicGetters,
                    Namespaces = receiver.Namespaces[classDeclaration]
                };
                var generatedCode = generatorTemplate.GenerateCode();
                generatedFiles.Add(new GeneratedFile
                {
                    ClassName = className,
                    Namespace = namespaceName,
                    Code = generatedCode,
                    GeneratedFileName = $"{className}.gen2.cs"
                });
            }
        }

        GenerateFiles(context, generatedFiles);
    }
}

public class ExecutableEventSyntaxReceiver : ISyntaxReceiver
{
    public readonly List<ClassDeclarationSyntax> Candidates = [];

    public readonly Dictionary<ClassDeclarationSyntax, GeneratorFunctionInfo> Method = new();
    
    public readonly Dictionary<ClassDeclarationSyntax, List<GeneratorParameterInfo>> PublicGetters = new();

    public readonly Dictionary<ClassDeclarationSyntax, HashSet<string>> Namespaces = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is not ClassDeclarationSyntax classNode)
            return;

        if (classNode.BaseList == null || classNode.BaseList.Types.Count == 0)
            return;

        var hasEventAttribute = classNode
            .AttributeLists
            .SelectMany(a => a.Attributes)
            .Any(attr => attr.Name.ToString().Contains("ExecutableEvent"));
        
        if (!hasEventAttribute)
        {
            return;
        }

        /* Skip generic types not specify argument */
        if (classNode.TypeParameterList?.Parameters.Count > 0)
        {
            return;
        }

        GeneratorFunctionInfo methodInfo = null;
        foreach (var member in classNode.Members)
        {
            if (member is MethodDeclarationSyntax methodNode)
            {
                if (!methodNode.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
                {
                    continue;
                }
                    
                hasEventAttribute = methodNode
                    .AttributeLists
                    .SelectMany(a => a.Attributes)
                    .Any(attr => attr.Name.ToString().Contains("ExecutableEvent"));

                if (!hasEventAttribute)
                {
                    continue;
                }

                methodInfo = new GeneratorFunctionInfo
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
                break;
            }
        }
            
        Candidates.Add(classNode);
        if (methodInfo != null)
        {
            Method.Add(classNode, methodInfo);
        }

        var namespaces = new HashSet<string>();
        var root = classNode.SyntaxTree.GetRoot();
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
        foreach (var usingDirective in usings)
        {
            var namespaceName = usingDirective.ToString();
            namespaces.Add(namespaceName);
        }
        Namespaces[classNode] = namespaces;

        var properties = new List<GeneratorParameterInfo>();
        foreach (var member in classNode.Members)
        {
            if (member is PropertyDeclarationSyntax propertyDeclaration)
            {
                if (!HasPublicGetter(propertyDeclaration)) continue;

                var parameterInfo = new GeneratorParameterInfo
                {
                    ParameterType = propertyDeclaration.Type!.ToString(),
                    ParameterName = propertyDeclaration.Identifier.Text
                };
                properties.Add(parameterInfo);
            }
        }
        PublicGetters[classNode] = properties;
    }

    private static bool HasPublicGetter(PropertyDeclarationSyntax propertyDeclaration)
    {
        if (!propertyDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
        {
            return false;
        }

        if (propertyDeclaration.AccessorList != null &&
            propertyDeclaration.AccessorList.Accessors.Any(x => x.IsKind(SyntaxKind.GetAccessorDeclaration)
             && !x.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword) || m.IsKind(SyntaxKind.ProtectedKeyword))))
        {
            return true;
        }

        return false;
    }
}