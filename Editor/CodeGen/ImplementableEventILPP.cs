using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using ILPPInterface = Unity.CompilationPipeline.Common.ILPostProcessing.ILPostProcessor;
namespace Unity.Ceres.ILPP.CodeGen
{
    public sealed class ImplementableEventILPP: ILPPInterface
    {
        private readonly List<DiagnosticMessage> _diagnostics = new();
        
        private ModuleDefinition _mainModule;
        
        private ModuleDefinition _ceresModule;
        
        private PostProcessorAssemblyResolver _assemblyResolver;

        private MethodReference[] _bridgeMethodRefs;
        
        private MethodReference _bridgeMethodUberRef;
                
        private readonly OpCode[] _ldargs = { OpCodes.Ldarg_0, OpCodes.Ldarg_1, OpCodes.Ldarg_2, OpCodes.Ldarg_3 };
        
        private readonly OpCode[] _ldc4S = { OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1, OpCodes.Ldc_I4_2,
            OpCodes.Ldc_I4_3,OpCodes.Ldc_I4_4, OpCodes.Ldc_I4_5, OpCodes.Ldc_I4_6, OpCodes.Ldc_I4_7 };
        
        public override ILPostProcessor GetInstance()
        {
            return this;
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            if (compiledAssembly.Name == CodeGenHelpers.RuntimeAssemblyName)
            {
                return true;
            }
            return compiledAssembly.References
                .Any(filePath => Path.GetFileNameWithoutExtension(filePath) == CodeGenHelpers.RuntimeAssemblyName);
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly))
            {
                return null;
            }

            _diagnostics.Clear();

            // read
            var assemblyDefinition = CodeGenHelpers.AssemblyDefinitionFor(compiledAssembly, out _assemblyResolver);
            if (assemblyDefinition == null)
            {
                _diagnostics.AddError($"Cannot read Ceres assembly definition: {compiledAssembly.Name}");
                return null;
            }
            
            // modules
            (_, _ceresModule) = CodeGenHelpers.FindBaseModules(assemblyDefinition, _assemblyResolver);

            // process
            var mainModule = assemblyDefinition.MainModule;
            if (mainModule != null)
            {
                _mainModule = assemblyDefinition.MainModule;

                if (ImportReferences(mainModule, compiledAssembly.Defines))
                {
                    foreach (var typeDefinition in mainModule.Types)
                    {
                        if (!typeDefinition.IsClass)
                        {
                            continue;
                        }
                        
                        ProcessImplementableEvent(typeDefinition);
                    }
                    _mainModule.RemoveRecursiveReferences();
                }
                else
                {
                    _diagnostics.AddError($"Cannot import references into main module: {mainModule.Name}");
                }
            }
            else
            {
                _diagnostics.AddError($"Cannot get main module from Ceres assembly definition: {compiledAssembly.Name}");
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

            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), _diagnostics);
        }

        private bool ImportReferences(ModuleDefinition moduleDefinition, string[] assemblyDefines)
        {
            TypeDefinition flowGraphRuntimeExtensionsDef = null;
            foreach (var typeDefinition in _ceresModule.GetAllTypes())
            {
                if (typeDefinition.Name == nameof(FlowGraphRuntimeExtensions))
                {
                    flowGraphRuntimeExtensionsDef = typeDefinition;
                    break;
                }
            }

            if (flowGraphRuntimeExtensionsDef == null) return false;
            moduleDefinition.ImportReference(flowGraphRuntimeExtensionsDef);
            var bridgeMethods = typeof(FlowGraphRuntimeExtensions)
                .GetMethods()
                .Where(x=> x.Name == nameof(FlowGraphRuntimeExtensions.ProcessEvent))
                .OrderBy(x=> x.GetParameters().Length)
                .ToArray();
            _bridgeMethodRefs = bridgeMethods.Select(moduleDefinition.ImportReference).ToArray();
            _bridgeMethodUberRef = moduleDefinition.ImportReference(typeof(FlowGraphRuntimeExtensions)
                .GetMethod(nameof(FlowGraphRuntimeExtensions.ProcessEventUber)));
            return true;
        }
        
        private void ProcessImplementableEvent(TypeDefinition typeDefinition)
        {
            var implementableMethods = typeDefinition.Methods.Where(definition =>
            {
                return definition.CustomAttributes.Any(x =>
                    x.AttributeType.Resolve().Name == nameof(ImplementableEventAttribute));
            }).ToArray();
            
            if (!implementableMethods.Any())
            {
               return; 
            }
            if (!typeDefinition.HasInterface("Ceres.Graph.Flow.IFlowGraphRuntime"))
            {
                _diagnostics.AddWarning($"ImplementableEvent can only be executed when {typeDefinition.Name} implement IFlowGraphRuntime, ILPP will be ignored");
                return;
            }
            
            foreach (var methodDefinition in implementableMethods)
            {
                if (RecursiveSearchBridgeMethodCall(methodDefinition))
                {
                    // _diagnostics.AddWarning($"ProcessEvent has been called in ImplementableEvent method {typeDefinition.Name}.{methodDefinition.Name}, ILPP will be ignored");
                    continue;
                }
                // Inject bridge method call
                var parametersCount = methodDefinition.Parameters.Count;
                var processor = methodDefinition.Body.GetILProcessor();
                var instructions = new List<Instruction>();
                if (parametersCount < _bridgeMethodRefs.Length)
                {
                    var bridgeMethodRef = _bridgeMethodRefs[parametersCount];
                    if (parametersCount > 0)
                    {
                        bridgeMethodRef = MakeGenericInstance(bridgeMethodRef,
                            methodDefinition.Parameters.Select(x => x.ParameterType).ToArray());
                    }

                    /* Include self ptr as arg */
                    for (var i = 0; i < parametersCount + 1; ++i)
                    {
                        if (i < _ldargs.Length)
                        {
                            instructions.Add(Instruction.Create(_ldargs[i]));
                        }
                        else
                        {
                            instructions.Add(processor.Create(OpCodes.Ldarg_S, (byte)i));
                        }
                    }

                    instructions.Add(processor.Create(OpCodes.Ldstr, methodDefinition.Name));
                    instructions.Add(processor.Create(OpCodes.Call, bridgeMethodRef));
                }
                else
                {
                    /* Inject uber bridge method call */
                    /* Push self ptr */
                    instructions.Add(Instruction.Create(_ldargs[0]));
                    /* Push array length */
                    if (parametersCount < _ldc4S.Length)
                    {
                        instructions.Add(Instruction.Create(_ldc4S[parametersCount]));
                    }
                    else
                    {
                        instructions.Add(Instruction.Create(OpCodes.Ldc_I4, parametersCount));
                    }
                    /* Allocate object[] */
                    instructions.Add(Instruction.Create(OpCodes.Newarr,_mainModule.ImportReference(typeof(object))));
                    /* Fill array */
                    for (var i = 1; i < parametersCount + 1; ++i)
                    {
                        int parameterIndex = i - 1;
                        instructions.Add(Instruction.Create(OpCodes.Dup));
                        if (parameterIndex < _ldc4S.Length)
                        {
                            instructions.Add(Instruction.Create(_ldc4S[parameterIndex]));
                        }
                        else
                        {
                            instructions.Add(Instruction.Create(OpCodes.Ldc_I4, parameterIndex));
                        }
                        if (i < _ldargs.Length)
                        {
                            instructions.Add(Instruction.Create(_ldargs[i]));
                        }
                        else
                        {
                            instructions.Add(processor.Create(OpCodes.Ldarg_S, (byte)i));
                        }
                        var typeRef = methodDefinition.Parameters[parameterIndex].ParameterType;
                        if (typeRef.IsValueType)
                        {
                            instructions.Add(Instruction.Create(OpCodes.Box, typeRef));
                        }
                        instructions.Add(processor.Create(OpCodes.Stelem_Ref));
                    }
                    instructions.Add(processor.Create(OpCodes.Ldstr, methodDefinition.Name));
                    instructions.Add(processor.Create(OpCodes.Call, _bridgeMethodUberRef));
                }

                instructions.AddRange(methodDefinition.Body.Instructions);
                methodDefinition.Body.Instructions.Clear();
                foreach (var instruction in instructions)
                {
                    methodDefinition.Body.Instructions.Add(instruction);
                }
            }
        }
        
        
        private static MethodReference MakeGenericInstance(MethodReference self, TypeReference[] arguments)
        {
            var reference = new GenericInstanceMethod(self);

            foreach (var argument in arguments)
            {
                reference.GenericArguments.Add(argument);
            }
            return reference;
        }

        private bool RecursiveSearchBridgeMethodCall(MethodDefinition methodDefinition)
        {
            var processor = methodDefinition.Body.GetILProcessor();
            bool containsBridgeMethod = processor.Body.Instructions
                .Any(instruction =>
                {
                    if (instruction.OpCode != OpCodes.Call) return false;
                    if (instruction.Operand is not MethodReference methodReference) return false;
                    return methodReference.Name is nameof(FlowGraphRuntimeExtensions.ProcessEvent) or nameof(FlowGraphRuntimeExtensions.ProcessEventUber);
                });
            if (containsBridgeMethod) return true;
            // Find base method call...
            if(processor.Body.Instructions
                   .FirstOrDefault(instruction =>
                   {
                       if (instruction.OpCode != OpCodes.Call) return false;
                       if (instruction.Operand is not MethodReference methodReference) return false;
                       return methodReference.Name == methodDefinition.Name;
                   })?.Operand is not MethodReference baseMethod)
            {
                return false;
            }
            // Search in base method
            return RecursiveSearchBridgeMethodCall(baseMethod.Resolve());
        }
    }
}