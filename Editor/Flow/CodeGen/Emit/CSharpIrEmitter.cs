using System.Linq;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal static class CSharpIrEmitter
    {
        public static string Emit(CsCompilationUnit unit)
        {
            var writer = new CSharpCodeWriter();
            foreach (var line in unit.HeaderLines)
            {
                writer.WriteLine(line);
            }

            if (unit.HeaderLines.Count > 0)
            {
                writer.WriteLine();
            }

            foreach (var usingLine in unit.UsingLines)
            {
                writer.WriteLine(usingLine);
            }

            if (unit.UsingLines.Count > 0)
            {
                writer.WriteLine();
            }

            writer.WriteLine($"namespace {unit.Namespace}");
            writer.WriteLine("{");
            writer.Indent();
            EmitClass(writer, unit.Class);
            writer.Unindent();
            writer.WriteLine("}");
            return writer.ToString();
        }

        private static void EmitClass(CSharpCodeWriter writer, CsClassModel model)
        {
            var baseClause = string.IsNullOrEmpty(model.BaseType) ? string.Empty : $" : {model.BaseType}";
            writer.WriteLine($"{model.Modifiers} class {model.Name}{baseClause}");
            writer.WriteLine("{");
            writer.Indent();

            foreach (var field in model.Fields)
            {
                var initializer = string.IsNullOrEmpty(field.Initializer) ? string.Empty : $" = {field.Initializer}";
                writer.WriteLine($"{field.Modifiers} {FlowCSharpRuntimeGenerator.GetFriendlyTypeName(field.Type)} {field.Name}{initializer};");
            }

            if (model.Fields.Count > 0)
            {
                writer.WriteLine();
            }

            writer.WriteLine($"public {model.Name}({model.ConstructorParameterList}) : base({model.BaseConstructorArguments})");
            writer.WriteLine("{");
            writer.Indent();
            model.ConstructorBody.Emit(writer);
            writer.Unindent();
            writer.WriteLine("}");
            writer.WriteLine();

            foreach (var method in model.Methods)
            {
                EmitMethod(writer, method);
                writer.WriteLine();
            }

            foreach (var member in model.RawMembers)
            {
                writer.WriteRawBlock(member);
                if (!member.EndsWith("\n"))
                {
                    writer.WriteLine();
                }
            }

            writer.Unindent();
            writer.WriteLine("}");
        }

        private static void EmitMethod(CSharpCodeWriter writer, CsMethod method)
        {
            var returnType = FlowCSharpRuntimeGenerator.GetFriendlyTypeName(method.ReturnType);
            var parameters = string.Join(", ", method.Parameters.Select(parameter =>
            {
                var prefix = parameter.IsRef ? "ref " : string.Empty;
                return $"{prefix}{FlowCSharpRuntimeGenerator.GetFriendlyTypeName(parameter.Type)} {parameter.Name}";
            }));
            var asyncPrefix = method.IsAsync ? "async " : string.Empty;
            writer.WriteLine($"{method.Modifiers} {asyncPrefix}{returnType} {method.Name}({parameters})");
            writer.WriteLine("{");
            writer.Indent();
            method.Body.Emit(writer);
            writer.Unindent();
            writer.WriteLine("}");
        }
    }
}
