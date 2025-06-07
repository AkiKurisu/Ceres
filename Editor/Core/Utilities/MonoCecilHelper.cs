// Ported from UnityEditor
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;

namespace Ceres.Editor.Utilities
{
    internal static class MonoCecilHelper
    {
        public static SequencePoint GetMethodFirstSequencePoint(MethodDefinition methodDefinition)
        {
            if (methodDefinition == null)
            {
                CeresLogger.LogWarning(
                    "MethodDefinition cannot be null. Check if any method was found by name in its declaring type TypeDefinition.");
                return null;
            }

            if (!methodDefinition.HasBody || !methodDefinition.Body.Instructions.Any() ||
                methodDefinition.DebugInformation == null)
            {
                CeresLogger.LogWarning("To get SequencePoints MethodDefinition for " + methodDefinition.Name +
                                       " must have MethodBody, DebugInformation and Instructions.");
                return null;
            }

            if (!methodDefinition.DebugInformation.HasSequencePoints)
            {
                foreach (TypeDefinition nestedType in methodDefinition.DeclaringType.NestedTypes)
                {
                    foreach (MethodDefinition method in nestedType.Methods)
                    {
                        if (method.DebugInformation != null &&
                            method.DebugInformation.StateMachineKickOffMethod == methodDefinition && method.HasBody &&
                            method.Body.Instructions.Count > 0)
                        {
                            methodDefinition = method;
                            goto getSequencePoint;
                        }
                    }
                }

                CeresLogger.LogWarning("No SequencePoints for MethodDefinition for " + methodDefinition.Name);
                return null;
            }

            getSequencePoint:
            return methodDefinition.DebugInformation.SequencePoints.FirstOrDefault(x => !x.IsHidden);
        }

        private static AssemblyDefinition ReadAssembly(string assemblyPath)
        {
            using var assemblyResolver = new DefaultAssemblyResolver();
            assemblyResolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath));
            var parameters = new ReaderParameters
            {
                ReadSymbols = true,
                SymbolReaderProvider = new DefaultSymbolReaderProvider(false),
                AssemblyResolver = assemblyResolver,
                ReadingMode = ReadingMode.Deferred
            };
            try
            {
                return AssemblyDefinition.ReadAssembly(assemblyPath, parameters);
            }
            catch (Exception ex)
            {
                CeresLogger.LogError(ex.Message);
                return null;
            }
        }

        public static (string filePath, int lineNumber) TryGetCecilFileOpenInfo(Type type, MethodInfo methodInfo)
        {
            // May not find sequence point in unity modules
            using( CeresLogger.LogScope(LogType.Error))
            {
                using var assemblyDefinition = ReadAssembly(type.Assembly.Location);
                var methodDefinition =
                    assemblyDefinition.MainModule.LookupToken(methodInfo.MetadataToken) as MethodDefinition;
                var firstSequencePoint = GetMethodFirstSequencePoint(methodDefinition);
                if (firstSequencePoint != null)
                {
                    return (firstSequencePoint.Document.Url, firstSequencePoint.StartLine);
                }

                return (default, default);
            }
        }
    }
}