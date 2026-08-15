using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Ceres.Serialization.Editor
{
    public static class DynamicTypeBuilder
    {
        private const string kDynamicTypeBuilderAssemblyName = "Ceres.Emit";
        
        private static ModuleBuilder m_ModuleBuilder;
        
        private static readonly ConcurrentDictionary<string, Type> s_TypeCache = new();

        private static ModuleBuilder CreateModuleBuilder()
        {
            if (m_ModuleBuilder != null)
                return m_ModuleBuilder;

            var appDomain = AppDomain.CurrentDomain;
            var assemblyName = new AssemblyName(kDynamicTypeBuilderAssemblyName);
            var assemblyBuilder = appDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            m_ModuleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
            return m_ModuleBuilder;
        }

        public static Type MakeDerivedType(Type baseClass, Type parameterType)
        {
            ModuleBuilder moduleBuilder = CreateModuleBuilder();
            string tAssemblyName = parameterType.Assembly.GetName().Name;
            string typeName = $"{baseClass.Namespace}_{baseClass.Name}";

            if (baseClass.IsGenericType)
            {
                //Get rid of the '`N' after the class name for the # of generic args
                //TODO: If there are >= 10 args (highly unlikely) this will break (:
                typeName = typeName[..^2];
                typeName += $"_{string.Join("_", baseClass.GetGenericArguments().Select(t => t.FullName))}";
            }

            string typeNameWithoutAssembly = typeName.Replace('.', '_');

            typeName = $"{tAssemblyName}_{typeName}";
            typeName = typeName.Replace('.', '_');
            
            // Check cache first using the full type name
            if (s_TypeCache.TryGetValue(typeName, out var cachedType))
            {
                return cachedType;
            }
            
            // Also check cache for alternative naming pattern (for backward compatibility)
            // This matches the original logic that searched for types ending with typeNameWithoutAssembly
            var alternativeKey = $"{tAssemblyName}_{typeNameWithoutAssembly}";
            if (s_TypeCache.TryGetValue(alternativeKey, out var cachedTypeAlt))
            {
                return cachedTypeAlt;
            }

            var baseConstructor = baseClass.GetConstructor(BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance, null, new Type[0], null);
            var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Class | TypeAttributes.Public, baseClass);
            var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, null);
            var ilGenerator = constructor.GetILGenerator();

            if (baseConstructor != null)
            {
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Call, baseConstructor);
            }

            ilGenerator.Emit(OpCodes.Nop);
            ilGenerator.Emit(OpCodes.Nop);
            ilGenerator.Emit(OpCodes.Ret);
            var createdType = typeBuilder.CreateType();
            
            // Cache the created type
            s_TypeCache.TryAdd(typeName, createdType);
            s_TypeCache.TryAdd(alternativeKey, createdType);
            
            return createdType;
        }
    }
}
