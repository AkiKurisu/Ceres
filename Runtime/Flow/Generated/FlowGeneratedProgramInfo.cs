using System;
using System.Collections.Generic;
using System.Reflection;
using Ceres.Utilities;
using Chris.Serialization;
using Chris.Events;

namespace Ceres.Graph.Flow
{
    [Serializable]
    public sealed class FlowGeneratedProgramInfo
    {
        public bool enabled;

        public int generatorVersion;

        public string programId;

        public string assetGuid;

        public string graphHash;

        public string generatedTypeName;

        public FlowGeneratedRuntimeProfile generatorProfile = FlowGeneratedRuntimeProfile.OptimizedSafe;

        public FlowGeneratedRuntimeCancellationMode generatorCancellationMode =
            FlowGeneratedRuntimeCancellationMode.Auto;

        public FlowGeneratedRuntimeVariableStorageMode generatorVariableStorageMode =
            FlowGeneratedRuntimeVariableStorageMode.LocalFieldsForUnshared;

        public FlowGeneratedRuntimeSerializedTypeMode generatorSerializedTypeMode =
            FlowGeneratedRuntimeSerializedTypeMode.DirectType;

        public string generatorOptionsHash;

        public long generatedUtcTicks;

        public FlowGeneratedFunctionDependencyInfo[] functionDependencies = Array.Empty<FlowGeneratedFunctionDependencyInfo>();

        public bool IsCurrent(FlowGraphData graphData)
        {
            if (!enabled ||
                generatorVersion != FlowGeneratedRuntimeUtility.CurrentProgramInfoVersion ||
                string.IsNullOrEmpty(GetProgramId()) ||
                string.IsNullOrEmpty(graphHash) ||
                graphHash != FlowGeneratedRuntimeUtility.CalculateGraphHash(graphData) ||
                !FlowGeneratedRuntimeUtility.AreGeneratedRuntimeOptionsCurrent(this))
            {
                return false;
            }

#if UNITY_EDITOR
            return AreFunctionDependenciesCurrent();
#else
            return true;
#endif
        }

        public string GetProgramId()
        {
            return string.IsNullOrEmpty(programId) ? assetGuid : programId;
        }

#if UNITY_EDITOR
        public bool AreFunctionDependenciesCurrent()
        {
            if (functionDependencies == null) return false;

            foreach (var dependency in functionDependencies)
            {
                if (dependency == null || !dependency.IsCurrent())
                {
                    return false;
                }
            }

            return true;
        }
#endif
    }

    [Serializable]
    public sealed class FlowGeneratedFunctionDependencyInfo
    {
        public string assetGuid;

        public string assetName;

        public string graphHash;

        public FlowGeneratedFunctionDependencyInfo()
        {
        }

        public FlowGeneratedFunctionDependencyInfo(string assetGuid, string assetName, string graphHash)
        {
            this.assetGuid = assetGuid;
            this.assetName = assetName;
            this.graphHash = graphHash;
        }

#if UNITY_EDITOR
        public bool IsCurrent()
        {
            if (string.IsNullOrEmpty(assetGuid) || string.IsNullOrEmpty(graphHash))
            {
                return false;
            }

            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(assetGuid);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<FlowGraphFunctionAsset>(path);
            if (!asset)
            {
                return false;
            }

            var graphData = ((IFlowGraphContainer)asset).GetFlowGraphData();
            return graphHash == FlowGeneratedRuntimeUtility.CalculateGraphHash(graphData);
        }
#endif
    }

    public static class FlowGeneratedRuntimeUtility
    {
        public const int CurrentProgramInfoVersion = 2;

        public static FlowGeneratedRuntimeProfile CurrentGeneratedRuntimeProfile =>
            FlowConfig.Get().generatedRuntimeProfile;

        public static FlowGeneratedRuntimeCancellationMode CurrentGeneratedRuntimeCancellationMode =>
            FlowConfig.Get().generatedRuntimeCancellationMode;

        public static FlowGeneratedRuntimeVariableStorageMode CurrentGeneratedRuntimeVariableStorageMode =>
            FlowConfig.Get().generatedRuntimeVariableStorageMode;

        public static FlowGeneratedRuntimeSerializedTypeMode CurrentGeneratedRuntimeSerializedTypeMode =>
            FlowConfig.Get().generatedRuntimeSerializedTypeMode;

        public static bool UsesGeneratedRuntimeProgram =>
            CurrentGeneratedRuntimeProfile != FlowGeneratedRuntimeProfile.Debuggable;

        public static string CurrentGeneratedRuntimeOptionsHash =>
            CalculateGeneratedRuntimeOptionsHash(CurrentGeneratedRuntimeProfile,
                CurrentGeneratedRuntimeCancellationMode,
                CurrentGeneratedRuntimeVariableStorageMode,
                CurrentGeneratedRuntimeSerializedTypeMode);

        public static string CalculateGeneratedRuntimeOptionsHash(FlowGeneratedRuntimeProfile profile,
            FlowGeneratedRuntimeCancellationMode cancellationMode,
            FlowGeneratedRuntimeVariableStorageMode variableStorageMode,
            FlowGeneratedRuntimeSerializedTypeMode serializedTypeMode)
        {
            return $"{(int)profile}:{(int)cancellationMode}:{(int)variableStorageMode}:{(int)serializedTypeMode}"
                .Hash64().ToString("X16");
        }

        public static bool AreGeneratedRuntimeOptionsCurrent(FlowGeneratedProgramInfo info)
        {
            return info != null &&
                   info.generatorProfile == CurrentGeneratedRuntimeProfile &&
                   info.generatorCancellationMode == CurrentGeneratedRuntimeCancellationMode &&
                   info.generatorVariableStorageMode == CurrentGeneratedRuntimeVariableStorageMode &&
                   info.generatorSerializedTypeMode == CurrentGeneratedRuntimeSerializedTypeMode &&
                   string.Equals(info.generatorOptionsHash, CurrentGeneratedRuntimeOptionsHash,
                       StringComparison.Ordinal);
        }

        public static string CalculateGraphHash(FlowGraphData graphData)
        {
            if (graphData == null) return string.Empty;

            var clone = graphData.CloneT<FlowGraphData>();
            ClearTimestamps(clone);
            return clone.ToJson().Hash64().ToString("X16");
        }

        public static Blackboard CreateBlackboard(FlowGraphData graphData)
        {
            var variables = new List<SharedVariable>(graphData?.variableData?.Length ?? 0);
            if (graphData?.variableData != null)
            {
                foreach (var data in graphData.variableData)
                {
                    var variableType = data.variableType.ToType();
                    var variable = data.Deserialize(variableType);
                    if (variable != null)
                    {
                        variables.Add(variable);
                    }
                }
            }

            var blackboard = Blackboard.Create<Blackboard>(variables, false);
            blackboard.LinkToGlobal();
            return blackboard;
        }

        public static TValue GetLocalVariableValue<TVariable, TValue>(FlowGraphData graphData, string variableName)
            where TVariable : SharedVariable
        {
            if (TryGetLocalVariableValue<TVariable, TValue>(graphData, variableName, out var value))
            {
                return value;
            }

            return default;
        }

        public static bool TryGetLocalVariableValue<TVariable, TValue>(FlowGraphData graphData, string variableName,
            out TValue value)
            where TVariable : SharedVariable
        {
            value = default;
            if (graphData?.variableData == null || string.IsNullOrEmpty(variableName))
            {
                return false;
            }

            foreach (var data in graphData.variableData)
            {
                var variableType = data.variableType.ToType();
                if (variableType == null || !typeof(TVariable).IsAssignableFrom(variableType))
                {
                    continue;
                }

                if (data.Deserialize(variableType) is not TVariable variable ||
                    !string.Equals(variable.Name, variableName, StringComparison.Ordinal))
                {
                    continue;
                }

                return TryReadLocalVariableValue(variable, out value);
            }

            return false;
        }

        public static TValue GetNodeLocalVariableValue<TValue>(FlowGraphData graphData, string nodeGuid,
            string fieldName, int index)
        {
            if (TryGetNodeLocalVariableValue<TValue>(graphData, nodeGuid, fieldName, index, out var value))
            {
                return value;
            }

            return default;
        }

        public static bool TryGetNodeLocalVariableValue<TValue>(FlowGraphData graphData, string nodeGuid,
            string fieldName, int index, out TValue value)
        {
            value = default;
            if (graphData?.nodeData == null ||
                string.IsNullOrEmpty(nodeGuid) ||
                string.IsNullOrEmpty(fieldName))
            {
                return false;
            }

            foreach (var data in graphData.nodeData)
            {
                if (data == null || !string.Equals(data.guid, nodeGuid, StringComparison.Ordinal))
                {
                    continue;
                }

                var nodeType = ResolveGeneratedNodeType(data);
                if (nodeType == null || data.Deserialize(nodeType) is not CeresNode node)
                {
                    return false;
                }

                var field = GetFieldInHierarchy(nodeType, fieldName);
                if (field == null)
                {
                    return false;
                }

                var fieldValue = field.GetValue(node);
                if (!TryGetSharedVariableFromFieldValue(fieldValue, index, out var variable))
                {
                    return false;
                }

                return TryReadLocalVariableValue(variable, out value);
            }

            return false;
        }

        private static Type ResolveGeneratedNodeType(CeresNodeData data)
        {
            try
            {
                var nodeType = data.nodeType.ToType();
                if (nodeType == null ||
                    data.genericParameters == null ||
                    data.genericParameters.Length == 0 ||
                    !nodeType.IsGenericTypeDefinition)
                {
                    return nodeType;
                }

                var arguments = new Type[data.genericParameters.Length];
                for (var i = 0; i < arguments.Length; i++)
                {
                    arguments[i] = SerializedType.FromString(data.genericParameters[i]);
                    if (arguments[i] == null)
                    {
                        return null;
                    }
                }

                return nodeType.MakeGenericType(arguments);
            }
            catch
            {
                return null;
            }
        }

        private static FieldInfo GetFieldInHierarchy(Type type, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            while (type != null)
            {
                var field = type.GetField(fieldName, flags);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static bool TryGetSharedVariableFromFieldValue(object fieldValue, int index,
            out SharedVariable variable)
        {
            variable = null;
            if (index < 0)
            {
                variable = fieldValue as SharedVariable;
                return variable != null;
            }

            if (fieldValue is not System.Collections.IList list ||
                index >= list.Count)
            {
                return false;
            }

            variable = list[index] as SharedVariable;
            return variable != null;
        }

        private static bool TryReadLocalVariableValue<TValue>(SharedVariable variable, out TValue value)
        {
            value = default;
            if (variable == null || variable.IsShared || variable.IsGlobal)
            {
                return false;
            }

            var rawValue = variable.GetValue();
            if (rawValue == null)
            {
                return !typeof(TValue).IsValueType || Nullable.GetUnderlyingType(typeof(TValue)) != null;
            }

            if (rawValue is TValue typedValue)
            {
                value = typedValue;
                return true;
            }

            try
            {
                value = (TValue)Convert.ChangeType(rawValue, typeof(TValue));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static T GetRequiredSharedVariable<T>(Blackboard blackboard, string variableName)
            where T : SharedVariable
        {
            return blackboard?.GetSharedVariable(variableName) as T;
        }

        public static void DisposeBlackboard(Blackboard blackboard)
        {
            if (blackboard == null) return;
            foreach (var variable in blackboard.SharedVariables)
            {
                variable?.Dispose();
            }
        }

        private static void ClearTimestamps(FlowGraphData graphData)
        {
#if UNITY_EDITOR
            graphData.saveTimestamp = 0;
#endif
            if (graphData.subGraphData == null) return;

            foreach (var subGraph in graphData.subGraphData)
            {
                if (subGraph?.graphData == null) continue;
#if UNITY_EDITOR
                subGraph.graphData.saveTimestamp = 0;
#endif
            }
        }

        public static bool IsUnityNull<T>(T value)
        {
            if (value is UnityEngine.Object unityObject)
            {
                return !unityObject;
            }

            return value == null;
        }

        public static TValue GetTargetOrDefault<TValue>(bool isStatic, bool isSelfTarget, TValue inputValue,
            UnityEngine.Object context)
        {
            if (isStatic) return default;
            if (!isSelfTarget) return inputValue;
            if (IsUnityNull(inputValue) && context is TValue target) return target;
            return inputValue;
        }

        public static TValue GetSelfTargetOrDefault<TValue>(bool isSelfTarget, TValue inputValue,
            UnityEngine.Object context)
        {
            if (!isSelfTarget) return inputValue;
            if (IsUnityNull(inputValue) && context is TValue target) return target;
            return inputValue;
        }

        public static TValue GetSubFlowArgument<TValue>(EventBase evtBase, int index)
        {
            if (evtBase is not ExecuteSubFlowEvent subFlowEvent ||
                subFlowEvent.Args == null ||
                index < 0 ||
                index >= subFlowEvent.Args.Count)
            {
                return default;
            }

            var argument = subFlowEvent.Args[index];
            if (argument is CeresPort<TValue> typedPort)
            {
                return typedPort.Value;
            }

            var value = argument.GetValue();
            if (value == null)
            {
                return default;
            }

            return value is TValue typedValue ? typedValue : (TValue)value;
        }

        public static void SetSubFlowReturn<TValue>(EventBase evtBase, TValue value)
        {
            if (evtBase is ExecuteSubFlowEvent subFlowEvent)
            {
                subFlowEvent.Return = new CeresPort<TValue>(value);
            }
        }

        public static void PrewarmSerializedType(SerializedTypeBase serializedType)
        {
            serializedType?.GetObjectType();
        }

        public static T[] CastArray<T>(Array source)
        {
            if (source == null)
            {
                return null;
            }

            if (source.Rank != 1)
            {
                throw new InvalidCastException("Ceres generated runtime only supports one-dimensional array return casts.");
            }

            var result = new T[source.Length];
            var lowerBound = source.GetLowerBound(0);
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = (T)source.GetValue(lowerBound + i);
            }

            return result;
        }

        public static T FindObjectOfType<T>() where T : UnityEngine.Object
        {
#if UNITY_6000_0_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<T>();
#else
            return UnityEngine.Object.FindObjectOfType<T>();
#endif
        }

        public static T GetOrAddComponent<T>(UnityEngine.GameObject gameObject) where T : UnityEngine.Component
        {
            if (!gameObject)
            {
                return null;
            }

            if (gameObject.TryGetComponent<T>(out var component))
            {
                return component;
            }

            return gameObject.AddComponent<T>();
        }

        public static IFlowExecutableProgram CreateExecutableProgram(IFlowGraphContainer container,
            FlowGeneratedProgramInfo info)
        {
            var graphData = container.GetFlowGraphData();
            if (!UsesGeneratedRuntimeProgram)
            {
                return CreateCompiledGraphProgram(container.GetFlowGraph());
            }

            if (FlowGeneratedProgramRegistry.TryCreate(info, graphData, out var generatedProgram))
            {
                return generatedProgram;
            }

#if UNITY_EDITOR
            if (info != null && info.enabled)
            {
                var objectName = container.Object ? container.Object.name : container.GetType().Name;
                CeresLogger.LogWarning(
                    $"Generated Flow runtime for {objectName} is missing or stale. Falling back to graph runtime in Editor.");
            }
#endif
            return CreateCompiledGraphProgram(container.GetFlowGraph());
        }

        public static IFlowExecutableProgram CreateCompiledGraphProgram(FlowGraph graph)
        {
            using var context = FlowGraphCompilationContext.GetPooled();
            using var compiler = CeresGraphCompiler.GetPooled(graph, context);
            graph.Compile(compiler);
            return graph;
        }
    }
}
