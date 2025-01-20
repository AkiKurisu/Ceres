using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ceres;
using Ceres.Graph;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using ILPPInterface = Unity.CompilationPipeline.Common.ILPostProcessing.ILPostProcessor;
using MethodAttributes = Mono.Cecil.MethodAttributes;
namespace Unity.Ceres.ILPP.CodeGen
{
    internal sealed class CeresNodeILPP : ILPPInterface
    {
        private readonly List<DiagnosticMessage> m_Diagnostics = new();

        private ModuleDefinition m_UnityModule;

        private ModuleDefinition m_CeresModule;

        private ModuleDefinition m_MainModule;
        
        private TypeReference m_CeresNode_TypeRef;
        
        private TypeReference m_SharedVariable_TypeRef;
        
        private TypeReference m_CeresPort_TypeRef;

        private MethodReference m_ExceptionCtorMethodReference;
        
        private MethodReference m_List_SharedVariable_Add;
        
        private MethodReference m_List_SharedVariable_AddRange;
        
        private MethodReference m_Dictionary_Port_Add;
        
        private MethodReference m_Dictionary_PortList_Add;

        private PostProcessorAssemblyResolver m_AssemblyResolver;
        
        private const string k_CeresNode___initializeVariables = nameof(CeresNode.__initializeVariables);
        
        private const string k_CeresNode___initializePorts = nameof(CeresNode.__initializePorts);
        
        private const string k_CeresNode_SharedVariables = nameof(CeresNode.SharedVariables);
        
        private const string k_CeresNode_Ports = nameof(CeresNode.Ports);
        
        private const string k_CeresNode_PortLists = nameof(CeresNode.PortLists);
        
        private FieldReference m_CeresNode_SharedVariables_FieldRef;
        
        private FieldReference m_CeresNode_Ports_FieldRef;
        
        private FieldReference m_CeresNode_PortLists_FieldRef;

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

            m_Diagnostics.Clear();

            // read
            var assemblyDefinition = CodeGenHelpers.AssemblyDefinitionFor(compiledAssembly, out m_AssemblyResolver);
            if (assemblyDefinition == null)
            {
                m_Diagnostics.AddError($"Cannot read assembly definition: {compiledAssembly.Name}");
                return null;
            }

            // modules
            (m_UnityModule, m_CeresModule) = CodeGenHelpers.FindBaseModules(assemblyDefinition, m_AssemblyResolver);

            if (m_UnityModule == null)
            {
                m_Diagnostics.AddError($"Cannot find Unity module: {CodeGenHelpers.UnityModuleName}");
                return null;
            }

            if (m_CeresModule == null)
            {
                m_Diagnostics.AddError($"Cannot find Ceres module: {CodeGenHelpers.CeresModuleName}");
                return null;
            }
            
            var mainModule = assemblyDefinition.MainModule;
            if (mainModule != null)
            {
                m_MainModule = mainModule;

                if (ImportReferences(mainModule, compiledAssembly.Defines))
                {
                    try
                    {
                        mainModule.GetTypes()
                            .Where(t => t.IsSubclassOf(CodeGenHelpers.CeresNode_FullName))
                            .ToList()
                            .ForEach(b => ProcessCeresNode(b, compiledAssembly.Defines));
                    }
                    catch (Exception e)
                    {
                        m_Diagnostics.AddError((e + e.StackTrace).Replace("\n", "|").Replace("\r", "|"));
                    }
                }
                else
                {
                    m_Diagnostics.AddError($"Cannot import references into main module: {mainModule.Name}");
                }
            }
            else
            {
                m_Diagnostics.AddError($"Cannot get main module from assembly definition: {compiledAssembly.Name}");
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


        private bool ImportReferences(ModuleDefinition moduleDefinition, string[] assemblyDefines)
        {
            TypeDefinition ceresNodeTypeDef = null;
            TypeDefinition sharedVariableTypeDef = null;
            TypeDefinition ceresPortTypeDef = null;
            
            foreach (var typeDefinition in m_CeresModule.GetAllTypes())
            {
                if (ceresNodeTypeDef == null && typeDefinition.Name == nameof(CeresNode))
                {
                    ceresNodeTypeDef = typeDefinition;
                    continue;
                }

                if (sharedVariableTypeDef == null && typeDefinition.Name == nameof(SharedVariable))
                {
                    sharedVariableTypeDef = typeDefinition;
                    continue;
                }
                
                if (ceresPortTypeDef == null && typeDefinition.Name == nameof(CeresPort))
                {
                    ceresPortTypeDef = typeDefinition;
                    continue;
                }
            }

            m_CeresNode_TypeRef = moduleDefinition.ImportReference(ceresNodeTypeDef);

            foreach (var fieldDef in ceresNodeTypeDef!.Fields)
            {
                switch (fieldDef.Name)
                {
                    case k_CeresNode_SharedVariables:
                        m_CeresNode_SharedVariables_FieldRef = moduleDefinition.ImportReference(fieldDef);
                        break;
                    case k_CeresNode_Ports:
                        m_CeresNode_Ports_FieldRef = moduleDefinition.ImportReference(fieldDef);
                        break;
                    case k_CeresNode_PortLists:
                        m_CeresNode_PortLists_FieldRef = moduleDefinition.ImportReference(fieldDef);
                        break;
                }
            }


            m_SharedVariable_TypeRef = moduleDefinition.ImportReference(sharedVariableTypeDef);
            m_CeresPort_TypeRef = moduleDefinition.ImportReference(ceresPortTypeDef);
            
            // Find all extension methods for FastBufferReader and FastBufferWriter to enable user-implemented methods to be called
            var assemblies = new List<AssemblyDefinition> { m_MainModule.Assembly };
            foreach (var reference in m_MainModule.AssemblyReferences)
            {
                var assembly = m_AssemblyResolver.Resolve(reference);
                if (assembly != null)
                {
                    assemblies.Add(assembly);
                }
            }

            
            // Standard types are really hard to reliably find using the Mono Cecil way, they resolve differently in Mono vs .NET Core
            // Importing with typeof() is less dangerous for standard framework types though, so we can just do it
            var exceptionType = typeof(Exception);
            var exceptionCtor = exceptionType.GetConstructor(new[] { typeof(string) });
            m_ExceptionCtorMethodReference = m_MainModule.ImportReference(exceptionCtor);

            var variableListType = typeof(List<SharedVariable>);
            var addMethod = variableListType.GetMethod(nameof(List<SharedVariable>.Add),
                new[] { typeof(SharedVariable) });
            var addRangeMethod = variableListType.GetMethod(nameof(List<SharedVariable>.AddRange),
                new[] { typeof(IEnumerable<SharedVariable>) });
            m_List_SharedVariable_Add = moduleDefinition.ImportReference(addMethod);
            m_List_SharedVariable_AddRange = moduleDefinition.ImportReference(addRangeMethod);
            
            var portDictionaryType = typeof(Dictionary<string, CeresPort>);
            var portAddMethod = portDictionaryType.GetMethod(nameof(Dictionary<string, CeresPort>.Add),
                new[] { typeof(string), typeof(CeresPort) });
            var portListDictionaryType = typeof(Dictionary<string, IList>);
            var portListAddMethod = portListDictionaryType.GetMethod(nameof(Dictionary<string, IList>.Add),
                new[] { typeof(string), typeof(IList) });
            m_Dictionary_Port_Add = moduleDefinition.ImportReference(portAddMethod);
            m_Dictionary_PortList_Add = moduleDefinition.ImportReference(portListAddMethod);
            
            return true;
        }
        
        private void GenerateVariableInitialization(TypeDefinition type)
        {
            foreach (var methodDefinition in type.Methods)
            {
                if (methodDefinition.Name == k_CeresNode___initializeVariables)
                {
                    // If this hits, we've already generated the method for this class because a child class got processed first.
                    return;
                }
            }

            var method = new MethodDefinition(
                k_CeresNode___initializeVariables,
                MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                m_MainModule.TypeSystem.Void);

            var processor = method.Body.GetILProcessor();

            method.Body.Variables.Add(new VariableDefinition(m_MainModule.TypeSystem.Boolean));

            processor.Emit(OpCodes.Nop);

            foreach (var fieldDefinition in type.Fields)
            {
                FieldReference field = fieldDefinition;
                if (type.HasGenericParameters)
                {
                    var genericType = new GenericInstanceType(fieldDefinition.DeclaringType);
                    foreach (var parameter in fieldDefinition.DeclaringType.GenericParameters)
                    {
                        genericType.GenericArguments.Add(parameter);
                    }
                    field = new FieldReference(fieldDefinition.Name, fieldDefinition.FieldType, genericType);
                }

                if (field.FieldType.Resolve() == null)
                {
                    continue;
                }

                if (!field.FieldType.IsArray && !field.FieldType.Resolve().IsArray 
                                             && field.FieldType.IsSubclassOf(m_SharedVariable_TypeRef))
                    
                {
                    // if({variable} == null) {
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, field);
                    processor.Emit(OpCodes.Ldnull);
                    processor.Emit(OpCodes.Ceq);
                    processor.Emit(OpCodes.Stloc_0);
                    processor.Emit(OpCodes.Ldloc_0);

                    var afterThrowInstruction = processor.Create(OpCodes.Nop);

                    processor.Emit(OpCodes.Brfalse, afterThrowInstruction);

                    // {variable} = new ();
                    processor.Emit(OpCodes.Ldarg_0);                       
                    processor.Emit(OpCodes.Newobj, field.ImportDefaultConstructor(m_MainModule));
                    processor.Emit(OpCodes.Stfld, field);
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Br, afterThrowInstruction);
                    
                    // throw new Exception("...");
                    // processor.Emit(OpCodes.Nop);
                    // processor.Emit(OpCodes.Ldstr, $"{type.Name}.{field.Name} cannot be null. All {nameof(SharedVariable)} fields must be initialized.");
                    // processor.Emit(OpCodes.Newobj, m_ExceptionCtorMethodReference);
                    // processor.Emit(OpCodes.Throw);

                    // }
                    processor.Append(afterThrowInstruction);

                    // SharedVariable.Add({variable});
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, m_CeresNode_SharedVariables_FieldRef);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, field);
                    processor.Emit(OpCodes.Callvirt, m_List_SharedVariable_Add);
                    continue;
                }

                bool isVariableArray = field.FieldType.IsArray &&
                                       field.FieldType.GetElementType().IsSubclassOf(m_SharedVariable_TypeRef);

                bool isVariableList = false;
                if (field.FieldType is GenericInstanceType gi)
                {
                    if (gi.ElementType.FullName == typeof(List<>).FullName 
                        && gi.GenericArguments[0].IsSubclassOf(m_SharedVariable_TypeRef))
                    {
                        isVariableList = true;
                    }
                }
                
                if (isVariableArray || isVariableList)
                {
                    // if {IList<SharedVariable>} == null) {
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, field);
                    processor.Emit(OpCodes.Ldnull);
                    processor.Emit(OpCodes.Ceq);
                    processor.Emit(OpCodes.Stloc_0);
                    processor.Emit(OpCodes.Ldloc_0);

                    var afterThrowInstruction = processor.Create(OpCodes.Nop);

                    processor.Emit(OpCodes.Brfalse, afterThrowInstruction);

                    // {IList<SharedVariable>} = new();
                    // processor.Emit(OpCodes.Ldarg_0);                       
                    // processor.Emit(OpCodes.Newobj, field.ImportDefaultConstructor(m_MainModule));
                    // processor.Emit(OpCodes.Stfld, field);
                    // processor.Emit(OpCodes.Nop);
                    // processor.Emit(OpCodes.Br, afterThrowInstruction);
                    
                    // throw new Exception("...");
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldstr, $"{type.Name}.{field.Name} cannot be null. All {nameof(IList<SharedVariable>)} fields must be initialized.");
                    processor.Emit(OpCodes.Newobj, m_ExceptionCtorMethodReference);
                    processor.Emit(OpCodes.Throw);

                    // }
                    processor.Append(afterThrowInstruction);

                    // SharedVariable.AddRange({IList<SharedVariable>});
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, m_CeresNode_SharedVariables_FieldRef);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, field);
                    processor.Emit(OpCodes.Callvirt, m_List_SharedVariable_AddRange);
                }
            }

            // Find the base method...
            MethodReference initializeVariablesReference = null;
            foreach (var methodDefinition in type.BaseType.Resolve().Methods)
            {
                if (methodDefinition.Name == k_CeresNode___initializeVariables)
                {
                    initializeVariablesReference = m_MainModule.ImportReference(methodDefinition);
                    break;
                }
            }

            if (initializeVariablesReference == null)
            {
                // If we couldn't find it, we have to go ahead and add it.
                // The base class could be in another assembly... that's ok, this won't
                // actually save, but it'll generate the same method the same way later,
                // so this at least allows us to reference it.
                GenerateVariableInitialization(type.BaseType.Resolve());
                foreach (var methodDefinition in type.BaseType.Resolve().Methods)
                {
                    if (methodDefinition.Name == k_CeresNode___initializeVariables)
                    {
                        initializeVariablesReference = m_MainModule.ImportReference(methodDefinition);
                        break;
                    }
                }
            }

            if (type.BaseType.Resolve().HasGenericParameters)
            {
                var baseTypeInstance = (GenericInstanceType)type.BaseType;
                initializeVariablesReference = initializeVariablesReference.MakeGeneric(baseTypeInstance.GenericArguments.ToArray());
            }

            // base.__initializeVariables();
            processor.Emit(OpCodes.Nop);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, initializeVariablesReference);
            processor.Emit(OpCodes.Nop);

            processor.Emit(OpCodes.Ret);

            type.Methods.Add(method);
        }
        
        private void GeneratePortInitialization(TypeDefinition type)
        {
            foreach (var methodDefinition in type.Methods)
            {
                if (methodDefinition.Name == k_CeresNode___initializePorts)
                {
                    // If this hits, we've already generated the method for this class because a child class got processed first.
                    return;
                }
            }

            var method = new MethodDefinition(
                k_CeresNode___initializePorts,
                MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                m_MainModule.TypeSystem.Void);

            var processor = method.Body.GetILProcessor();

            method.Body.Variables.Add(new VariableDefinition(m_MainModule.TypeSystem.Boolean));

            processor.Emit(OpCodes.Nop);

            foreach (var fieldDefinition in type.Fields)
            {
                FieldReference field = fieldDefinition;
                if (type.HasGenericParameters)
                {
                    var genericType = new GenericInstanceType(fieldDefinition.DeclaringType);
                    foreach (var parameter in fieldDefinition.DeclaringType.GenericParameters)
                    {
                        genericType.GenericArguments.Add(parameter);
                    }
                    field = new FieldReference(fieldDefinition.Name, fieldDefinition.FieldType, genericType);
                }

                if (field.FieldType.Resolve() == null)
                {
                    continue;
                }

                if (!field.FieldType.IsArray && !field.FieldType.Resolve().IsArray 
                                             && field.FieldType.IsSubclassOf(m_CeresPort_TypeRef))
                    
                {
                    // if({port} == null) {
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, field);
                    processor.Emit(OpCodes.Ldnull);
                    processor.Emit(OpCodes.Ceq);
                    processor.Emit(OpCodes.Stloc_0);
                    processor.Emit(OpCodes.Ldloc_0);

                    var afterThrowInstruction = processor.Create(OpCodes.Nop);

                    processor.Emit(OpCodes.Brfalse, afterThrowInstruction);

                    // {port} = new ();
                    processor.Emit(OpCodes.Ldarg_0);                       
                    processor.Emit(OpCodes.Newobj, field.ImportDefaultConstructor(m_MainModule));
                    processor.Emit(OpCodes.Stfld, field);
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Br, afterThrowInstruction);
                    
                    // throw new Exception("...");
                    // processor.Emit(OpCodes.Nop);
                    // processor.Emit(OpCodes.Ldstr, $"{type.Name}.{field.Name} cannot be null. All {nameof(CeresPort)} fields must be initialized.");
                    // processor.Emit(OpCodes.Newobj, m_ExceptionCtorMethodReference);
                    // processor.Emit(OpCodes.Throw);

                    // }
                    processor.Append(afterThrowInstruction);

                    // Ports.Add({port}.Name, {port});
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, m_CeresNode_Ports_FieldRef);
                    var fieldName = field.Name;
                    processor.Emit(OpCodes.Ldstr, fieldName);  
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, field);
                    processor.Emit(OpCodes.Callvirt, m_Dictionary_Port_Add);
                    continue;
                }

                bool isPortArray = field.FieldType.IsArray &&
                                       field.FieldType.GetElementType().IsSubclassOf(m_CeresPort_TypeRef);

                bool isPortList = false;
                if (field.FieldType is GenericInstanceType gi)
                {
                    if (gi.ElementType.FullName == typeof(List<>).FullName 
                        && gi.GenericArguments[0].IsSubclassOf(m_CeresPort_TypeRef))
                    {
                        isPortList = true;
                    }
                }
                
                if (isPortArray || isPortList)
                {
                    // if {IList<CeresPort>} == null) {
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, field);
                    processor.Emit(OpCodes.Ldnull);
                    processor.Emit(OpCodes.Ceq);
                    processor.Emit(OpCodes.Stloc_0);
                    processor.Emit(OpCodes.Ldloc_0);

                    var afterThrowInstruction = processor.Create(OpCodes.Nop);

                    processor.Emit(OpCodes.Brfalse, afterThrowInstruction);

                    // {IList<CeresPort>} = new ();
                    // processor.Emit(OpCodes.Ldarg_0);                       
                    // processor.Emit(OpCodes.Newobj, field.ImportDefaultConstructor(m_MainModule));
                    // processor.Emit(OpCodes.Stfld, field);
                    // processor.Emit(OpCodes.Nop);
                    // processor.Emit(OpCodes.Br, afterThrowInstruction);
                    
                    // throw new Exception("...");
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldstr, $"{type.Name}.{field.Name} cannot be null. All {nameof(IList<CeresPort>)} fields must be initialized.");
                    processor.Emit(OpCodes.Newobj, m_ExceptionCtorMethodReference);
                    processor.Emit(OpCodes.Throw);

                    // }
                    processor.Append(afterThrowInstruction);

                    // PortLists.AddRange({IList<CeresPort>}.Name, {IList<CeresPort>});
                    processor.Emit(OpCodes.Nop);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, m_CeresNode_PortLists_FieldRef);
                    var fieldName = field.Name;
                    processor.Emit(OpCodes.Ldstr, fieldName);  
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, field);
                    processor.Emit(OpCodes.Callvirt, m_Dictionary_PortList_Add);
                }
            }

            // Find the base method...
            MethodReference initializePortsReference = null;
            foreach (var methodDefinition in type.BaseType.Resolve().Methods)
            {
                if (methodDefinition.Name == k_CeresNode___initializePorts)
                {
                    initializePortsReference = m_MainModule.ImportReference(methodDefinition);
                    break;
                }
            }

            if (initializePortsReference == null)
            {
                // If we couldn't find it, we have to go ahead and add it.
                // The base class could be in another assembly... that's ok, this won't
                // actually save, but it'll generate the same method the same way later,
                // so this at least allows us to reference it.
                GeneratePortInitialization(type.BaseType.Resolve());
                foreach (var methodDefinition in type.BaseType.Resolve().Methods)
                {
                    if (methodDefinition.Name == k_CeresNode___initializePorts)
                    {
                        initializePortsReference = m_MainModule.ImportReference(methodDefinition);
                        break;
                    }
                }
            }

            if (type.BaseType.Resolve().HasGenericParameters)
            {
                var baseTypeInstance = (GenericInstanceType)type.BaseType;
                initializePortsReference = initializePortsReference.MakeGeneric(baseTypeInstance.GenericArguments.ToArray());
            }

            // base.__initializePorts();
            processor.Emit(OpCodes.Nop);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, initializePortsReference);
            processor.Emit(OpCodes.Nop);

            processor.Emit(OpCodes.Ret);

            type.Methods.Add(method);
        }
    
        private void ProcessCeresNode(TypeDefinition typeDefinition, string[] assemblyDefines)
        {
            GenerateVariableInitialization(typeDefinition);
            GeneratePortInitialization(typeDefinition);
            
            // override CeresNode.__getTypeName() method to return concrete type
            {
                var ceresNode_TypeDef = m_CeresNode_TypeRef.Resolve();
                var baseGetTypeNameMethod = ceresNode_TypeDef.Methods.First(p => p.Name.Equals(nameof(CeresNode.__getTypeName)));

                var newGetTypeNameMethod = new MethodDefinition(
                    nameof(CeresNode.__getTypeName),
                    (baseGetTypeNameMethod.Attributes & ~MethodAttributes.NewSlot) | MethodAttributes.ReuseSlot,
                    baseGetTypeNameMethod.ReturnType)
                {
                    ImplAttributes = baseGetTypeNameMethod.ImplAttributes,
                    SemanticsAttributes = baseGetTypeNameMethod.SemanticsAttributes,
                    IsFamilyOrAssembly = true
                };

                var processor = newGetTypeNameMethod.Body.GetILProcessor();
                processor.Body.Instructions.Add(processor.Create(OpCodes.Ldstr, typeDefinition.Name));
                processor.Body.Instructions.Add(processor.Create(OpCodes.Ret));

                typeDefinition.Methods.Add(newGetTypeNameMethod);
            }

            m_MainModule.RemoveRecursiveReferences();
        }
    }
}