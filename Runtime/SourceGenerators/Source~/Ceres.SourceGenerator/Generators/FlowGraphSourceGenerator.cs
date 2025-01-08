﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Ceres.SourceGenerator
{
    [Generator]
    public class FlowGraphSourceGenerator : ISourceGenerator
    {
        private static readonly string template =
@"
/// <auto-generated>
/// This file is auto-generated by Ceres.SourceGenerator. 
/// All changes will be discarded.
/// </auto-generated>
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Annotations;
using Chris.Serialization;
using UnityEngine;
using UnityEngine.Assertions;
using UObject = UnityEngine.Object;
namespace {NAMESPACE}
{
    public partial class {CLASSNAME}
    {
        [NonSerialized]
        private FlowGraph _graph;
        
        [SerializeField]
        private FlowGraphData graphData;
        
        public UObject Object => this;

        protected FlowGraph Graph
        {
            get
            {
                if (_graph == null)
                {
                    _graph = GetFlowGraph();
                    _graph.Compile();
                }

                return _graph;
            }
        }

        public CeresGraph GetGraph()
        {
            return GetFlowGraph();
        }

        public FlowGraph GetFlowGraph()
        {
            if (Application.isPlaying && _graph != null)
            {
                return _graph;
            }
            return new FlowGraph(graphData.CloneT<FlowGraphData>());
        }

        public void SetGraphData(CeresGraphData graph)
        {
            graphData = (FlowGraphData)graph;
        }
        
        protected void ProcessEvent([CallerMemberName] string eventName = """")
        {
            using var evt = ExecuteFlowEvent.Create(eventName, ExecuteFlowEvent.DefaultArgs);
            Graph.TryExecuteEvent(this, evt.FunctionName, evt);
        }
        
        protected void ProcessEvent([CallerMemberName] string eventName = """", params object[] parameters)
        {
            using var evt = ExecuteFlowEvent.Create(eventName, parameters);
            Graph.TryExecuteEvent(this, evt.FunctionName, evt);
        }
        
        protected void ProcessEvent<T1>(T1 arg1, [CallerMemberName] string eventName = """")
        {
            using var evt = ExecuteFlowEvent<T1>.Create(eventName, arg1);
            Graph.TryExecuteEvent(this, evt.FunctionName, evt);
        }
        
        protected void ProcessEvent<T1, T2>(T1 arg1, T2 arg2, [CallerMemberName] string eventName = """")
        {
            using var evt = ExecuteFlowEvent<T1, T2>.Create(eventName, arg1, arg2);
            Graph.TryExecuteEvent(this, evt.FunctionName, evt);
        }
        
        protected void ProcessEvent<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3, [CallerMemberName] string eventName = """")
        {
            using var evt = ExecuteFlowEvent<T1, T2, T3>.Create(eventName, arg1, arg2, arg3);
            Graph.TryExecuteEvent(this, evt.FunctionName, evt);
        }
        
        protected void ProcessEvent<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, [CallerMemberName] string eventName = """")
        {
            using var evt = ExecuteFlowEvent<T1, T2, T3, T4>.Create(eventName, arg1, arg2, arg3, arg4);
            Graph.TryExecuteEvent(this, evt.FunctionName, evt);
        }

        protected void ProcessEvent<T1, T2, T3, T4, T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, [CallerMemberName] string eventName = """")
        {
            using var evt = ExecuteFlowEvent<T1, T2, T3, T4, T5>.Create(eventName, arg1, arg2, arg3, arg4, arg5);
            Graph.TryExecuteEvent(this, evt.FunctionName, evt);
        }

        protected void ProcessEvent<T1, T2, T3, T4, T5, T6>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, [CallerMemberName] string eventName = """")
        {
            using var evt = ExecuteFlowEvent<T1, T2, T3, T4, T5, T6>.Create(eventName, arg1, arg2, arg3, arg4, arg5, arg6);
            Graph.TryExecuteEvent(this, evt.FunctionName, evt);
        }
    }
}
";

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new CeresSyntaxReceiver());
        }

        static bool ShouldRunGenerator(GeneratorExecutionContext executionContext)
        {
            // Skip running if no references to ceres are passed to the compilation
            return executionContext.Compilation.Assembly.Name.StartsWith("Ceres", StringComparison.Ordinal) ||
                   executionContext.Compilation.ReferencedAssemblyNames.Any(r => r.Name.Equals("Ceres", StringComparison.Ordinal));
        }

        public struct GeneratedFile
        {
            public string Class;

            public string Namespace;

            public string GeneratedFileName;

            public string Code;
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var receiver = context.SyntaxReceiver as CeresSyntaxReceiver;
            if (receiver == null) return;

            if (!ShouldRunGenerator(context))
                return;

            Helpers.SetupContext(context);
            Debug.LogInfo($"Execute assmebly {context.Compilation.Assembly.Name}");

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

                var semanticModel = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

                if (classSymbol == null)
                {
                    continue;
                }

                if(!classSymbol.GetAttributes().Any(x=> x.AttributeClass?.ToDisplayString() == "Ceres.Graph.Flow.Annotations.GenerateFlowAttribute"))
                {
                    continue;
                }

                var namespaceNode = classDeclaration.Parent as NamespaceDeclarationSyntax;
                var namespaceName = namespaceNode.Name.ToString();

                var generatedCode = GenerateCode(namespaceName, className);
                generatedFiles.Add(new GeneratedFile
                {
                    Class = className,
                    Namespace = namespaceName,
                    Code = generatedCode,
                    GeneratedFileName = $"{className}.gen.cs"
                });
            }

            // Always delete all the previously generated files
            if (Helpers.CanWriteFiles)
            {
                var outputFolder = Path.Combine(Helpers.GetOutputPath(), $"{context.Compilation.AssemblyName}");
                if (Directory.Exists(outputFolder))
                    Directory.Delete(outputFolder, true);
                if (generatedFiles.Count != 0)
                    Directory.CreateDirectory(outputFolder);
            }

            foreach (var nameAndSource in generatedFiles)
            {
                Debug.LogInfo($"Generate {nameAndSource.GeneratedFileName}");
                var sourceText = SourceText.From(nameAndSource.Code, System.Text.Encoding.UTF8);
                // Normalize filename for hint purpose. Special characters are not supported anymore
                // var hintName = uniqueName.Replace('/', '_').Replace('+', '-');
                // TODO: compute a normalized hash of that name using a common stable hash algorithm
                var sourcePath = Path.Combine($"{context.Compilation.AssemblyName}", nameAndSource.GeneratedFileName);
                var hintName = TypeHash.FNV1A64(sourcePath).ToString();
                context.AddSource(hintName, sourceText.WithInitialLineDirective(sourcePath));
                try
                {
                    if (Helpers.CanWriteFiles)
                        File.WriteAllText(Path.Combine(Helpers.GetOutputPath(), sourcePath), nameAndSource.Code);
                }
                catch (Exception e)
                {
                    // In the rare event/occasion when this happen, at the very least don't bother the user and move forward
                    Debug.LogWarning($"cannot write file {sourcePath}. An exception has been thrown:{e}");
                }
            }
        }

        private static string GenerateCode(string namespaceName, string className)
        {
            return template.Replace("{NAMESPACE}", namespaceName).Replace("{CLASSNAME}", className);
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

            if (!classNode.BaseList.Types.Any(x => x.ToString() == "IFlowGraphContainer"))
            {
                return;
            }

            Candidates.Add(classNode);
        }
    }
}