using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ceres.UIElements.SourceGenerator;

[Generator]
public sealed class UIToolkitViewSourceGenerator : ISourceGenerator
{
    private const string ViewAttribute = "Ceres.UIElements.UIToolkitViewAttribute";
    private const string QueryAttribute = "Ceres.UIElements.UIToolkitQueryAttribute";

    private static readonly DiagnosticDescriptor PartialRequired = new(
        "CERESUI001", "UI Toolkit view must be partial", "Type '{0}' must be partial to generate UI Toolkit bindings",
        "Ceres.UIElements", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidField = new(
        "CERESUI002", "Invalid UI Toolkit query field", "Field '{0}' must be a mutable instance field derived from VisualElement",
        "Ceres.UIElements", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor DuplicateName = new(
        "CERESUI003", "Duplicate UI Toolkit element name", "Element name '{0}' is used by more than one query field in '{1}'",
        "Ceres.UIElements", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidName = new(
        "CERESUI004", "Invalid UI Toolkit element name", "Field '{0}' resolves to an empty or invalid UI Toolkit element name",
        "Ceres.UIElements", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor UnsupportedDeclaration = new(
        "CERESUI005", "Unsupported UI Toolkit view declaration", "Type '{0}' must be a non-generic namespace-level class",
        "Ceres.UIElements", DiagnosticSeverity.Error, true);

    public void Initialize(GeneratorInitializationContext context) => context.RegisterForSyntaxNotifications(() => new Receiver());

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not Receiver receiver) return;
        INamedTypeSymbol viewAttribute = context.Compilation.GetTypeByMetadataName(ViewAttribute);
        INamedTypeSymbol queryAttribute = context.Compilation.GetTypeByMetadataName(QueryAttribute);
        INamedTypeSymbol visualElement = context.Compilation.GetTypeByMetadataName("UnityEngine.UIElements.VisualElement");
        if (viewAttribute == null || queryAttribute == null || visualElement == null) return;

        var processedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (ClassDeclarationSyntax declaration in receiver.Candidates)
        {
            SemanticModel model = context.Compilation.GetSemanticModel(declaration.SyntaxTree);
            if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol type || !processedTypes.Add(type)) continue;
            AttributeData marker = type.GetAttributes().FirstOrDefault(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, viewAttribute));
            if (marker == null) continue;
            if (type.Arity != 0 || type.ContainingType != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(UnsupportedDeclaration, declaration.Identifier.GetLocation(), type.ToDisplayString()));
                continue;
            }
            if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                context.ReportDiagnostic(Diagnostic.Create(PartialRequired, declaration.Identifier.GetLocation(), type.ToDisplayString()));
                continue;
            }

            string prefix = marker.ConstructorArguments.Length == 0 ? string.Empty : marker.ConstructorArguments[0].Value as string ?? string.Empty;
            var fields = new List<FieldInfo>();
            foreach (IFieldSymbol field in type.GetMembers().OfType<IFieldSymbol>())
            {
                AttributeData query = field.GetAttributes().FirstOrDefault(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, queryAttribute));
                if (query == null) continue;
                Location location = field.Locations.FirstOrDefault();
                if (field.IsStatic || field.IsReadOnly || !Inherits(field.Type, visualElement))
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidField, location, field.Name));
                    continue;
                }
                string explicitName = query.ConstructorArguments.Length == 0 ? null : query.ConstructorArguments[0].Value as string;
                string name = prefix + (string.IsNullOrEmpty(explicitName) ? ToKebabCase(field.Name.TrimStart('_')) : explicitName);
                if (!ValidName(name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidName, location, field.Name));
                    continue;
                }
                fields.Add(new FieldInfo(field, name));
            }

            bool duplicate = false;
            foreach (IGrouping<string, FieldInfo> group in fields.GroupBy(field => field.ElementName, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                duplicate = true;
                foreach (FieldInfo field in group)
                    context.ReportDiagnostic(Diagnostic.Create(DuplicateName, field.Symbol.Locations.FirstOrDefault(), group.Key, type.ToDisplayString()));
            }
            if (!duplicate) Emit(context, type, fields);
        }
    }

    private static void Emit(GeneratorExecutionContext context, INamedTypeSymbol type, IReadOnlyList<FieldInfo> fields)
    {
        string ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var source = new StringBuilder("// <auto-generated />\n");
        if (ns != null) source.Append("namespace ").Append(ns).Append("\n{\n");
        source.Append("    ").Append(Accessibility(type.DeclaredAccessibility)).Append(" partial class ").Append(type.Name).Append("\n    {\n")
            .Append("        private static readonly global::Ceres.UIElements.UIToolkitElementDescriptor[] __generatedElementDescriptors = new global::Ceres.UIElements.UIToolkitElementDescriptor[]\n        {\n");
        foreach (FieldInfo field in fields)
            source.Append("            new global::Ceres.UIElements.UIToolkitElementDescriptor(\"").Append(field.Symbol.Name).Append("\", \"")
                .Append(field.ElementName).Append("\", typeof(").Append(field.Symbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(")),\n");
        source.Append("        };\n\n")
            .Append("        public static global::System.Collections.Generic.IReadOnlyList<global::Ceres.UIElements.UIToolkitElementDescriptor> GeneratedElementDescriptors => __generatedElementDescriptors;\n\n")
            .Append("        private void BindGeneratedElements(global::UnityEngine.UIElements.VisualElement root)\n        {\n");
        foreach (FieldInfo field in fields)
            source.Append("            this.").Append(field.Symbol.Name).Append(" = global::Ceres.UIElements.UIToolkitQuery.Require<")
                .Append(field.Symbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(">(root, \"")
                .Append(field.ElementName).Append("\", typeof(").Append(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append("), \"").Append(field.Symbol.Name).Append("\");\n");
        source.Append("        }\n    }\n");
        if (ns != null) source.Append("}\n");
        context.AddSource(type.ToDisplayString().Replace('.', '_') + ".UIToolkitView.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static bool Inherits(ITypeSymbol type, INamedTypeSymbol baseType)
    {
        for (ITypeSymbol current = type; current != null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
        return false;
    }

    private static string ToKebabCase(string value)
    {
        var result = new StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsUpper(c) && i > 0 && result[result.Length - 1] != '-') result.Append('-');
            result.Append(char.ToLowerInvariant(c));
        }
        return result.ToString();
    }

    private static bool ValidName(string value) => !string.IsNullOrWhiteSpace(value) && value.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');

    private static string Accessibility(Accessibility accessibility) => accessibility switch
    {
        Microsoft.CodeAnalysis.Accessibility.Public => "public",
        Microsoft.CodeAnalysis.Accessibility.Internal => "internal",
        _ => "internal"
    };

    private sealed class Receiver : ISyntaxReceiver
    {
        public readonly List<ClassDeclarationSyntax> Candidates = new();
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is ClassDeclarationSyntax declaration && declaration.AttributeLists.Count > 0) Candidates.Add(declaration);
        }
    }

    private readonly struct FieldInfo
    {
        public FieldInfo(IFieldSymbol symbol, string elementName) { Symbol = symbol; ElementName = elementName; }
        public IFieldSymbol Symbol { get; }
        public string ElementName { get; }
    }
}
