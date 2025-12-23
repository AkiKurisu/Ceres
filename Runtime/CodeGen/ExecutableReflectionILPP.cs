using System;
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
    /// <summary>
    /// ILPP for Ceres.Flow executable functions and implementable events
    /// </summary>
    public sealed class ExecutableReflectionILPP: ILPPInterface
    {
        private readonly List<DiagnosticMessage> _diagnostics = new();
        
        private ModuleDefinition _mainModule;
        
        private ModuleDefinition _ceresModule;
        
        private PostProcessorAssemblyResolver _assemblyResolver;

        private MethodReference[] _bridgeMethodRefs;
        
        private MethodReference _bridgeMethodUberRef;

        private MethodReference _executableFunctionAttributeRef;
                
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
            
            if (_ceresModule == null)
            {
                _diagnostics.AddError($"Cannot find Ceres module: {CodeGenHelpers.CeresModuleName}");
                return null;
            }

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
                        ProcessExecutableFunction(typeDefinition);
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
            _executableFunctionAttributeRef = moduleDefinition.ImportReference(typeof(ExecutableFunctionAttribute)
                .GetConstructor(Type.EmptyTypes));
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
                
                // Check if base class method also has ImplementableEvent attribute
                // If so, and current method calls base method, skip injection to avoid double call
                if (HasBaseMethodWithImplementableEvent(typeDefinition, methodDefinition))
                {
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

        private void ProcessExecutableFunction(TypeDefinition typeDefinition)
        {
            var executableFunctions = typeDefinition.Methods.Where(definition =>
            {
                // Only need weave instance function
                if (definition.IsStatic) return false;
                return definition.CustomAttributes.Any(x =>
                    x.AttributeType.Resolve().Name == nameof(ExecutableFunctionAttribute));
            }).ToArray();
            
            if (!executableFunctions.Any())
            {
                return; 
            }

            foreach (var executableFunction in executableFunctions)
            {
                MakeExecutableFunctionInvoker(typeDefinition, executableFunction);
            }
        }
        
        private void MakeExecutableFunctionInvoker(TypeDefinition type, MethodDefinition methodToCall)
        {
            var parameters = methodToCall.Parameters.Select(definition => definition.ParameterType).ToList();
            parameters.Insert(0, type);
            int parametersCount = parameters.Count;
            var returnType = methodToCall.ReturnType ?? _mainModule.TypeSystem.Void;
            var invokerFunctionDefinition = type.AddMethod("Invoke_" + methodToCall.Name, 
                MethodAttributes.Public | MethodAttributes.Static, 
                returnType,
                parameters);
            invokerFunctionDefinition.CustomAttributes.Add(new CustomAttribute(_executableFunctionAttributeRef));

            ILProcessor processor = invokerFunctionDefinition.Body.GetILProcessor();
            OpCode callOp = methodToCall.IsVirtual ? OpCodes.Callvirt : OpCodes.Call;


            /* Include self ptr as arg */
            for (var i = 0; i < parametersCount; ++i)
            {
                if (i < _ldargs.Length)
                {
                    processor.Append(Instruction.Create(_ldargs[i]));
                }
                else
                {
                    processor.Append(processor.Create(OpCodes.Ldarg_S, (byte)i));
                }
            }
            processor.Emit(callOp, methodToCall);
            processor.Emit(OpCodes.Ret);
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
                    // Check both Call and Callvirt opcodes
                    if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt) return false;
                    if (instruction.Operand is not MethodReference methodReference) return false;
                    return methodReference.Name is nameof(FlowGraphRuntimeExtensions.ProcessEvent) or nameof(FlowGraphRuntimeExtensions.ProcessEventUber);
                });
            if (containsBridgeMethod) return true;
            // Find base method call...
            if(processor.Body.Instructions
                   .FirstOrDefault(instruction =>
                   {
                       // Check both Call and Callvirt opcodes for base method calls
                       if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt) return false;
                       if (instruction.Operand is not MethodReference methodReference) return false;
                       return methodReference.Name == methodDefinition.Name;
                   })?.Operand is not MethodReference baseMethod)
            {
                return false;
            }
            // Search in base method
            try
            {
                var baseMethodDef = baseMethod.Resolve();
                if (baseMethodDef == null) return false;
                return RecursiveSearchBridgeMethodCall(baseMethodDef);
            }
            catch
            {
                // Base method might not be resolvable (e.g., in different assembly)
                return false;
            }
        }
        
        private bool HasBaseMethodWithImplementableEvent(TypeDefinition typeDefinition, MethodDefinition methodDefinition)
        {
            if (typeDefinition.BaseType == null) return false;
            
            try
            {
                var baseType = typeDefinition.BaseType.Resolve();
                if (baseType == null) return false;
                
                // Find base method with same name and signature
                var baseMethod = baseType.Methods.FirstOrDefault(m =>
                {
                    if (m.Name != methodDefinition.Name) return false;
                    if (m.Parameters.Count != methodDefinition.Parameters.Count) return false;
                    
                    // Check if parameters match
                    for (int i = 0; i < m.Parameters.Count; i++)
                    {
                        if (m.Parameters[i].ParameterType.FullName != methodDefinition.Parameters[i].ParameterType.FullName)
                        {
                            return false;
                        }
                    }
                    
                    return true;
                });
                
                if (baseMethod == null) return false;
                
                // Check if base method has ImplementableEvent attribute
                bool hasAttribute = baseMethod.CustomAttributes.Any(x =>
                    x.AttributeType.Resolve().Name == nameof(ImplementableEventAttribute));
                
                if (!hasAttribute) return false;
                
                // Check if current method calls base method
                var processor = methodDefinition.Body.GetILProcessor();
                bool callsBaseMethod = processor.Body.Instructions
                    .Any(instruction =>
                    {
                        if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt) return false;
                        if (instruction.Operand is not MethodReference methodReference) return false;
                        return methodReference.Name == methodDefinition.Name;
                    });
                
                return callsBaseMethod;
            }
            catch
            {
                // Base type might not be resolvable
                return false;
            }
        }
    }
}