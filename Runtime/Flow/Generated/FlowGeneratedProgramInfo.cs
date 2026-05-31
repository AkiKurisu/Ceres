using System;
using System.Collections.Generic;
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

        public long generatedUtcTicks;

        public FlowGeneratedFunctionDependencyInfo[] functionDependencies = Array.Empty<FlowGeneratedFunctionDependencyInfo>();

        public bool IsCurrent(FlowGraphData graphData)
        {
            if (!enabled ||
                generatorVersion != FlowGeneratedRuntimeUtility.CurrentProgramInfoVersion ||
                string.IsNullOrEmpty(GetProgramId()) ||
                string.IsNullOrEmpty(graphHash) ||
                graphHash != FlowGeneratedRuntimeUtility.CalculateGraphHash(graphData))
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
        public const int CurrentProgramInfoVersion = 4;

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

        public static T GetRequiredSharedVariable<T>(Blackboard blackboard, string variableName)
            where T : SharedVariable
        {
            return blackboard.GetSharedVariable(variableName) as T;
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

        public static IFlowExecutableProgram CreateExecutableProgram(IFlowGraphContainer container,
            FlowGeneratedProgramInfo info)
        {
            var graphData = container.GetFlowGraphData();
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
