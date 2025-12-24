using System;
using System.Collections.Generic;
using Ceres.Graph;
using Ceres.Graph.Flow;
using UnityEditor;
using UnityEngine;

namespace Ceres.Editor.Graph.Flow
{
    /// <summary>
    /// Manager for hot reloading FlowGraph instances during play mode
    /// </summary>
    [InitializeOnLoad]
    public static class FlowGraphHotReloadManager
    {
        private static readonly Dictionary<IFlowGraphContainer, long> ContainerTimestamps = new();
        
        private static bool _isInitialized;
        
        private static double _lastCheckTime;
        
        private static bool _isHotReloadEnabled;

        // Check interval in seconds (200ms = 0.2s)
        private const double CheckInterval = 0.2;

        /// <summary>
        /// Whether hot reload is currently enabled
        /// </summary>
        public static bool IsHotReloadEnabled
        {
            get => _isHotReloadEnabled;
            set
            {
                if (_isHotReloadEnabled != value)
                {
                    _isHotReloadEnabled = value;
                    if (value && Application.isPlaying)
                    {
                        // Refresh timestamps when enabling
                        RefreshContainerTimestamps();
                    }
                }
            }
        }

        static FlowGraphHotReloadManager()
        {
            Initialize();
        }

        private static void Initialize()
        {
            if (_isInitialized) return;
            
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            _isInitialized = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state is PlayModeStateChange.ExitingPlayMode or PlayModeStateChange.ExitingEditMode)
            {
                ContainerTimestamps.Clear();
            }
        }

        private static void OnUpdate()
        {
            // Only check during play mode and when hot reload is enabled
            if (!Application.isPlaying || !_isHotReloadEnabled) return;

            // Throttle checks to avoid performance issues
            var currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - _lastCheckTime < CheckInterval) return;
            _lastCheckTime = currentTime;

            CheckForChanges();
        }

        /// <summary>
        /// Refresh timestamps for all active FlowGraph containers
        /// </summary>
        public static void RefreshContainerTimestamps()
        {
            if (!Application.isPlaying)
            {
                CeresLogger.LogWarning("Hot reload is only available during play mode");
                return;
            }
            
            ContainerTimestamps.Clear();
            
            // Find all FlowGraphObjectBase instances in the scene
            var runtimeInstances = FlowGraphObjectBase.GetAllRuntimeInstances();
            foreach (var instance in runtimeInstances)
            {
                var container = instance.GetContainer();
                var graphData = container?.GetFlowGraphData();
                if (graphData != null)
                {
                    ContainerTimestamps[container] = graphData.saveTimestamp;
                }
            }
        }

        /// <summary>
        /// Check for changes in FlowGraph assets and trigger hot reload if needed
        /// </summary>
        public static void CheckForChanges()
        {
            if (!Application.isPlaying)
            {
                CeresLogger.LogWarning("Hot reload is only available during play mode");
                return;
            }
            
            var runtimeInstances = FlowGraphObjectBase.GetAllRuntimeInstances();
            var containersToReload = new HashSet<IFlowGraphContainer>();

            foreach (var instance in runtimeInstances)
            {
                var container = instance.GetContainer();

                var graphData = container?.GetFlowGraphData();
                if (graphData == null) continue;

                // Check if timestamp has changed
                if (ContainerTimestamps.TryGetValue(container, out var oldTimestamp))
                {
                    if (graphData.saveTimestamp != oldTimestamp)
                    {
                        containersToReload.Add(container);
                        ContainerTimestamps[container] = graphData.saveTimestamp;
                    }
                }
                else
                {
                    // New container, add to tracking
                    ContainerTimestamps[container] = graphData.saveTimestamp;
                }
            }

            // Trigger hot reload for changed containers
            foreach (var container in containersToReload)
            {
                ReloadContainer(container);
            }
        }

        /// <summary>
        /// Reload all runtime instances for a specific container
        /// </summary>
        private static void ReloadContainer(IFlowGraphContainer container)
        {
            var runtimeInstances = FlowGraphObjectBase.GetRuntimeInstances(container);
            if (runtimeInstances == null || runtimeInstances.Count == 0) return;

            foreach (var instance in runtimeInstances)
            {
                try
                {
                    ReloadInstance(instance, container);
                }
                catch (Exception ex)
                {
                    CeresLogger.LogError($"Failed to hot reload FlowGraph instance: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// Reload a single runtime instance
        /// </summary>
        private static void ReloadInstance(IFlowGraphRuntime runtime, IFlowGraphContainer container)
        {
            var oldGraph = runtime.Graph;
            if (oldGraph == null) return;

            // Check for active execution contexts
            var activeContext = oldGraph.GetExecutionContext();
            if (activeContext != null)
            {
                CeresLogger.LogWarning($"Hot reloading FlowGraph '{container.Object.name}' while execution is in progress. " +
                               "Active execution will continue with old graph instance, new events will use new graph.");
            }

            // Create new graph instance from updated data
            var graphData = container.GetFlowGraphData();
            FlowGraph newGraph;
            
            // In editor play mode, clone data to avoid modifying persistent data
            if (Application.isPlaying)
            {
                var clonedData = graphData.CloneT<FlowGraphData>();
                newGraph = clonedData.CreateFlowGraphInstance();
            }
            else
            {
                newGraph = graphData.CreateFlowGraphInstance();
            }

            // Compile new graph
            using var compilationContext = FlowGraphCompilationContext.GetPooled();
            using var compiler = CeresGraphCompiler.GetPooled(newGraph, compilationContext);
            newGraph.Compile(compiler);
            if (runtime is FlowGraphObjectBase flowGraphObject)
            {
                flowGraphObject.ReplaceGraph(newGraph);
            }

            CeresLogger.Log($"Hot reloaded FlowGraph: {container.Object.name}");
        }
    }
}

