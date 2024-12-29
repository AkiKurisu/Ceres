using System.Collections.Generic;
using System.IO;
using Ceres.Graph;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using ILPPInterface = Unity.CompilationPipeline.Common.ILPostProcessing.ILPostProcessor;
namespace Unity.Ceres.Editor.CodeGen
{
    internal sealed class RuntimeAccessModifiersILPP : ILPPInterface
    {
        public override ILPPInterface GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            return compiledAssembly.Name == CodeGenHelpers.RuntimeAssemblyName;
        }

        private readonly List<DiagnosticMessage> m_Diagnostics = new();

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly))
            {
                return null;
            }

            m_Diagnostics.Clear();

            // read
            var assemblyDefinition = CodeGenHelpers.AssemblyDefinitionFor(compiledAssembly, out var unused);
            if (assemblyDefinition == null)
            {
                m_Diagnostics.AddError($"Cannot read Netcode Runtime assembly definition: {compiledAssembly.Name}");
                return null;
            }

            // process
            var mainModule = assemblyDefinition.MainModule;
            if (mainModule != null)
            {
                foreach (var typeDefinition in mainModule.Types)
                {
                    if (!typeDefinition.IsClass)
                    {
                        continue;
                    }

                    switch (typeDefinition.Name)
                    {
                        case nameof(CeresNode):
                            ProcessCeresNode(typeDefinition);
                            break;
                    }
                }
            }
            else
            {
                m_Diagnostics.AddError($"Cannot get main module from Netcode Runtime assembly definition: {compiledAssembly.Name}");
            }

            // write
            var pe = new MemoryStream();
            var pdb = new MemoryStream();

            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(),
                SymbolStream = pdb,
                WriteSymbols = true
            };

            assemblyDefinition.Write(pe, writerParameters);

            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), m_Diagnostics);
        }
        

        private void ProcessCeresNode(TypeDefinition typeDefinition)
        {
            foreach (var fieldDefinition in typeDefinition.Fields)
            {
                if (fieldDefinition.Name == nameof(CeresNode.SharedVariables))
                {
                    fieldDefinition.IsFamilyOrAssembly = true;
                }
                
                if (fieldDefinition.Name == nameof(CeresNode.Ports))
                {
                    fieldDefinition.IsFamilyOrAssembly = true;
                }
                
                if (fieldDefinition.Name == nameof(CeresNode.PortLists))
                {
                    fieldDefinition.IsFamilyOrAssembly = true;
                }
            }
            
            foreach (var methodDefinition in typeDefinition.Methods)
            {
                if (methodDefinition.Name == nameof(CeresNode.__initializeVariables))
                {
                    methodDefinition.IsFamily = true;
                }
                
                if (methodDefinition.Name == nameof(CeresNode.__initializePorts))
                {
                    methodDefinition.IsFamily = true;
                }

                if (methodDefinition.Name == nameof(CeresNode.__getTypeName))
                {
                    methodDefinition.IsFamilyOrAssembly = true;
                }
            }
        }
    }
}
