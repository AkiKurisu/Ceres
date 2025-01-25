﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace Ceres.SourceGenerator.Generators
{
    internal class ExecutableLibraryGeneratorContext
    {
        private static readonly string StartTemplate =
"""
/// <auto-generated>
/// This file is auto-generated by Ceres.SourceGenerator. 
/// All changes will be discarded.
/// </auto-generated>
{USINGNAMESPACE}
namespace {NAMESPACE}
{
    [System.Runtime.CompilerServices.CompilerGenerated]
    public partial class {CLASSNAME}
    {
        protected override unsafe void CollectExecutableFunctions()
        {
""";
        private static readonly string EndTemplate =
"""

        }
    }
}
""";


        public string Namespace;

        public string ClassName;

        public List<ExecutableFunctionInfo> FunctionInfos;

        public HashSet<string> Namespaces;

        public string GenerateCode()
        {
            var sb = new StringBuilder();
            var namedCode = StartTemplate
                .Replace("{USINGNAMESPACE}", string.Join("\n", Namespaces))
                .Replace("{NAMESPACE}", Namespace)
                .Replace("{CLASSNAME}", ClassName);
            sb.Append(namedCode);
            foreach (var function in FunctionInfos)
            {
                var list = new List<string>();
                list.AddRange(function.Parameters.Select(x => x.ParameterType));
                list.Add(function.ReturnParameter.ParameterType);
                string delegateStructure = string.Join(", ", list.ToArray());
                sb.Append(
$"""
                
            RegisterExecutableFunctions<{ClassName}>(nameof({function.MethodName}), {function.Parameters.Count}, (delegate* <{delegateStructure}>)&{function.MethodName});
""");
            }
            sb.Append(EndTemplate);
            return sb.ToString();
        }
    }
}
