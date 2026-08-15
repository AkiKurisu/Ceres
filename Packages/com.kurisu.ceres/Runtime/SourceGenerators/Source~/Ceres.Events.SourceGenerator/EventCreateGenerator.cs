using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ceres.Events.SourceGenerator;

[Generator]
public class EventCreateGenerator : ISourceGenerator
{
    private const string EventBaseMetadataName = "Ceres.Events.EventBase`1";

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new EventCreateSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not EventCreateSyntaxReceiver receiver) return;

        var eventBaseSymbol = context.Compilation.GetTypeByMetadataName(EventBaseMetadataName);
        if (eventBaseSymbol == null) return;

        foreach (var classDeclaration in receiver.Candidates)
        {
            var semanticModel = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
                continue;

            // Confirm it inherits EventBase<T>
            if (!InheritsEventBase(classSymbol, eventBaseSymbol))
                continue;

            // Skip if a static Create method already exists on this type
            if (HasStaticCreateMethod(classSymbol))
                continue;

            // Collect eligible properties, ordered from base to derived
            var properties = CollectEligibleProperties(classSymbol, eventBaseSymbol);
            if (properties.Count == 0)
                continue;

            var code = GenerateCode(classDeclaration, classSymbol, properties);
            // Use fully qualified name to avoid hint collisions for nested types
            var fullName = classSymbol.ToDisplayString()
                .Replace('<', '[').Replace('>', ']')
                .Replace('.', '_').Replace('+', '_');
            var hintName = $"{fullName}.EventCreate.g.cs";
            context.AddSource(hintName, SourceText.From(code, Encoding.UTF8));
        }
    }

    private static bool InheritsEventBase(INamedTypeSymbol classSymbol, INamedTypeSymbol eventBaseSymbol)
    {
        var current = classSymbol.BaseType;
        while (current != null)
        {
            if (current.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, eventBaseSymbol))
            {
                return true;
            }
            current = current.BaseType;
        }
        return false;
    }

    private static bool HasStaticCreateMethod(INamedTypeSymbol classSymbol)
    {
        return classSymbol.GetMembers("Create")
            .OfType<IMethodSymbol>()
            .Any(m => m.IsStatic);
    }

    private static List<PropertyInfo> CollectEligibleProperties(
        INamedTypeSymbol classSymbol, INamedTypeSymbol eventBaseSymbol)
    {
        // Gather the hierarchy from the concrete type up to (but excluding) EventBase<T>
        var hierarchy = new List<INamedTypeSymbol>();
        var current = classSymbol;
        while (current != null)
        {
            if (current.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, eventBaseSymbol))
                break;
            hierarchy.Add(current);
            current = current.BaseType;
        }

        // Reverse so base class properties come first
        hierarchy.Reverse();

        var result = new List<PropertyInfo>();
        foreach (var type in hierarchy)
        {
            foreach (var member in type.GetMembers().OfType<IPropertySymbol>())
            {
                // Only properties declared directly on this type (no inherited duplicates)
                if (!SymbolEqualityComparer.Default.Equals(member.ContainingType, type))
                    continue;

                if (!IsEligibleProperty(member))
                    continue;

                result.Add(new PropertyInfo(member.Name, member.Type.ToDisplayString()));
            }
        }
        return result;
    }

    private static bool IsEligibleProperty(IPropertySymbol prop)
    {
        if (prop.IsStatic || prop.IsIndexer) return false;
        if (prop.GetMethod == null ||
            prop.GetMethod.DeclaredAccessibility != Accessibility.Public) return false;
        if (prop.SetMethod == null) return false;

        var setAccess = prop.SetMethod.DeclaredAccessibility;
        return setAccess is Accessibility.Private
            or Accessibility.Internal
            or Accessibility.Public
            or Accessibility.ProtectedAndInternal
            or Accessibility.ProtectedOrInternal;
    }

    private static string GenerateCode(
        ClassDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        List<PropertyInfo> properties)
    {
        var namespaceName = classSymbol.ContainingNamespace?.IsGlobalNamespace == false
            ? classSymbol.ContainingNamespace.ToDisplayString()
            : null;

        var className = classSymbol.Name;

        // Build nesting type chain from outermost to innermost containing type
        var nestingChain = new List<INamedTypeSymbol>();
        var ct = classSymbol.ContainingType;
        while (ct != null)
        {
            nestingChain.Insert(0, ct);
            ct = ct.ContainingType;
        }

        // Collect using directives from the source file
        var root = classDeclaration.SyntaxTree.GetRoot();
        var usings = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(u => u.ToString())
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// This file is auto-generated by Ceres.Events.SourceGenerator.");
        sb.AppendLine("// All changes will be discarded.");
        foreach (var u in usings)
            sb.AppendLine(u);
        sb.AppendLine();

        bool hasNamespace = !string.IsNullOrEmpty(namespaceName);
        if (hasNamespace)
        {
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
        }

        // Emit containing type wrappers from outermost to innermost
        int baseIndentLevel = hasNamespace ? 1 : 0;
        for (int i = 0; i < nestingChain.Count; i++)
        {
            string ind = GetIndent(baseIndentLevel + i);
            var nestingType = nestingChain[i];
            sb.AppendLine($"{ind}{GetAccessModifier(nestingType.DeclaredAccessibility)} partial {GetTypeKind(nestingType)} {nestingType.Name}");
            sb.AppendLine($"{ind}{{");
        }

        // Emit the event class itself
        int classLevel = baseIndentLevel + nestingChain.Count;
        string classIndent = GetIndent(classLevel);
        string memberIndent = GetIndent(classLevel + 1);
        string bodyIndent = GetIndent(classLevel + 2);

        sb.AppendLine($"{classIndent}{GetAccessModifier(classSymbol.DeclaredAccessibility)} partial class {className}");
        sb.AppendLine($"{classIndent}{{");

        var paramList = string.Join(", ",
            properties.Select(p => $"{p.TypeName} {ToCamelCase(p.PropertyName)}"));

        sb.AppendLine($"{memberIndent}public static {className} Create({paramList})");
        sb.AppendLine($"{memberIndent}{{");
        sb.AppendLine($"{bodyIndent}var evt = GetPooled();");
        foreach (var prop in properties)
            sb.AppendLine($"{bodyIndent}evt.{prop.PropertyName} = {ToCamelCase(prop.PropertyName)};");
        sb.AppendLine($"{bodyIndent}return evt;");
        sb.AppendLine($"{memberIndent}}}");
        sb.AppendLine($"{classIndent}}}");

        // Close containing type wrappers from innermost to outermost
        for (int i = nestingChain.Count - 1; i >= 0; i--)
        {
            string ind = GetIndent(baseIndentLevel + i);
            sb.AppendLine($"{ind}}}");
        }

        if (hasNamespace)
            sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GetIndent(int level) => new string(' ', level * 4);

    private static string GetTypeKind(INamedTypeSymbol type)
    {
        return type.TypeKind switch
        {
            Microsoft.CodeAnalysis.TypeKind.Class when type.IsRecord => "record",
            Microsoft.CodeAnalysis.TypeKind.Class => "class",
            Microsoft.CodeAnalysis.TypeKind.Struct when type.IsRecord => "record struct",
            Microsoft.CodeAnalysis.TypeKind.Struct => "struct",
            _ => "class"
        };
    }

    private static string GetAccessModifier(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "public"
        };
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private readonly struct PropertyInfo
    {
        public readonly string PropertyName;
        public readonly string TypeName;

        public PropertyInfo(string propertyName, string typeName)
        {
            PropertyName = propertyName;
            TypeName = typeName;
        }
    }
}

public class EventCreateSyntaxReceiver : ISyntaxReceiver
{
    public readonly List<ClassDeclarationSyntax> Candidates = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is not ClassDeclarationSyntax classNode)
            return;

        // Must be partial
        if (!classNode.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            return;

        // Must have a base list
        if (classNode.BaseList == null || classNode.BaseList.Types.Count == 0)
            return;

        // Skip open generic types
        if (classNode.TypeParameterList?.Parameters.Count > 0)
            return;

        Candidates.Add(classNode);
    }
}
