﻿using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace Ceres.SourceGenerator;

internal class ExecutableLibraryGeneratorTemplate
{
    private const string StartTemplate =
        """
        /// <auto-generated>
        /// This file is auto-generated by Ceres.SourceGenerator. 
        /// All changes will be discarded.
        /// </auto-generated>
        {USING_NAMESPACES}
        namespace {NAMESPACE}
        {
            [UnityEngine.Scripting.Preserve]
            [System.Runtime.CompilerServices.CompilerGenerated]
            public partial class {CLASSNAME}
            {
                protected override unsafe void CollectExecutableFunctions()
                {
        """;
    private const string EndTemplate =
        """
        
                }
            }
        }
        """;


    public string Namespace;

    public string ClassName;

    public List<GeneratorFunctionInfo> FunctionInfos;

    public HashSet<string> Namespaces;

    public string GenerateCode()
    {
        var sb = new StringBuilder();
        var namedCode = StartTemplate
            .Replace("{USING_NAMESPACES}", string.Join("\n", Namespaces))
            .Replace("{NAMESPACE}", Namespace)
            .Replace("{CLASSNAME}", ClassName);
        sb.Append(namedCode);
        foreach (var function in FunctionInfos)
        {
            var list = new List<string>();
            list.AddRange(function.Parameters.Select(x => x.ParameterType));
            list.Add(function.ReturnParameter.ParameterType);
            string delegateStructure = string.Join(", ", list.ToArray());
            var filePath = function.Syntax.SyntaxTree.FilePath.Replace("\\", "\\\\");
            var lineSpan = function.Syntax.SyntaxTree.GetLineSpan(function.Syntax.Span);
            sb.Append(
                $"""
                                 
                             RegisterExecutableFunctionPtr<{ClassName}>(nameof({function.MethodName}), {function.Parameters.Count}, (delegate* <{delegateStructure}>)&{function.MethodName});
                             RegisterExecutableFunctionFileInfo<{ClassName}>(nameof({function.MethodName}), {function.Parameters.Count}, "{filePath}", {lineSpan.StartLinePosition.Line});
                 """);
        }
        sb.Append(EndTemplate);
        return sb.ToString();
    }
}