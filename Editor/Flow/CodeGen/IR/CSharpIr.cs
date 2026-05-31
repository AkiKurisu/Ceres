using System;
using System.Collections.Generic;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal enum CsExpressionPurity
    {
        Pure,
        PerEventStable,
        CachePerEvent,
        Impure
    }

    internal sealed class CsExpression
    {
        public static readonly CsExpression Empty = new(string.Empty, typeof(void));

        public string Code { get; }

        public Type Type { get; }

        public CsExpressionPurity Purity { get; }

        public bool CanInline { get; }

        public bool RequiresMaterialization { get; }

        public CsExpression(string code, Type type, CsExpressionPurity purity = CsExpressionPurity.Pure,
            bool canInline = true, bool requiresMaterialization = false)
        {
            Code = code;
            Type = type;
            Purity = purity;
            CanInline = canInline;
            RequiresMaterialization = requiresMaterialization;
        }

        public CsExpression WithCode(string code)
        {
            return new CsExpression(code, Type, Purity, CanInline, RequiresMaterialization);
        }

        public override string ToString()
        {
            return Code;
        }
    }

    internal abstract class CsStatement
    {
        public abstract void Emit(CSharpCodeWriter writer);
    }

    internal sealed class CsRawStatement : CsStatement
    {
        public string Code { get; }

        public CsRawStatement(string code)
        {
            Code = code ?? string.Empty;
        }

        public override void Emit(CSharpCodeWriter writer)
        {
            writer.WriteRawBlock(Code);
        }
    }

    internal sealed class CsExpressionStatement : CsStatement
    {
        public CsExpression Expression { get; }

        public CsExpressionStatement(CsExpression expression)
        {
            Expression = expression;
        }

        public override void Emit(CSharpCodeWriter writer)
        {
            writer.WriteLine($"{Expression.Code};");
        }
    }

    internal sealed class CsAssignmentStatement : CsStatement
    {
        public string Target { get; }

        public CsExpression Value { get; }

        public CsAssignmentStatement(string target, CsExpression value)
        {
            Target = target;
            Value = value;
        }

        public override void Emit(CSharpCodeWriter writer)
        {
            writer.WriteLine($"{Target} = {Value.Code};");
        }
    }

    internal sealed class CsDeclarationStatement : CsStatement
    {
        public Type Type { get; }

        public string Name { get; }

        public CsExpression Initializer { get; }

        public CsDeclarationStatement(Type type, string name, CsExpression initializer = null)
        {
            Type = type;
            Name = name;
            Initializer = initializer;
        }

        public override void Emit(CSharpCodeWriter writer)
        {
            var typeName = FlowCSharpRuntimeGenerator.GetFriendlyTypeName(Type);
            writer.WriteLine(Initializer == null
                ? $"{typeName} {Name};"
                : $"{typeName} {Name} = {Initializer.Code};");
        }
    }

    internal sealed class CsReturnStatement : CsStatement
    {
        public CsExpression Expression { get; }

        public CsReturnStatement(CsExpression expression = null)
        {
            Expression = expression;
        }

        public override void Emit(CSharpCodeWriter writer)
        {
            writer.WriteLine(Expression == null ? "return;" : $"return {Expression.Code};");
        }
    }

    internal sealed class CsIfStatement : CsStatement
    {
        public CsExpression Condition { get; }

        public CsBlock Then { get; } = new();

        public CsBlock Else { get; } = new();

        public CsIfStatement(CsExpression condition)
        {
            Condition = condition;
        }

        public override void Emit(CSharpCodeWriter writer)
        {
            writer.WriteLine($"if ({Condition.Code})");
            writer.WriteLine("{");
            writer.Indent();
            Then.Emit(writer);
            writer.Unindent();
            writer.WriteLine("}");
            if (Else.Count == 0)
            {
                return;
            }

            writer.WriteLine("else");
            writer.WriteLine("{");
            writer.Indent();
            Else.Emit(writer);
            writer.Unindent();
            writer.WriteLine("}");
        }
    }

    internal sealed class CsTryFinallyStatement : CsStatement
    {
        public CsBlock Try { get; } = new();

        public CsBlock Finally { get; } = new();

        public override void Emit(CSharpCodeWriter writer)
        {
            writer.WriteLine("try");
            writer.WriteLine("{");
            writer.Indent();
            Try.Emit(writer);
            writer.Unindent();
            writer.WriteLine("}");
            writer.WriteLine("finally");
            writer.WriteLine("{");
            writer.Indent();
            Finally.Emit(writer);
            writer.Unindent();
            writer.WriteLine("}");
        }
    }

    internal sealed class CsBlock
    {
        private readonly List<CsStatement> _statements = new();

        public int Count => _statements.Count;

        public IReadOnlyList<CsStatement> Statements => _statements;

        public void Add(CsStatement statement)
        {
            if (statement != null)
            {
                _statements.Add(statement);
            }
        }

        public void AddRaw(string code)
        {
            Add(new CsRawStatement(code));
        }

        public void Emit(CSharpCodeWriter writer)
        {
            foreach (var statement in _statements)
            {
                statement.Emit(writer);
            }
        }
    }

    internal readonly struct CsParameter
    {
        public readonly Type Type;

        public readonly string Name;

        public readonly bool IsRef;

        public CsParameter(Type type, string name, bool isRef = false)
        {
            Type = type;
            Name = name;
            IsRef = isRef;
        }
    }

    internal sealed class CsField
    {
        public string Modifiers { get; }

        public Type Type { get; }

        public string Name { get; }

        public string Initializer { get; }

        public CsField(string modifiers, Type type, string name, string initializer = null)
        {
            Modifiers = modifiers;
            Type = type;
            Name = name;
            Initializer = initializer;
        }
    }

    internal sealed class CsMethod
    {
        public string Modifiers { get; set; } = "private";

        public Type ReturnType { get; set; } = typeof(void);

        public string Name { get; set; }

        public bool IsAsync { get; set; }

        public List<CsParameter> Parameters { get; } = new();

        public CsBlock Body { get; } = new();
    }

    internal sealed class CsClassModel
    {
        public string Modifiers { get; set; } = "public sealed";

        public string Name { get; set; }

        public string BaseType { get; set; }

        public List<CsField> Fields { get; } = new();

        public List<CsMethod> Methods { get; } = new();

        public CsBlock ConstructorBody { get; } = new();

        public string ConstructorParameterList { get; set; } = "FlowGraphData graphData";

        public string BaseConstructorArguments { get; set; } = "graphData";

        public List<string> RawMembers { get; } = new();
    }

    internal sealed class CsCompilationUnit
    {
        public List<string> HeaderLines { get; } = new();

        public List<string> UsingLines { get; } = new();

        public string Namespace { get; set; }

        public CsClassModel Class { get; set; }
    }
}
