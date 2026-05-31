using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Ceres.Graph.Flow;
using Ceres.Utilities;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal static class FlowCSharpRuntimeGenerationDriver
    {
        private static readonly string[] RegistryUsingLines =
        {
            "using Ceres.Graph.Flow;",
            "using UnityEngine;"
        };

        public static void GenerateAllEnabledAssets()
        {
            var assets = FindFlowGraphAssets().Where(asset => asset.generatedRuntimeInfo.enabled).ToArray();
            GenerateAssets(assets);
        }

        public static void GenerateAssets(IEnumerable<FlowGraphScriptableObjectBase> assets)
        {
            var generatedCount = 0;
            foreach (var asset in assets)
            {
                GenerateAssetProgramSource(asset);
                generatedCount++;
            }

            GenerateRegistrySourceFile();
            AssetDatabase.Refresh();
            CeresLogger.Log($"Generated C# runtime for {generatedCount} FlowGraph asset(s).");
        }

        public static void GenerateAsset(FlowGraphScriptableObjectBase asset)
        {
            GenerateAssetProgramSource(asset);
            GenerateRegistrySourceFile();
        }

        public static void GenerateContainer(IFlowGeneratedRuntimeContainer container)
        {
            if (container?.Object is FlowGraphScriptableObjectBase asset)
            {
                GenerateAsset(asset);
                return;
            }

            GenerateManualContainer(container);
        }

        public static void GenerateManualContainer(IFlowGeneratedRuntimeContainer container)
        {
            GenerateManualProgramSource(container);
            GenerateRegistrySourceFile();
        }

        public static void GenerateManualContainers(IEnumerable<IFlowGeneratedRuntimeContainer> containers)
        {
            var generatedCount = 0;
            foreach (var container in containers)
            {
                GenerateManualProgramSource(container);
                generatedCount++;
            }

            GenerateRegistrySourceFile();
            AssetDatabase.Refresh();
            CeresLogger.Log($"Generated C# runtime for {generatedCount} GenerateFlow object(s).");
        }

        public static void SynchronizeManualGeneratedRuntime(IFlowGeneratedRuntimeContainer container)
        {
            UpdateManualManifestEntry(container);
            GenerateRegistrySourceFile();
        }

        public static void ValidateGeneratedAsset(FlowGraphScriptableObjectBase asset)
        {
            ValidateGeneratedContainer(asset, asset.generatedRuntimeInfo, asset.name);
        }

        public static void ValidateGeneratedManifestEntries()
        {
            if (!FlowGeneratedRuntimeUtility.UsesGeneratedRuntimeProgram)
            {
                return;
            }

            foreach (var entry in ReadManifest().Entries)
            {
                if (!TryResolveManifestContainer(entry, out var container, out var contextObject))
                {
                    throw new BuildFailedException(
                        $"Generated C# runtime object {entry.objectName} is missing. Run {FlowCSharpRuntimeGenerator.GenerateRuntimeMenuPath} before building.");
                }

                var info = container.GeneratedRuntimeInfo;
                if (info == null || !info.enabled) continue;
                if (!string.Equals(info.GetProgramId(), entry.programId, StringComparison.Ordinal))
                {
                    throw new BuildFailedException(
                        $"Generated C# runtime for {contextObject.name} has a mismatched program id. Run {FlowCSharpRuntimeGenerator.GenerateRuntimeMenuPath} before building.");
                }

                ValidateGeneratedContainer(container, info, contextObject.name);
            }
        }

        internal static IEnumerable<IFlowGeneratedRuntimeContainer> FindSelectedManualGeneratedRuntimeContainers()
        {
            var visited = new HashSet<int>();
            foreach (var selected in Selection.objects)
            {
                foreach (var container in EnumerateGeneratedRuntimeContainers(selected))
                {
                    if (container.Object is FlowGraphScriptableObjectBase) continue;
                    if (container.Object == null || !visited.Add(container.Object.GetInstanceID())) continue;
                    yield return container;
                }
            }
        }

        private static void GenerateAssetProgramSource(FlowGraphScriptableObjectBase asset)
        {
            if (!asset)
            {
                throw new FlowCSharpRuntimeGenerationException("Can not generate C# runtime for null asset.");
            }

            var path = AssetDatabase.GetAssetPath(asset);
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                throw new FlowCSharpRuntimeGenerationException($"Can not resolve asset guid for {asset.name}.");
            }

            var identity = GeneratedProgramIdentity.CreateForAsset(asset.name, guid);
            GenerateProgramSource(asset, asset.generatedRuntimeInfo, identity, asset.name, guid);
            SaveContainerObject(asset);
        }

        private static void GenerateManualProgramSource(IFlowGeneratedRuntimeContainer container)
        {
            if (container?.Object == null)
            {
                throw new FlowCSharpRuntimeGenerationException("Can not generate C# runtime for null object.");
            }

            if (container.Object is FlowGraphScriptableObjectBase asset)
            {
                GenerateAssetProgramSource(asset);
                return;
            }

            var programId = GetManualProgramId(container);
            var identity = GeneratedProgramIdentity.CreateForManual(container.Object.name, programId);
            GenerateProgramSource(container, container.GeneratedRuntimeInfo, identity, container.Object.name);
            UpdateManualManifestEntry(container);
            SaveContainerObject(container.Object);
        }

        private static void GenerateProgramSource(IFlowGraphContainer container, FlowGeneratedProgramInfo info,
            GeneratedProgramIdentity identity, string displayName, string assetGuid = null)
        {
            var graphData = container.GetFlowGraphData();
            if (graphData == null)
            {
                throw new FlowCSharpRuntimeGenerationException($"{displayName} has no graph data.");
            }

            var graphHash = FlowGeneratedRuntimeUtility.CalculateGraphHash(graphData);
            var generatorProfile = FlowGeneratedRuntimeUtility.CurrentGeneratedRuntimeProfile;
            var cancellationMode = FlowGeneratedRuntimeUtility.CurrentGeneratedRuntimeCancellationMode;
            var variableStorageMode = FlowGeneratedRuntimeUtility.CurrentGeneratedRuntimeVariableStorageMode;
            var serializedTypeMode = FlowGeneratedRuntimeUtility.CurrentGeneratedRuntimeSerializedTypeMode;

            info.enabled = true;
            info.generatorVersion = FlowGeneratedRuntimeUtility.CurrentProgramInfoVersion;
            info.programId = identity.ProgramId;
            info.assetGuid = assetGuid;
            info.graphHash = graphHash;
            info.generatorProfile = generatorProfile;
            info.generatorCancellationMode = cancellationMode;
            info.generatorVariableStorageMode = variableStorageMode;
            info.generatorSerializedTypeMode = serializedTypeMode;
            info.generatorOptionsHash =
                FlowGeneratedRuntimeUtility.CalculateGeneratedRuntimeOptionsHash(generatorProfile, cancellationMode,
                    variableStorageMode, serializedTypeMode);
            info.generatedUtcTicks = DateTime.UtcNow.Ticks;

            if (!FlowGeneratedRuntimeUtility.UsesGeneratedRuntimeProgram)
            {
                info.generatedTypeName = string.Empty;
                info.functionDependencies = Array.Empty<FlowGeneratedFunctionDependencyInfo>();
                DeleteGeneratedAsset(identity.SourcePath);
                return;
            }

            var generated = FlowCSharpRuntimeGenerator.GenerateSource(displayName, graphData, identity.ClassName);
            WriteGeneratedSource(identity.SourcePath, generated.Source);

            info.generatedTypeName = identity.TypeName;
            info.functionDependencies = generated.FunctionDependencies;
        }

        private static void ValidateGeneratedContainer(IFlowGraphContainer container, FlowGeneratedProgramInfo info,
            string displayName)
        {
            var graphData = container.GetFlowGraphData();
            if (info == null || !info.enabled) return;
            if (!info.IsCurrent(graphData))
            {
                throw new BuildFailedException(
                    $"Generated C# runtime for {displayName} is missing or stale. Run {FlowCSharpRuntimeGenerator.GenerateRuntimeMenuPath} before building.");
            }

            if (!FlowGeneratedRuntimeUtility.UsesGeneratedRuntimeProgram)
            {
                return;
            }

            ValidateGeneratedSourceFiles(info, displayName);
            var expectedDependencies = FlowCSharpRuntimeGenerator.ValidateSupport(displayName, graphData);
            ValidateFunctionDependencyRecords(displayName, info, expectedDependencies);
        }

        private static void ValidateGeneratedSourceFiles(FlowGeneratedProgramInfo info, string displayName)
        {
            var identity = ParseRequiredGeneratedProgramIdentity(info, displayName);
            RequireGeneratedSourceFile(identity.SourcePath,
                $"Generated C# runtime source for {displayName} is missing. Run {FlowCSharpRuntimeGenerator.GenerateRuntimeMenuPath} before building.");
            RequireGeneratedSourceFile(GetGeneratedRegistrySourcePath(),
                $"Generated C# runtime registry is missing. Run {FlowCSharpRuntimeGenerator.GenerateRuntimeMenuPath} before building.");
            ValidateGeneratedRegistrySource();
        }

        private static void ValidateFunctionDependencyRecords(string displayName, FlowGeneratedProgramInfo info,
            FlowGeneratedFunctionDependencyInfo[] expectedDependencies)
        {
            if (expectedDependencies.Length == 0) return;

            var actualDependencies = info.functionDependencies ?? Array.Empty<FlowGeneratedFunctionDependencyInfo>();
            foreach (var expected in expectedDependencies)
            {
                var actual = actualDependencies.FirstOrDefault(dependency =>
                    dependency != null && dependency.assetGuid == expected.assetGuid);
                if (actual != null && actual.graphHash == expected.graphHash)
                {
                    continue;
                }

                throw new BuildFailedException(
                    $"Generated C# runtime for {displayName} is missing or stale for function asset {expected.assetName}. Run {FlowCSharpRuntimeGenerator.GenerateRuntimeMenuPath} before building.");
            }
        }

        private static IEnumerable<FlowGraphScriptableObjectBase> FindFlowGraphAssets()
        {
            return AssetDatabase.FindAssets($"t:{nameof(FlowGraphScriptableObjectBase)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<FlowGraphScriptableObjectBase>)
                .Where(asset => asset);
        }

        private static void GenerateRegistrySourceFile()
        {
            var entries = FindGeneratedProgramRegistrations().ToArray();
            if (entries.Length == 0)
            {
                DeleteGeneratedRegistrySourceFile();
                return;
            }

            WriteGeneratedSource(GetGeneratedRegistrySourcePath(), GenerateRegistrySource(entries));
        }

        private static IEnumerable<GeneratedProgramRegistration> FindGeneratedProgramRegistrations()
        {
            foreach (var asset in FindFlowGraphAssets())
            {
                if (TryCreateGeneratedProgramRegistration(asset.generatedRuntimeInfo, out var registration))
                    yield return registration;
            }

            foreach (var entry in ReadManifest().Entries)
            {
                if (!TryResolveManifestContainer(entry, out var container, out _) ||
                    !string.Equals(container.GeneratedRuntimeInfo.GetProgramId(), entry.programId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryCreateGeneratedProgramRegistration(container.GeneratedRuntimeInfo, out var registration))
                    yield return registration;
            }
        }

        private static string GenerateRegistrySource(IEnumerable<GeneratedProgramRegistration> entries)
        {
            var body = new StringBuilder();
            FlowCSharpRuntimeGenerator.AppendGeneratedPreamble(body, RegistryUsingLines);
            body.AppendLine($"namespace {FlowCSharpRuntimeGenerator.GeneratedNamespace}");
            body.AppendLine("{");
            body.AppendLine($"    internal static class {FlowCSharpRuntimeGenerator.GeneratedRegistryClassName}");
            body.AppendLine("    {");
            body.AppendLine("        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]");
            body.AppendLine("        private static void RegisterGeneratedPrograms()");
            body.AppendLine("        {");
            body.AppendLine("            FlowGeneratedProgramRegistry.Clear();");
            foreach (var entry in entries.OrderBy(entry => entry.ProgramId, StringComparer.Ordinal))
            {
                body.AppendLine($"            FlowGeneratedProgramRegistry.Register(\"{FlowCSharpRuntimeGenerator.Escape(entry.ProgramId)}\", \"{FlowCSharpRuntimeGenerator.Escape(entry.GraphHash)}\", \"{FlowCSharpRuntimeGenerator.Escape(entry.GeneratedTypeName)}\", graphData => new {entry.TypeReference}(graphData));");
            }

            body.AppendLine("        }");
            body.AppendLine("    }");
            body.AppendLine("}");
            return body.ToString();
        }

        private static GeneratedProgramIdentity ParseRequiredGeneratedProgramIdentity(FlowGeneratedProgramInfo info,
            string displayName)
        {
            if (GeneratedProgramIdentity.TryParse(info.generatedTypeName, out var identity))
            {
                return identity;
            }

            throw new BuildFailedException(
                $"Generated C# runtime for {displayName} has an invalid generated type name. Run {FlowCSharpRuntimeGenerator.GenerateRuntimeMenuPath} before building.");
        }

        private static bool TryCreateGeneratedProgramRegistration(FlowGeneratedProgramInfo info,
            out GeneratedProgramRegistration registration)
        {
            registration = default;
            if (!IsGeneratedProgramRegistrationCandidate(info) ||
                !GeneratedProgramIdentity.TryParse(info.generatedTypeName, out var identity) ||
                !File.Exists(identity.SourcePath))
            {
                return false;
            }

            registration = new GeneratedProgramRegistration(
                info.GetProgramId(),
                info.graphHash,
                info.generatedTypeName,
                identity.TypeReference);
            return true;
        }

        private static bool IsGeneratedProgramRegistrationCandidate(FlowGeneratedProgramInfo info)
        {
            return info != null &&
                   info.enabled &&
                   FlowGeneratedRuntimeUtility.UsesGeneratedRuntimeProgram &&
                   info.generatorVersion == FlowGeneratedRuntimeUtility.CurrentProgramInfoVersion &&
                   FlowGeneratedRuntimeUtility.AreGeneratedRuntimeOptionsCurrent(info) &&
                   !string.IsNullOrEmpty(info.GetProgramId()) &&
                   !string.IsNullOrEmpty(info.graphHash) &&
                   !string.IsNullOrEmpty(info.generatedTypeName);
        }

        private static void RequireGeneratedSourceFile(string path, string errorMessage)
        {
            if (File.Exists(path)) return;
            throw new BuildFailedException(errorMessage);
        }

        private static void ValidateGeneratedRegistrySource()
        {
            var path = GetGeneratedRegistrySourcePath();
            var actualSource = File.ReadAllText(path, Encoding.UTF8);
            var expectedSource = GenerateRegistrySource(FindGeneratedProgramRegistrations());
            if (HasSameGeneratedSource(actualSource, expectedSource)) return;

            throw new BuildFailedException(
                $"Generated C# runtime registry is stale. Run {FlowCSharpRuntimeGenerator.GenerateRuntimeMenuPath} before building.");
        }

        private static bool HasSameGeneratedSource(string left, string right)
        {
            return NormalizeLineEndings(left) == NormalizeLineEndings(right);
        }

        private static string NormalizeLineEndings(string value)
        {
            return value?
                .Replace("\r\n", "\n")
                .Replace("\r", "\n") ?? string.Empty;
        }

        private static void WriteGeneratedSource(string path, string source)
        {
            Directory.CreateDirectory(FlowCSharpRuntimeGenerator.OutputDirectory);
            File.WriteAllText(path, source, Encoding.UTF8);
        }

        private static void DeleteGeneratedRegistrySourceFile()
        {
            DeleteGeneratedAsset(GetGeneratedRegistrySourcePath());
            DeleteGeneratedOutputDirectoryIfEmpty();
        }

        private static void DeleteGeneratedOutputDirectoryIfEmpty()
        {
            if (!Directory.Exists(FlowCSharpRuntimeGenerator.OutputDirectory) ||
                Directory.EnumerateFileSystemEntries(FlowCSharpRuntimeGenerator.OutputDirectory).Any())
            {
                return;
            }

            DeleteGeneratedAsset(FlowCSharpRuntimeGenerator.OutputDirectory);
        }

        private static void DeleteGeneratedAsset(string path)
        {
            if ((File.Exists(path) || Directory.Exists(path)) &&
                !AssetDatabase.DeleteAsset(path))
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path);
                }
            }

            DeleteFileIfExists($"{path}.meta");
        }

        private static void DeleteFileIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string GetGeneratedProgramSourcePath(string className)
        {
            return Path.Combine(FlowCSharpRuntimeGenerator.OutputDirectory,
                $"{className}{FlowCSharpRuntimeGenerator.GeneratedSourceExtension}").Replace("\\", "/");
        }

        private static string GetGeneratedRegistrySourcePath()
        {
            return Path.Combine(FlowCSharpRuntimeGenerator.OutputDirectory,
                FlowCSharpRuntimeGenerator.GeneratedRegistryFileName).Replace("\\", "/");
        }

        private static string GetGeneratedManifestPath()
        {
            return Path.Combine(FlowCSharpRuntimeGenerator.OutputDirectory,
                FlowCSharpRuntimeGenerator.GeneratedManifestFileName).Replace("\\", "/");
        }

        private static IEnumerable<IFlowGeneratedRuntimeContainer> EnumerateGeneratedRuntimeContainers(UObject selected)
        {
            if (selected is IFlowGeneratedRuntimeContainer container)
            {
                yield return container;
            }

            if (selected is GameObject gameObject)
            {
                foreach (var component in gameObject.GetComponents<MonoBehaviour>())
                {
                    if (component is IFlowGeneratedRuntimeContainer componentContainer)
                    {
                        yield return componentContainer;
                    }
                }
            }
        }

        private static string GetManualProgramId(IFlowGeneratedRuntimeContainer container)
        {
            var id = GlobalObjectId.GetGlobalObjectIdSlow(container.Object).ToString();
            if (string.IsNullOrEmpty(id))
            {
                throw new FlowCSharpRuntimeGenerationException(
                    $"Can not resolve stable program id for {container.Object.name}.");
            }

            return id;
        }

        private static void SaveContainerObject(UObject target)
        {
            if (!target) return;
            EditorUtility.SetDirty(target);

            var path = AssetDatabase.GetAssetPath(target);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.SaveAssetIfDirty(target);
                return;
            }

            if (target is Component component && component.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }
        }

        private static void UpdateManualManifestEntry(IFlowGeneratedRuntimeContainer container)
        {
            if (container?.Object == null || container.Object is FlowGraphScriptableObjectBase) return;

            var manifest = ReadManifest();
            var programId = GetManualProgramId(container);
            var info = container.GeneratedRuntimeInfo;
            manifest.Entries.RemoveAll(item => string.Equals(item.programId, programId, StringComparison.Ordinal));
            if (!IsGeneratedProgramRegistrationCandidate(info) ||
                !string.Equals(info.GetProgramId(), programId, StringComparison.Ordinal))
            {
                WriteManifest(manifest);
                return;
            }

            manifest.Entries.Add(new GeneratedProgramManifestEntry
            {
                programId = programId,
                objectName = container.Object.name,
                graphHash = info.graphHash,
                generatedTypeName = info.generatedTypeName
            });
            WriteManifest(manifest);
        }

        private static GeneratedProgramManifest ReadManifest()
        {
            var path = GetGeneratedManifestPath();
            if (!File.Exists(path))
            {
                return new GeneratedProgramManifest();
            }

            var manifest = JsonUtility.FromJson<GeneratedProgramManifest>(File.ReadAllText(path, Encoding.UTF8));
            return manifest ?? new GeneratedProgramManifest();
        }

        private static void WriteManifest(GeneratedProgramManifest manifest)
        {
            if (manifest == null || manifest.Entries.Count == 0)
            {
                DeleteGeneratedManifestFile();
                return;
            }

            Directory.CreateDirectory(FlowCSharpRuntimeGenerator.OutputDirectory);
            File.WriteAllText(GetGeneratedManifestPath(), JsonUtility.ToJson(manifest, true), Encoding.UTF8);
        }

        private static void DeleteGeneratedManifestFile()
        {
            DeleteGeneratedAsset(GetGeneratedManifestPath());
            DeleteGeneratedOutputDirectoryIfEmpty();
        }

        private static bool TryResolveManifestContainer(GeneratedProgramManifestEntry entry,
            out IFlowGeneratedRuntimeContainer container, out UObject contextObject)
        {
            container = null;
            contextObject = null;
            if (entry == null || string.IsNullOrEmpty(entry.programId) ||
                !GlobalObjectId.TryParse(entry.programId, out var globalObjectId))
            {
                return false;
            }

            contextObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
            if (!contextObject)
            {
                return false;
            }

            if (contextObject is IFlowGeneratedRuntimeContainer directContainer)
            {
                container = directContainer;
                return true;
            }

            if (contextObject is GameObject gameObject)
            {
                container = gameObject.GetComponents<MonoBehaviour>()
                    .OfType<IFlowGeneratedRuntimeContainer>()
                    .FirstOrDefault(candidate =>
                        string.Equals(candidate.GeneratedRuntimeInfo.GetProgramId(), entry.programId,
                            StringComparison.Ordinal));
            }

            return container != null;
        }

        private static bool IsQualifiedIdentifier(string value)
        {
            var parts = value.Split('.');
            return parts.Length > 0 && parts.All(IsIdentifier);
        }

        private static bool IsIdentifier(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                   !char.IsDigit(value[0]) &&
                   value.All(c => c == '_' || char.IsLetterOrDigit(c));
        }

        private readonly struct GeneratedProgramIdentity
        {
            public readonly string ProgramId;

            public readonly string ClassName;

            public readonly string TypeName;

            public string TypeReference => $"global::{TypeName}";

            public string SourcePath => GetGeneratedProgramSourcePath(ClassName);

            private GeneratedProgramIdentity(string programId, string className, string typeName)
            {
                ProgramId = programId;
                ClassName = className;
                TypeName = typeName;
            }

            public static GeneratedProgramIdentity CreateForAsset(string assetName, string assetGuid)
            {
                var className =
                    $"{FlowCSharpRuntimeGenerator.GeneratedProgramClassPrefix}{FlowCSharpRuntimeGenerator.SanitizeIdentifier(assetName)}_{assetGuid[..8]}";
                return new GeneratedProgramIdentity(assetGuid, className,
                    $"{FlowCSharpRuntimeGenerator.GeneratedNamespace}.{className}");
            }

            public static GeneratedProgramIdentity CreateForManual(string objectName, string programId)
            {
                var suffix = programId.Hash64().ToString("X16", CultureInfo.InvariantCulture)[..8];
                var className =
                    $"{FlowCSharpRuntimeGenerator.GeneratedProgramClassPrefix}{FlowCSharpRuntimeGenerator.SanitizeIdentifier(objectName)}_{suffix}";
                return new GeneratedProgramIdentity(programId, className,
                    $"{FlowCSharpRuntimeGenerator.GeneratedNamespace}.{className}");
            }

            public static bool TryParse(string typeName, out GeneratedProgramIdentity identity)
            {
                identity = default;
                if (string.IsNullOrEmpty(typeName) ||
                    !typeName.StartsWith($"{FlowCSharpRuntimeGenerator.GeneratedNamespace}.", StringComparison.Ordinal) ||
                    !IsQualifiedIdentifier(typeName))
                {
                    return false;
                }

                var className = typeName[(typeName.LastIndexOf('.') + 1)..];
                identity = new GeneratedProgramIdentity(null, className, typeName);
                return true;
            }
        }

        private readonly struct GeneratedProgramRegistration
        {
            public readonly string ProgramId;

            public readonly string GraphHash;

            public readonly string GeneratedTypeName;

            public readonly string TypeReference;

            public GeneratedProgramRegistration(string programId, string graphHash, string generatedTypeName,
                string typeReference)
            {
                ProgramId = programId;
                GraphHash = graphHash;
                GeneratedTypeName = generatedTypeName;
                TypeReference = typeReference;
            }
        }

        [Serializable]
        private sealed class GeneratedProgramManifest
        {
            public List<GeneratedProgramManifestEntry> entries = new();

            public List<GeneratedProgramManifestEntry> Entries => entries ??= new List<GeneratedProgramManifestEntry>();
        }

        [Serializable]
        private sealed class GeneratedProgramManifestEntry
        {
            public string programId;

            public string objectName;

            public string graphHash;

            public string generatedTypeName;
        }
    }
}
